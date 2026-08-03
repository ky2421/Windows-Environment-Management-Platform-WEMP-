using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 图形设置执行器：扫描 Steam 游戏库与桌面游戏快捷方式，
/// 为每个游戏 exe 添加系统"图形设置 → 高性能（独立显卡）"偏好。
/// 有核显的机器选择节能会导致游戏卡顿黑屏，高性能为推荐选项。
/// </summary>
public sealed class GpuAction : IOptimizationAction
{
    private const string GpuPrefKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\GraphicsDrivers\GPUPref\GpuPreferenceAppList";

    // 2 = 高性能（独立显卡），1 = 省电，0 = 系统默认
    private const int GpuPreferenceHighPerformance = 2;

    public string ItemType => "gpu";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.LocalMachine.OpenSubKey(GpuPrefKey, writable: false);
        var entries = new List<GpuPreferenceEntry>();
        if (key is not null)
        {
            foreach (var name in key.GetValueNames())
            {
                entries.Add(new GpuPreferenceEntry(name, key.GetValue(name) as byte[]));
            }
        }

        return Task.FromResult<object?>(entries);
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var executables = FindGameExecutables();
        if (executables.Count == 0)
        {
            throw new InvalidOperationException("未扫描到游戏程序（Steam 库或桌面快捷方式），请先安装游戏");
        }

        using var key = Registry.LocalMachine.CreateSubKey(GpuPrefKey, writable: true);
        foreach (var exe in executables)
        {
            // 已存在的条目跳过，保留用户手动配置
            if (key.GetValue(exe) is not null)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(exe);
            key.SetValue(exe, BuildPreferenceData(name), RegistryValueKind.Binary);
        }

        return Task.FromResult<object?>($"已为 {executables.Count} 个游戏启用高性能显卡偏好");
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<GpuPreferenceEntry> entries)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var key = Registry.LocalMachine.OpenSubKey(GpuPrefKey, writable: true);
        if (key is null)
        {
            return Task.CompletedTask;
        }

        // 恢复：原本存在的条目恢复原值，原本不存在的条目删除
        var existing = key.GetValueNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in existing)
        {
            var original = entries.FirstOrDefault(e => string.Equals(e.ExePath, name, StringComparison.OrdinalIgnoreCase));
            if (original is null || original.Data is null)
            {
                key.DeleteValue(name, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(name, original.Data, RegistryValueKind.Binary);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>扫描游戏 exe：Steam 库 + 桌面快捷方式。</summary>
    private static List<string> FindGameExecutables()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Steam 库：steamapps\common\<游戏>\ 顶层主程序
        var steamPath = Registry.CurrentUser.GetValue(
            @"Software\Valve\Steam", "SteamPath") as string;
        if (!string.IsNullOrEmpty(steamPath))
        {
            var commonDir = Path.Combine(steamPath, "steamapps", "common");
            if (Directory.Exists(commonDir))
            {
                foreach (var gameDir in Directory.EnumerateDirectories(commonDir))
                {
                    foreach (var exe in Directory.EnumerateFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        if (IsMainExecutable(exe) && seen.Add(exe))
                        {
                            found.Add(exe);
                        }
                    }
                }
            }
        }

        // 2) 桌面快捷方式：解析 .lnk 目标
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        foreach (var dir in new[] { desktop, publicDesktop })
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                var target = ResolveShortcutTarget(lnk);
                if (!string.IsNullOrEmpty(target)
                    && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && seen.Add(target))
                {
                    found.Add(target);
                }
            }
        }

        return found;
    }

    /// <summary>过滤辅助程序（卸载器/安装器/更新器/启动器）。</summary>
    internal static bool IsMainExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return !name.Contains("unins")
            && !name.Contains("uninstall")
            && !name.Contains("setup")
            && !name.Contains("redist")
            && !name.Contains("vc_redist")
            && !name.Contains("crash")
            && !name.Contains("update");
    }

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
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                try
                {
                    return shortcut.TargetPath as string;
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 构造 GPU 偏好二进制数据：[DWORD Name长度(UTF-16字节数)][Name UTF-16][DWORD GpuPreference]。
    /// 与"图形设置"页面写入格式一致。
    /// </summary>
    internal static byte[] BuildPreferenceData(string displayName)
    {
        var nameBytes = Encoding.Unicode.GetBytes(displayName);
        var data = new byte[4 + nameBytes.Length + 4];
        Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, data, 0, 4);
        Buffer.BlockCopy(nameBytes, 0, data, 4, nameBytes.Length);
        Buffer.BlockCopy(BitConverter.GetBytes(GpuPreferenceHighPerformance), 0, data, 4 + nameBytes.Length, 4);
        return data;
    }
}

/// <summary>GPU 偏好条目备份数据。</summary>
public sealed record GpuPreferenceEntry(string ExePath, byte[]? Data);
