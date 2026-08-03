using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Logging.Services;

namespace WEMP.Core.Tests;

/// <summary>审计日志服务测试：时间范围/级别过滤、分页边界、统计与持久化完整性。</summary>
public class AuditLogServiceTests
{
    private sealed class FakeEventSource : IEventSource
    {
        public Task<IReadOnlyList<SystemEvent>> ReadRecentAsync(string channel, TimeSpan window, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SystemEvent>>([]);
    }

    private static (WempDbContext Db, AuditLogService Audits, LoggingService Service) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var factory = new TestDbFactory(connection);
        var audits = new AuditLogService(factory);
        var service = new LoggingService(factory, audits, new FakeEventSource(), new AnomalyDetector());
        return (db, audits, service);
    }

    private static AuditLog Log(string module, string action, string level, DateTime timestamp, string result = "success")
        => new()
        {
            Timestamp = timestamp,
            Module = module,
            Level = level,
            Action = action,
            Target = $"target-{action}",
            Message = $"消息 {action}",
            Result = result,
        };

    [Fact]
    public async Task QueryAsync_filters_by_module_level_keyword_and_time_range()
    {
        var (db, audits, _) = CreateHarness();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0);

        db.AuditLogs.AddRange(
            Log("A", "a.1", "info", baseTime),
            Log("A", "a.2", "error", baseTime.AddHours(1)),
            Log("B", "b.1", "error", baseTime.AddHours(2)),
            Log("B", "b.2", "info", baseTime.AddHours(3)));
        await db.SaveChangesAsync();

        // 模块过滤
        var (byModule, moduleTotal) = await audits.QueryAsync(module: "A");
        Assert.Equal(2, moduleTotal);

        // 级别过滤
        var (byLevel, levelTotal) = await audits.QueryAsync(level: "error");
        Assert.Equal(2, levelTotal);
        Assert.All(byLevel, a => Assert.Equal("error", a.Level));

        // 关键字命中 action / target / message
        var (byKeyword, keywordTotal) = await audits.QueryAsync(keyword: "b.1");
        Assert.Equal(1, keywordTotal);

        // 时间范围：> 12:00 且 <= 14:00 → a.2、b.1
        var (byRange, rangeTotal) = await audits.QueryAsync(since: baseTime.AddMinutes(1), until: baseTime.AddHours(2));
        Assert.Equal(2, rangeTotal);
        Assert.Contains(byRange, a => a.Action == "a.2");
        Assert.Contains(byRange, a => a.Action == "b.1");
    }

    [Fact]
    public async Task QueryAsync_clamps_page_size_and_paginates()
    {
        var (db, audits, _) = CreateHarness();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0);
        db.AuditLogs.AddRange(Enumerable.Range(0, 600)
            .Select(i => Log("A", $"a.{i}", "info", baseTime.AddMinutes(i))));
        await db.SaveChangesAsync();

        // pageSize 超过上限 500 → 钳制
        var (page1, total) = await audits.QueryAsync(pageSize: 9999);
        Assert.Equal(600, total);
        Assert.Equal(500, page1.Count);

        // 第 2 页取剩余 100 条
        var (page2, _) = await audits.QueryAsync(page: 2, pageSize: 500);
        Assert.Equal(100, page2.Count);
    }

    [Fact]
    public async Task QueryAsync_orders_by_timestamp_descending()
    {
        var (db, audits, _) = CreateHarness();
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0);
        db.AuditLogs.AddRange(
            Log("A", "first", "info", baseTime),
            Log("A", "last", "info", baseTime.AddHours(2)),
            Log("A", "middle", "info", baseTime.AddHours(1)));
        await db.SaveChangesAsync();

        var (items, _) = await audits.QueryAsync(module: "A");

        Assert.Equal(["last", "middle", "first"], items.Select(i => i.Action).ToList());
    }

    [Fact]
    public async Task WriteAsync_persists_complete_record()
    {
        var (db, audits, _) = CreateHarness();

        await audits.WriteAsync(
            module: "WEMP.Test",
            action: "test.write",
            target: "item-1",
            message: "完整记录",
            result: "failed",
            detailJson: "{\"key\":1}",
            level: "error");

        var saved = await db.AuditLogs.SingleAsync();
        Assert.Equal("WEMP.Test", saved.Module);
        Assert.Equal("test.write", saved.Action);
        Assert.Equal("item-1", saved.Target);
        Assert.Equal("完整记录", saved.Message);
        Assert.Equal("failed", saved.Result);
        Assert.Equal("error", saved.Level);
        Assert.Contains("key", saved.DetailJson);
        Assert.True(saved.Timestamp > DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public async Task GetStatisticsAsync_respects_since_and_counts_failed()
    {
        var (db, audits, _) = CreateHarness();

        // 24h 外失败（不计入 Failed24h）+ 24h 内失败 + 24h 内成功
        db.AuditLogs.AddRange(
            Log("A", "old.fail", "error", DateTime.Now.AddHours(-25), result: "failed"),
            Log("A", "recent.fail", "error", DateTime.Now.AddMinutes(-10), result: "failed"),
            Log("B", "recent.ok", "info", DateTime.Now.AddMinutes(-5)));
        await db.SaveChangesAsync();

        var all = await audits.GetStatisticsAsync();
        Assert.Equal(3, all.Total);
        Assert.Equal(1, all.Failed24h);

        // since 过滤后仅剩 2 条
        var recent = await audits.GetStatisticsAsync(since: DateTime.Now.AddHours(-1));
        Assert.Equal(2, recent.Total);
        Assert.Contains(recent.ByModule, m => m.Module == "A");
    }

    [Fact]
    public async Task LoggingService_write_and_query_error_level_roundtrip()
    {
        var (_, _, service) = CreateHarness();

        await service.WriteAuditAsync("WEMP.X", "x.crash", null, "崩溃", "failed", level: "error");
        await service.WriteAuditAsync("WEMP.X", "x.ok", null, "正常", "success", level: "info");

        var (errors, errorTotal) = await service.QueryAuditsAsync(level: "error");
        Assert.Equal(1, errorTotal);
        Assert.Equal("x.crash", errors[0].Action);

        var (all, total) = await service.QueryAuditsAsync();
        Assert.Equal(2, total);
    }
}
