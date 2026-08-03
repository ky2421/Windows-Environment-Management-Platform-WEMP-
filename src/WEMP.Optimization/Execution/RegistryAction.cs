using Microsoft.Win32;
using System.Text.Json;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 注册表优化执行器（registry 与 game 类别共用）：
/// 支持单键单值、单键多值、多键多值三种模式的备份/写入/恢复。
/// </summary>
public sealed class RegistryAction : IOptimizationAction
{
    public string ItemType => "registry";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 多键模式：每个键独立备份
        if (target.Keys is { } keys)
        {
            var result = keys
                .Select(spec => BackupKey(spec.Key, spec.Values))
                .ToList();
            return Task.FromResult<object?>(new RegistryBackup(false, null, null) { MultiKeys = result });
        }

        // 多值模式：同一键下多个值
        if (target.Values is { } values)
        {
            var backup = BackupKey(target.Key, values);
            return Task.FromResult<object?>(new RegistryBackup(false, null, null) { Multi = backup.Values });
        }

        // 单值模式（历史兼容）
        using var key = OpenKey(target.Key, writable: false);
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

        if (target.Keys is { } keys)
        {
            foreach (var spec in keys)
            {
                ApplyKey(spec.Key, spec.Values);
            }

            return Task.FromResult<object?>(null);
        }

        if (target.Values is { } values)
        {
            ApplyKey(target.Key, values);
            return Task.FromResult<object?>(null);
        }

        if (target.ValueName is { } singleName)
        {
            using var key = OpenKey(target.Key, writable: true)
                ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{target.Key}");
            WriteValue(key, singleName, target.ValueData);
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

        // 多键恢复
        if (b.MultiKeys is { } multiKeys)
        {
            foreach (var keyBackup in multiKeys)
            {
                RestoreKey(keyBackup.KeyPath, keyBackup.Values);
            }

            return Task.CompletedTask;
        }

        // 多值恢复
        if (b.Multi is { } multi)
        {
            RestoreKey(target.Key, multi);
            return Task.CompletedTask;
        }

        // 单值恢复（历史兼容）
        using var key = OpenKey(target.Key, writable: true)
            ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{target.Key}");

        if (b.Exists)
        {
            var kind = Enum.TryParse<RegistryValueKind>(b.Kind, out var parsed) ? parsed : RegistryValueKind.Unknown;
            key.SetValue(target.ValueName, ConvertRestoredValue(b.Data, kind), kind);
        }
        else if (target.ValueName is { } name)
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }

    // ---- 内部实现 ----

    private static RegistryKeyBackup BackupKey(string? keyPath, List<RegistryValueSpec>? specs)
    {
        using var key = OpenKey(keyPath, writable: false);
        var values = specs?.Select(v =>
        {
            var name = v.Name ?? "";
            var current = key?.GetValue(name);
            return current is null
                ? new RegistryValueBackup(name, false, null, null)
                : new RegistryValueBackup(name, true, key!.GetValueKind(name).ToString(), current);
        }).ToList() ?? [];

        return new RegistryKeyBackup(keyPath, values);
    }

    private static void ApplyKey(string? keyPath, List<RegistryValueSpec>? specs)
    {
        using var key = OpenKey(keyPath, writable: true)
            ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{keyPath}");

        foreach (var value in specs ?? [])
        {
            WriteValue(key, value.Name, value.Data);
        }
    }

    private static void RestoreKey(string? keyPath, List<RegistryValueBackup>? values)
    {
        using var key = OpenKey(keyPath, writable: true)
            ?? throw new InvalidOperationException($"无法打开注册表键（需管理员权限）：{keyPath}");

        foreach (var item in values ?? [])
        {
            if (item.Exists)
            {
                var kind = Enum.TryParse<RegistryValueKind>(item.Kind, out var parsed) ? parsed : RegistryValueKind.Unknown;
                key.SetValue(item.Name, ConvertRestoredValue(item.Data, kind), kind);
            }
            else
            {
                key.DeleteValue(item.Name, throwOnMissingValue: false);
            }
        }
    }

    /// <summary>
    /// 备份数据经 JSON 序列化（BeforeJson）往返后，int/byte[] 等会变成 JsonElement，
    /// 需按 RegistryValueKind 还原为 .NET 类型，否则 SetValue 抛类型不匹配异常。
    /// 返回非 null：null 数据按 kind 取安全默认值，避免 SetValue 抛参数异常。
    /// </summary>
    private static object ConvertRestoredValue(object? data, RegistryValueKind kind)
    {
        if (data is not JsonElement je)
        {
            return data ?? (kind switch
            {
                RegistryValueKind.DWord => 0,
                RegistryValueKind.QWord => 0L,
                RegistryValueKind.Binary => Array.Empty<byte>(),
                _ => string.Empty,
            });
        }

        return kind switch
        {
            RegistryValueKind.DWord => je.GetInt32(),
            RegistryValueKind.QWord => je.GetInt64(),
            RegistryValueKind.Binary => je.GetBytesFromBase64(),
            RegistryValueKind.String or RegistryValueKind.ExpandString => je.GetString() ?? "",
            _ => je.GetRawText(),
        };
    }

    private static void WriteValue(RegistryKey key, string? name, JsonElement? data)
    {
        if (data is { } element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    key.SetValue(name, element.GetInt32(), RegistryValueKind.DWord);
                    break;
                case JsonValueKind.String:
                    key.SetValue(name, element.GetString() ?? "", RegistryValueKind.String);
                    break;
                default:
                    key.SetValue(name, element.GetRawText(), RegistryValueKind.String);
                    break;
            }
        }
        else if (name is { } valueName)
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static RegistryKey? OpenKey(string? keyPath, bool writable)
    {
        if (string.IsNullOrEmpty(keyPath))
        {
            throw new ArgumentException("缺少注册表键路径（target.key）", nameof(keyPath));
        }

        var (hive, subPath) = ParseKey(keyPath);
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
public sealed record RegistryBackup(bool Exists, string? Kind, object? Data)
{
    /// <summary>多值备份（同一键下多个值的原始状态）。</summary>
    public List<RegistryValueBackup>? Multi { get; init; }

    /// <summary>多键备份（每个键一组值）。</summary>
    public List<RegistryKeyBackup>? MultiKeys { get; init; }
}

/// <summary>注册表单值备份数据。</summary>
public sealed record RegistryValueBackup(string Name, bool Exists, string? Kind, object? Data);

/// <summary>注册表键级备份数据。</summary>
public sealed record RegistryKeyBackup(string? KeyPath, List<RegistryValueBackup>? Values);
