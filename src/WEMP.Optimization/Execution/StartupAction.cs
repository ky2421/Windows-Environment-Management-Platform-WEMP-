using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 启动项优化执行器：备份 Run 键全部条目，按厂商关键词禁用 OEM 自启，回滚时恢复。
/// </summary>
public sealed class StartupAction : IOptimizationAction
{
    public string ItemType => "startup";

    public bool SupportsBackup => true;

    /// <summary>OEM 厂商关键词（匹配启动项名称或可执行路径）。</summary>
    private static readonly string[] VendorKeywords =
    [
        "asus", "asustek", "lenovo", "ideapad", "dell", "hp inc", "hewlett", "acer", "msi",
        "gigabyte", "razer", "microsoft corporation", "mcafee", "norton", "nvidia", "realtek",
    ];

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = new List<StartupEntry>();
        foreach (var (hive, path) in RunKeys())
        {
            using var key = OpenRunKey(hive, path, writable: false);
            if (key is null)
            {
                continue;
            }

            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    entries.Add(new StartupEntry(hive, path, name, value, Disabled: false));
                }
            }
        }

        return Task.FromResult<object?>(entries);
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<StartupEntry> entries)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var entry in entries)
        {
            if (!MatchesVendor(entry))
            {
                continue;
            }

            using var key = OpenRunKey(entry.Hive, entry.Path, writable: true);
            key?.DeleteValue(entry.Name, throwOnMissingValue: false);
        }

        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<StartupEntry> entries)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var entry in entries)
        {
            using var key = OpenRunKey(entry.Hive, entry.Path, writable: true);
            if (key is null)
            {
                continue;
            }

            if (key.GetValue(entry.Name) is null)
            {
                key.SetValue(entry.Name, entry.Value, RegistryValueKind.String);
            }
        }

        return Task.CompletedTask;
    }

    private static bool MatchesVendor(StartupEntry entry)
    {
        var haystack = $"{entry.Name} {entry.Value}".ToLowerInvariant();
        return VendorKeywords.Any(haystack.Contains);
    }

    private static IEnumerable<(string Hive, string Path)> RunKeys()
    {
        const string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        yield return ("HKCU", runPath);
        yield return ("HKLM", runPath);
    }

    private static RegistryKey? OpenRunKey(string hive, string path, bool writable)
    {
        var baseKey = hive switch
        {
            "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
            "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
            _ => null,
        };

        return baseKey?.OpenSubKey(path, writable);
    }
}

/// <summary>启动项备份数据。</summary>
public sealed record StartupEntry(string Hive, string Path, string Name, string Value, bool Disabled);
