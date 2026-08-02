using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// 日志中心服务：审计日志查询/写入、Windows 事件聚合（去重入库）、
/// 异常规则扫描（写 log_anomalies）与异常处置。
/// </summary>
public interface ILoggingService
{
    Task<(List<AuditLog> Items, int Total)> QueryAuditsAsync(
        string? module = null, string? level = null, string? keyword = null,
        DateTime? since = null, DateTime? until = null,
        int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);

    Task<AuditStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    Task WriteAuditAsync(string module, string action, string? target = null, string? message = null,
        string? result = null, string? detailJson = null, string level = "info", CancellationToken cancellationToken = default);

    /// <summary>从 Windows 事件日志聚合最近窗口事件到 system_events（按 时间+来源+事件ID 去重）。</summary>
    Task<int> AggregateEventsAsync(TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>查询已聚合的系统事件（按时间倒序，可限制条数）。</summary>
    Task<List<SystemEvent>> GetSystemEventsAsync(int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>对窗口内系统事件与审计日志执行异常扫描并去重写入 log_anomalies。</summary>
    Task<int> RunAnomalyScanAsync(TimeSpan window, CancellationToken cancellationToken = default);

    Task<List<LogAnomaly>> GetAnomaliesAsync(bool includeResolved = false, CancellationToken cancellationToken = default);

    /// <summary>将异常标记为已解决。</summary>
    Task<bool> ResolveAnomalyAsync(long anomalyId, CancellationToken cancellationToken = default);
}
