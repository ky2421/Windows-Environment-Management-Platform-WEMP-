using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>审计日志服务实现。</summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly WempDbContext _db;

    public AuditLogService(WempDbContext db)
    {
        _db = db;
    }

    public async Task<(List<AuditLog> Items, int Total)> QueryAsync(
        string? module = null, string? level = null, string? keyword = null,
        DateTime? since = null, DateTime? until = null,
        int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(l => l.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(l => l.Level == level);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(l => (l.Action != null && l.Action.Contains(kw))
                                     || (l.Target != null && l.Target.Contains(kw))
                                     || (l.Message != null && l.Message.Contains(kw)));
        }

        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(l => l.Timestamp <= until.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (items, total);
    }

    public async Task<AuditStatistics> GetStatisticsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        var byModule = (await query
            .GroupBy(l => l.Module)
            .Select(g => new { Module = g.Key, Count = g.Count() })
            .OrderByDescending(m => m.Count)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(m => new ModuleCount(m.Module, m.Count))
            .ToList();

        var byLevel = (await query
            .GroupBy(l => l.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .OrderByDescending(l => l.Count)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(l => new LevelCount(l.Level, l.Count))
            .ToList();

        var total = byModule.Sum(m => m.Count);
        var failed24h = await _db.AuditLogs
            .AsNoTracking()
            .CountAsync(l => l.Timestamp >= DateTime.Now.AddHours(-24) && l.Result == "failed", cancellationToken)
            .ConfigureAwait(false);

        return new AuditStatistics(total, byModule, byLevel, failed24h);
    }

    public async Task WriteAsync(string module, string action, string? target = null, string? message = null,
        string? result = null, string? detailJson = null, string level = "info", CancellationToken cancellationToken = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            Module = module,
            Level = level,
            Action = action,
            Target = target,
            Message = message,
            DetailJson = detailJson,
            Result = result,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
