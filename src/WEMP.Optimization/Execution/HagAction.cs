using System.Text.RegularExpressions;
using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 硬件加速 GPU 计划（HAGS）执行器：修改 DirectXUserGlobalSettings 中的
/// SwapEffectUpgradeEnable 标志（1=开启 / 0=关闭）。修改后需重启电脑生效。
/// </summary>
public sealed partial class HagAction : IOptimizationAction
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ValueName = "DirectXUserGlobalSettings";

    public string ItemType => "hags";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return Task.FromResult<object?>(new HagBackup(value));
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enable = target.Enable ?? true;
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);

        var current = key.GetValue(ValueName) as string;
        var updated = UpdateHagsFlag(current, enable);

        key.SetValue(ValueName, updated, RegistryValueKind.String);
        return Task.FromResult<object?>($"硬件加速 GPU 计划已{(enable ? "开启" : "关闭")}（重启电脑后生效）");
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not HagBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (b.OriginalValue is null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(ValueName, b.OriginalValue, RegistryValueKind.String);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新 DirectXUserGlobalSettings 中的 HAGS 标志（纯字符串逻辑，不触碰注册表，可单测）。
    /// 已有标志则原位替换，否则追加到末尾（自动补分号分隔）。
    /// </summary>
    internal static string UpdateHagsFlag(string? current, bool enable)
    {
        var flag = enable ? "0x1" : "0x0";
        if (string.IsNullOrWhiteSpace(current))
        {
            return $"SwapEffectUpgradeEnable={flag};";
        }

        if (FlagRegex().IsMatch(current))
        {
            return FlagRegex().Replace(current, $"SwapEffectUpgradeEnable={flag}");
        }

        return current.TrimEnd(';') + $";SwapEffectUpgradeEnable={flag};";
    }

    [GeneratedRegex(@"SwapEffectUpgradeEnable=0x[01]")]
    private static partial Regex FlagRegex();
}

/// <summary>HAGS 备份数据。</summary>
public sealed record HagBackup(string? OriginalValue);
