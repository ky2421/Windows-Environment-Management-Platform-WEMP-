using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;

namespace WEMP.Core.Tests;

/// <summary>
/// 执行层备份行为测试：仅调用只读的 BackupAsync（读注册表/枚举进程/查询系统），
/// 不执行任何 Apply/Restore，确保测试不改变真实系统状态。
/// 断言限定为结构不变量，不依赖具体机器状态。
/// </summary>
public class OptimizationActionBackupTests
{
    [Fact]
    public async Task HagAction_backup_reads_current_value_structure()
    {
        var action = new HagAction();

        var backup = await action.BackupAsync(Target(enable: true), CancellationToken.None);

        var hag = Assert.IsType<HagBackup>(backup);
        // 值可为 null（未配置过）或字符串，仅验证类型
        Assert.True(hag.OriginalValue is null || hag.OriginalValue.Length >= 0);
    }

    [Fact]
    public async Task BackgroundAction_backup_reads_current_value_structure()
    {
        var action = new BackgroundAction();

        var backup = await action.BackupAsync(Target(), CancellationToken.None);

        var bg = Assert.IsType<BackgroundBackup>(backup);
        // Exists 与 OriginalValue 取决于真实用户设置，仅验证结构
        _ = bg.Exists;
        _ = bg.OriginalValue;
    }

    [Fact]
    public async Task VisualAction_backup_reads_current_visual_effects_structure()
    {
        var action = new VisualAction();

        var backup = await action.BackupAsync(Target(), CancellationToken.None);

        var visual = Assert.IsType<VisualBackup>(backup);
        Assert.True(visual.UserPreferencesMask is null or { Length: > 0 });
    }

    [Fact]
    public async Task MemoryAction_backup_enumerates_whitelisted_processes()
    {
        var action = new MemoryAction();

        var backup = await action.BackupAsync(Target(), CancellationToken.None);

        var processes = Assert.IsType<List<ProcessBackup>>(backup);
        // 白名单进程可能未运行 → 结果可为空；若有条目则名称非空
        Assert.All(processes, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public async Task TimerAction_backup_parses_boot_config_or_returns_default()
    {
        var action = new TimerAction();

        var backup = await action.BackupAsync(Target(), CancellationToken.None);

        var timer = Assert.IsType<TimerBackup>(backup);
        if (timer.HadValue)
        {
            Assert.NotNull(timer.OriginalValue);
        }
    }

    [Fact]
    public async Task Backup_cancelled_token_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new HagAction().BackupAsync(Target(), cts.Token));
    }

    private static OptimizationTarget Target(bool? enable = null)
        => new() { Enable = enable };
}
