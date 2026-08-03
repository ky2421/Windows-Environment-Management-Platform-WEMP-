using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WEMP.PackageManagement.Infrastructure;

/// <summary>
/// 已安装软件图标解析：winget 列表不含图标信息，按显示名称多级回退解析图标路径——
/// 1) 注册表卸载键 DisplayIcon 精确匹配；2) 名称归一化匹配；3) InstallLocation 目录主程序 exe；
/// 4) 开始菜单快捷方式。卸载信息进程内缓存，避免重复扫描。
/// </summary>
public static partial class InstalledIconResolver
{
    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    private static readonly object Sync = new();
    private static bool _scanned;

    private static Dictionary<string, string> _exactIcons = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, List<string>> _normalizedIcons = [];
    private static Dictionary<string, List<string>> _normalizedLocations = [];
    private static Dictionary<string, List<string>> _startMenuApps = [];

    /// <summary>按显示名称解析图标文件路径；未命中返回 null。</summary>
    public static string? Resolve(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        EnsureScanned();
        var name = displayName.Trim();

        // 1) 注册表 DisplayIcon 精确匹配
        if (_exactIcons.TryGetValue(name, out var exact) && IsUsableIcon(CleanIcon(exact)))
        {
            return CleanIcon(exact);
        }

        // 2) 归一化匹配 DisplayIcon（忽略大小写/空白/括号/版本后缀差异）
        var normalized = NormalizeName(name);
        if (_normalizedIcons.TryGetValue(normalized, out var icons))
        {
            foreach (var candidate in icons)
            {
                var icon = CleanIcon(candidate);
                if (IsUsableIcon(icon))
                {
                    return icon;
                }
            }
        }

        // 3) InstallLocation 目录下主程序 exe
        if (_normalizedLocations.TryGetValue(normalized, out var locations))
        {
            foreach (var location in locations)
            {
                var exe = PickExecutable(location);
                if (exe is not null)
                {
                    return exe;
                }
            }
        }

        // 4) 开始菜单快捷方式（文件名归一化匹配，解析目标或直接交给提取器）
        if (_startMenuApps.TryGetValue(normalized, out var shortcuts))
        {
            foreach (var shortcut in shortcuts)
            {
                var target = ResolveShortcutTarget(shortcut) ?? shortcut;
                if (IsUsableIcon(target))
                {
                    return target;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 清理注册表 DisplayIcon 值：去外层引号、去逗号后的图标索引
    /// （如 <c>"C:\App\app.exe",0</c> → <c>C:\App\app.exe</c>）。
    /// </summary>
    public static string? CleanIcon(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var value = displayIcon.Trim().Trim('"');
        var comma = value.IndexOf(',');
        if (comma > 0)
        {
            value = value[..comma].Trim().Trim('"');
        }

        return value.Length == 0 ? null : value;
    }

    /// <summary>归一化软件名：小写、去空白、去括号内容与尾部版本号（如 "Google Chrome (x64)" → "googlechrome"）。</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var value = name.ToLowerInvariant();
        value = BracketsRegex().Replace(value, "");
        value = WhitespaceRegex().Replace(value, "");
        value = TrailingVersionRegex().Replace(value, "");
        return value;
    }

    private static void EnsureScanned()
    {
        lock (Sync)
        {
            if (_scanned)
            {
                return;
            }

            var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var normIcons = new Dictionary<string, List<string>>();
            var normLocations = new Dictionary<string, List<string>>();
            foreach (var root in UninstallRoots)
            {
                ScanRegistryHive(RegistryHive.LocalMachine, root, exact, normIcons, normLocations);
            }

            ScanRegistryHive(RegistryHive.CurrentUser, UninstallRoots[0], exact, normIcons, normLocations);

            _exactIcons = exact;
            _normalizedIcons = normIcons;
            _normalizedLocations = normLocations;
            _startMenuApps = ScanStartMenu();
            _scanned = true;
        }
    }

    private static void ScanRegistryHive(
        RegistryHive hive,
        string subKey,
        Dictionary<string, string> exact,
        Dictionary<string, List<string>> normIcons,
        Dictionary<string, List<string>> normLocations)
    {
        using var key = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64)
            .OpenSubKey(subKey);
        if (key is null)
        {
            return;
        }

        foreach (var childName in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(childName);
            if (child is null)
            {
                continue;
            }

            var name = child.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalized = NormalizeName(name);
            var icon = child.GetValue("DisplayIcon") as string;
            if (!string.IsNullOrWhiteSpace(icon))
            {
                exact.TryAdd(name.Trim(), icon);
                AddToList(normIcons, normalized, icon);
            }

            var location = child.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(location))
            {
                AddToList(normLocations, normalized, location.Trim().Trim('"'));
            }
        }
    }

    /// <summary>扫描开始菜单程序目录（当前用户 + 所有用户），文件名为应用显示名（去 .lnk）。</summary>
    private static Dictionary<string, List<string>> ScanStartMenu()
    {
        var map = new Dictionary<string, List<string>>();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        };
        var commonStart = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        if (!string.IsNullOrWhiteSpace(commonStart))
        {
            roots.Add(Path.Combine(commonStart, "Programs"));
        }

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var appName = Path.GetFileNameWithoutExtension(lnk);
                if (string.IsNullOrWhiteSpace(appName))
                {
                    continue;
                }

                AddToList(map, NormalizeName(appName), lnk);
            }
        }

        return map;
    }

    /// <summary>解析 .lnk 快捷方式目标（优先自定义图标路径，其次目标 exe）；失败返回 null。</summary>
    private static string? ResolveShortcutTarget(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            var iconLocation = (string?)shortcut.IconLocation;
            var cleaned = CleanIcon(iconLocation);
            if (IsUsableIcon(cleaned))
            {
                return cleaned;
            }

            var target = (string?)shortcut.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>InstallLocation 目录下挑主程序 exe：优先与显示名同名的 exe，否则取最大的 exe。</summary>
    private static string? PickExecutable(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var exes = Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(f => !IsSetupExecutable(Path.GetFileNameWithoutExtension(f)))
            .ToList();
        if (exes.Count == 0)
        {
            return null;
        }

        var normalizedDir = NormalizeName(Path.GetFileName(directory));
        var byName = exes.FirstOrDefault(f => NormalizeName(Path.GetFileNameWithoutExtension(f)) == normalizedDir);
        return byName ?? exes.OrderByDescending(f => new FileInfo(f).Length).First();
    }

    private static bool IsSetupExecutable(string nameWithoutExtension)
    {
        var lower = nameWithoutExtension.ToLowerInvariant();
        return lower.StartsWith("unins")
            || lower.StartsWith("setup")
            || lower.StartsWith("install")
            || lower.Contains("redist")
            || lower.Contains("vcredist")
            || lower.StartsWith("update")
            || lower.StartsWith("msiexec")
            || lower.StartsWith("dotnet")
            || lower.StartsWith("vc_");
    }

    private static bool IsUsableIcon(string? path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static void AddToList(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(value);
    }

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex BracketsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\d+(\.\d+)+$")]
    private static partial Regex TrailingVersionRegex();
}
