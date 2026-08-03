using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Optimization.Services;

/// <summary>单项优化执行结果。</summary>
public sealed record OptimizationResult(
    string ItemCode,
    string Name,
    bool Success,
    string Message,
    long DurationMs,
    string Action);

/// <summary>批量优化执行结果。</summary>
public sealed record OptimizationBatchResult(IReadOnlyList<OptimizationResult> Results)
{
    public int SuccessCount => Results.Count(r => r.Success);

    public int FailureCount => Results.Count(r => !r.Success);
}

/// <summary>系统优化服务：一键优化、自定义优化、回滚与历史记录。</summary>
public interface IOptimizationService
{
    /// <summary>获取全部优化知识库条目。</summary>
    Task<IReadOnlyList<OptimizationItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>一键优化：执行全部启用的知识库条目。</summary>
    Task<OptimizationBatchResult> ApplyOneKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>自定义优化：执行指定 Code 的条目。</summary>
    Task<OptimizationBatchResult> ApplySelectedAsync(
        IEnumerable<string> codes, CancellationToken cancellationToken = default);

    /// <summary>回滚指定 Code 的最近一次成功优化。</summary>
    Task<OptimizationBatchResult> RollbackAsync(
        IEnumerable<string> codes, CancellationToken cancellationToken = default);

    /// <summary>回滚全部已成功优化的条目。</summary>
    Task<OptimizationBatchResult> RollbackAllAsync(CancellationToken cancellationToken = default);

    /// <summary>回滚指定历史记录对应的快照（恢复到该记录应用前的状态）。</summary>
    Task<OptimizationBatchResult> RollbackRecordAsync(
        long recordId, CancellationToken cancellationToken = default);

    /// <summary>获取最近 N 条优化执行记录。</summary>
    Task<IReadOnlyList<OptimizationRecord>> GetHistoryAsync(
        int count, CancellationToken cancellationToken = default);
}
