using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// BIOS 优化指引执行器：检测 CPU 平台（Intel/AMD），
/// 给出 XMP/EXPO 内存超频开启指引。BIOS 设置无法程序化修改，仅提供检测与指引。
/// </summary>
public sealed class BiosAction : IOptimizationAction
{
    public string ItemType => "bios";

    public bool SupportsBackup => false;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isIntel = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
            ?.Contains("Intel", StringComparison.OrdinalIgnoreCase) == true;

        var message = isIntel
            ? "检测到 Intel 平台：开机按 Del/F2 进入 BIOS，开启 XMP（内存标称高频）。\n风险：内存体质差或四条内存易蓝屏、无法开机，出现不稳定立即关闭。"
            : "检测到 AMD 平台：开机按 Del/F2 进入 BIOS，开启 EXPO（内存标称高频）。\n风险：内存体质差或四条内存易蓝屏、无法开机，出现不稳定立即关闭。";

        return Task.FromResult<object?>(message);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        => throw new NotSupportedException("BIOS 设置需在 BIOS 界面手动还原（关闭 XMP/EXPO）");
}

/// <summary>BIOS 指引备份数据（不可回滚，未使用）。</summary>
public sealed record BiosBackup(string? Message);
