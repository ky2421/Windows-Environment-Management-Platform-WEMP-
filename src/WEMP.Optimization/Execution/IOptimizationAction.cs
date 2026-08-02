using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 单类优化执行器：备份 → 应用 → （回滚）三阶段。
/// 备份数据为可序列化对象，随优化记录持久化，用于回滚。
/// </summary>
public interface IOptimizationAction
{
    /// <summary>执行器类别标识（registry/service/startup/network/disk/power/memory）。</summary>
    string ItemType { get; }

    /// <summary>是否支持备份与回滚；false 表示不可恢复（如清理类操作）。</summary>
    bool SupportsBackup { get; }

    /// <summary>执行前备份原始状态，返回备份数据。</summary>
    Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken);

    /// <summary>应用优化。backup 为 <see cref="BackupAsync"/> 的返回值。</summary>
    Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken);

    /// <summary>用备份数据恢复原始状态。</summary>
    Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken);
}
