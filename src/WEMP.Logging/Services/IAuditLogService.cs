using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// 审计日志查询与写入服务（表：audit_logs）。
/// 查询支持模块/级别/关键字过滤与时间窗口，统计按模块与级别聚合。
/// </summary>
public interface IAuditLogService
{
    /// <summary>分页查询审计日志。</summary>
    Task<(List<AuditLog> Items, int Total)> QueryAsync(
        string? module = null, string? level = null, string? keyword = null,
        DateTime? since = null, DateTime? until = null,
        int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);

    /// <summary>统计聚合（总条数、按模块计数、按级别计数、最近 24h 失败条数）。</summary>
    Task<AuditStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>写入一条审计日志。</summary>
    Task WriteAsync(string module, string action, string? target = null, string? message = null,
        string? result = null, string? detailJson = null, string level = "info", CancellationToken cancellationToken = default);
}

/// <summary>审计日志统计结果。</summary>
public sealed record AuditStatistics(
    int Total,
    IReadOnlyList<ModuleCount> ByModule,
    IReadOnlyList<LevelCount> ByLevel,
    int Failed24h);

/// <summary>按模块计数。</summary>
public sealed record ModuleCount(string Module, int Count);

/// <summary>按级别计数。</summary>
public sealed record LevelCount(string Level, int Count);
