using Microsoft.Win32;
using System.Text.Json;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>注册表优化执行器（registry 与 game 类别共用）：备份、写值/删值、恢复。</summary>
public sealed class RegistryAction : IOptimizationAction
{
    public string ItemType => "registry";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = OpenKey(target, writable: false);
        if (key is null)
        {
            return Task.FromResult<object?>(new RegistryBackup(false, null, null));
        }

        var value = target.ValueName is { } name ? key.GetValue(name) : null;
        if (value is null)
        {
            return Task.FromResult<object?>(new RegistryBackup(false, null, null));
        }

        var kind = key.GetValueKind(target.ValueName!);
        return Task.FromResult<object?>(new RegistryBackup(true, kind.ToString(), value));
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = OpenKey(target, writable: true)
            ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{target.Key}");

        if (target.ValueData is { } data)
        {
            switch (data.ValueKind)
            {
                case JsonValueKind.Number:
                    key.SetValue(target.ValueName, data.GetInt32(), RegistryValueKind.DWord);
                    break;
                case JsonValueKind.String:
                    key.SetValue(target.ValueName, data.GetString() ?? "", RegistryValueKind.String);
                    break;
                default:
                    key.SetValue(target.ValueName, data.GetRawText(), RegistryValueKind.String);
                    break;
            }
        }
        else if (target.ValueName is { } name)
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }

        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not RegistryBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var key = OpenKey(target, writable: true)
            ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{target.Key}");

        if (b.Exists)
        {
            var kind = Enum.TryParse<RegistryValueKind>(b.Kind, out var parsed) ? parsed : RegistryValueKind.Unknown;
            key.SetValue(target.ValueName, b.Data ?? "", kind);
        }
        else if (target.ValueName is { } name)
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }

    private static RegistryKey? OpenKey(OptimizationTarget target, bool writable)
    {
        if (string.IsNullOrEmpty(target.Key))
        {
            throw new ArgumentException("缺少注册表键路径（target.key）", nameof(target));
        }

        var (hive, subPath) = ParseKey(target.Key);
        using var baseKey = hive switch
        {
            "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
            "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
            _ => throw new NotSupportedException($"不支持的注册表根键：{hive}"),
        };

        // 写模式下键不存在时自动创建（HKCU 测试键/新优化项适用）
        return writable ? baseKey.CreateSubKey(subPath) : baseKey.OpenSubKey(subPath);
    }

    private static (string Hive, string SubPath) ParseKey(string key)
    {
        var index = key.IndexOf('\\');
        return index < 0 ? (key, "") : (key[..index], key[(index + 1)..]);
    }
}

/// <summary>注册表备份数据。</summary>
public sealed record RegistryBackup(bool Exists, string? Kind, object? Data);
