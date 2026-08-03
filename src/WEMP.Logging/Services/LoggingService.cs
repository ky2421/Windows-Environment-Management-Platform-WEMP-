using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>日志中心服务实现。</summary>
public sealed class LoggingService : ILoggingService
{
    private readonly IDbContextFactory<WempDbContext> _dbFactory;
    private readonly IAuditLogService _audits;
    private readonly IEventSource _eventSource;
    private readonly IAnomalyDetector _detector;

    public LoggingService(IDbContextFactory<WempDbContext> dbFactory, IAuditLogService audits, IEventSource eventSource, IAnomalyDetector detector)
    {
        _dbFactory = dbFactory;
        _audits = audits;
        _eventSource = eventSource;
        _detector = detector;
    }

    public Task<(List<AuditLog> Items, int Total)> QueryAuditsAsync(
        string? module = null, string? level = null, string? keyword = null,
        DateTime? since = null, DateTime? until = null,
        int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _audits.QueryAsync(module, level, keyword, since, until, page, pageSize, cancellationToken);

    public Task<AuditStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
        => _audits.GetStatisticsAsync(since, cancellationToken);

    public Task WriteAuditAsync(string module, string action, string? target = null, string? message = null,
        string? result = null, string? detailJson = null, string level = "info", CancellationToken cancellationToken = default)
        => _audits.WriteAsync(module, action, target, message, result, detailJson, level, cancellationToken);

    public async Task<int> AggregateEventsAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var added = 0;
        foreach (var channel in new[] { "Application", "System" })
        {
            var events = await _eventSource.ReadRecentAsync(channel, window, cancellationToken).ConfigureAwait(false);
            foreach (var evt in events)
            {
                var exists = await db.SystemEvents.AsNoTracking().AnyAsync(
                    e => e.EventTime == evt.EventTime && e.Provider == evt.Provider && e.EventId == evt.EventId,
                    cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    continue;
                }

                evt.Provider ??= channel;
                db.SystemEvents.Add(evt);
                added++;
            }

            // 每通道提交一次，保证后续通道去重查询能看见已入库事件
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        Log.Information("事件聚合完成：新增 {Count} 条", added);
        return added;
    }

    public async Task<List<SystemEvent>> GetSystemEventsAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.SystemEvents.AsNoTracking()
            .OrderByDescending(e => e.EventTime)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RunAnomalyScanAsync(TimeSpan window, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var since = DateTime.Now.Subtract(window);

        var events = await db.SystemEvents.AsNoTracking()
            .Where(e => e.EventTime >= since)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Timestamp >= since)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var candidates = _detector.Detect(events, audits);
        var added = 0;
        foreach (var anomaly in candidates)
        {
            var duplicate = await db.LogAnomalies.AsNoTracking().AnyAsync(
                a => a.RuleCode == anomaly.RuleCode
                     && a.Title == anomaly.Title
                     && a.Status == "new"
                     && a.DetectedAt >= DateTime.Now.AddHours(-24),
                cancellationToken).ConfigureAwait(false);
            if (duplicate)
            {
                continue;
            }

            db.LogAnomalies.Add(anomaly);
            added++;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("异常扫描完成：发现 {Count} 条新异常", added);
        return added;
    }

    public async Task<List<LogAnomaly>> GetAnomaliesAsync(bool includeResolved = false, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = db.LogAnomalies.AsNoTracking();
        if (!includeResolved)
        {
            query = query.Where(a => a.Status != "resolved");
        }

        return await query.OrderByDescending(a => a.DetectedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ResolveAnomalyAsync(long anomalyId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var anomaly = await db.LogAnomalies.FindAsync([anomalyId], cancellationToken).ConfigureAwait(false);
        if (anomaly is null)
        {
            return false;
        }

        anomaly.Status = "resolved";
        anomaly.ResolvedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
