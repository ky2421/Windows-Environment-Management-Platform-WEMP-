using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 计时器命令优化执行器：bcdedit /set useplatformclock false，
/// 让系统使用 TSC 等高效计时器而非平台时钟（HPET），降低计时开销。
/// 需管理员权限；部分网游、直播、音频软件可能出现计时异常，可随时回滚。
/// </summary>
public sealed partial class TimerAction : IOptimizationAction
{
    public string ItemType => "timer";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync("bcdedit.exe", "/enum {current}", cancellationToken);
        var match = ValueRegex().Match(result.Output);
        if (!match.Success)
        {
            // 无 useplatformclock 条目 = 默认（等效 false）
            return new TimerBackup(false, null);
        }

        return new TimerBackup(true, match.Groups[1].Value.Equals("Yes", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync(
            "bcdedit.exe", "/set useplatformclock false", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"设置计时器失败（需管理员权限）：{result.Output.Trim()}");
        }

        return "已设置 useplatformclock=false（计时器优化完成）";
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not TimerBackup b)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        ProcessRunner.CommandResult result;
        if (b.HadValue)
        {
            // 恢复原值（默认条目恢复默认）
            var value = b.OriginalValue == true ? "true" : "false";
            result = await ProcessRunner.RunAsync(
                "bcdedit.exe", $"/set useplatformclock {value}", cancellationToken);
        }
        else
        {
            // 原本无条目：删除条目恢复默认
            result = await ProcessRunner.RunAsync(
                "bcdedit.exe", "/deletevalue useplatformclock", cancellationToken);
        }

        if (!result.Success)
        {
            throw new InvalidOperationException($"恢复计时器设置失败（需管理员权限）：{result.Output.Trim()}");
        }
    }

    [GeneratedRegex(@"^\s*useplatformclock\s+(Yes|No)", RegexOptions.Multiline)]
    private static partial Regex ValueRegex();
}

/// <summary>计时器设置备份数据。</summary>
public sealed record TimerBackup(bool HadValue, bool? OriginalValue);
