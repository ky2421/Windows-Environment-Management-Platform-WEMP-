using Microsoft.Win32;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 后台应用权限执行器：关闭"后台应用"全部权限（GlobalUserDisabled=1）。
/// 系统设置 → 隐私和安全性 → 后台应用 → 全部关闭。
/// </summary>
public sealed class BackgroundAction : IOptimizationAction
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    private const string ValueName = "GlobalUserDisabled";

    public string ItemType => "background";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName);
        return Task.FromResult<object?>(new BackgroundBackup(value is null, value is null ? 0 : Convert.ToInt32(value)));
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, 1, RegistryValueKind.DWord);
        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not BackgroundBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (b.Exists)
        {
            key.SetValue(ValueName, b.OriginalValue, RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }
}

/// <summary>后台应用设置备份数据。</summary>
public sealed record BackgroundBackup(bool Exists, int OriginalValue);
