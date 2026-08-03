using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Logging.Services;

namespace WEMP.Core.Tests;

/// <summary>日志中心服务测试：内存库 + Fake 事件源隔离 Windows 事件日志。</summary>
public class LoggingServiceTests
{
    private sealed class FakeEventSource : IEventSource
    {
        public Dictionary<string, List<SystemEvent>> ByChannel { get; } = [];

        public Task<IReadOnlyList<SystemEvent>> ReadRecentAsync(string channel, TimeSpan window, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SystemEvent>>(ByChannel.GetValueOrDefault(channel) ?? []);
    }

    private static (WempDbContext Db, LoggingService Service, FakeEventSource Source) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var source = new FakeEventSource();
        var factory = new TestDbFactory(connection);
        var service = new LoggingService(factory, new AuditLogService(factory), source, new AnomalyDetector());
        return (db, service, source);
    }

    [Fact]
    public async Task QueryAudits_filters_by_module_level_and_keyword()
    {
        var (db, service, _) = CreateHarness();
        for (var i = 0; i < 10; i++)
        {
            await service.WriteAuditAsync("WEMP.Optimization", $"opt.apply.{i}", "item-x", "应用优化项", i % 2 == 0 ? "success" : "failed");
        }

        await service.WriteAuditAsync("WEMP.GameMode", "game.session.start", null, "游戏会话开始", "success");

        var (all, total) = await service.QueryAuditsAsync();
        Assert.Equal(11, total);

        var (opt, optTotal) = await service.QueryAuditsAsync(module: "WEMP.Optimization");
        Assert.Equal(10, optTotal);
        Assert.All(opt, a => Assert.Equal("WEMP.Optimization", a.Module));

        var (errors, errTotal) = await service.QueryAuditsAsync(level: "error");
        Assert.Equal(0, errTotal); // 全部 info 级

        // 关键字命中 action 与 message
        var (byAction, actionTotal) = await service.QueryAuditsAsync(keyword: "session");
        Assert.Equal(1, actionTotal);
        Assert.Equal("WEMP.GameMode", byAction[0].Module);
    }

    [Fact]
    public async Task QueryAudits_paginates()
    {
        var (_, service, _) = CreateHarness();
        for (var i = 0; i < 25; i++)
        {
            await service.WriteAuditAsync("WEMP.Logging", $"audit.write.{i}", null, $"消息 {i}", "success");
        }

        var (page1, total) = await service.QueryAuditsAsync(page: 1, pageSize: 10);
        Assert.Equal(25, total);
        Assert.Equal(10, page1.Count);

        var (page3, _) = await service.QueryAuditsAsync(page: 3, pageSize: 10);
        Assert.Equal(5, page3.Count);
    }

    [Fact]
    public async Task Statistics_groups_by_module_and_level()
    {
        var (_, service, _) = CreateHarness();
        await service.WriteAuditAsync("A", "a.1", null, null, "success");
        await service.WriteAuditAsync("A", "a.2", null, null, "failed");
        await service.WriteAuditAsync("B", "b.1", null, null, "success");

        var stats = await service.GetStatisticsAsync();

        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.ByModule.Count);
        Assert.Equal(2, stats.ByModule[0].Count);
        Assert.Equal(1, stats.Failed24h);
        Assert.Contains(stats.ByLevel, l => l.Level == "info" && l.Count == 3);
    }

    [Fact]
    public async Task AggregateEvents_deduplicates_by_time_provider_eventid()
    {
        var (db, service, source) = CreateHarness();
        var now = DateTime.Now;
        source.ByChannel["Application"] =
        [
            new SystemEvent { EventTime = now, Provider = "AppError", EventId = 1000, Level = 2, Message = "崩溃 1" },
            new SystemEvent { EventTime = now.AddSeconds(1), Provider = "AppError", EventId = 1000, Level = 2, Message = "崩溃 2" },
        ];

        var added1 = await service.AggregateEventsAsync(TimeSpan.FromHours(1));
        Assert.Equal(2, added1);

        // 再次聚合：同（时间, Provider, EventId）去重
        var added2 = await service.AggregateEventsAsync(TimeSpan.FromHours(1));
        Assert.Equal(0, added2);
        Assert.Equal(2, await db.SystemEvents.CountAsync());
    }

    [Fact]
    public async Task AggregateEvents_ignores_events_older_than_window()
    {
        var (db, service, source) = CreateHarness();
        source.ByChannel["Application"] =
        [
            new SystemEvent
            {
                EventTime = DateTime.Now.AddHours(-3),
                Provider = "OldProvider",
                EventId = 100,
                Level = 2,
                Message = "旧事件",
            },
        ];

        var added = await service.AggregateEventsAsync(TimeSpan.FromHours(1));
        Assert.Equal(1, added);
        Assert.Single(await db.SystemEvents.ToListAsync());
    }

    [Fact]
    public async Task RunAnomalyScan_detects_and_deduplicates()
    {
        var (db, service, source) = CreateHarness();
        var now = DateTime.Now;

        // 崩溃事件 → EVENT_CRASH
        source.ByChannel["Application"] =
        [
            new SystemEvent { EventTime = now, Provider = "AppError", EventId = 1000, Level = 2, Message = "崩溃" },
        ];

        // 错误风暴：同一 Provider 5 条错误
        for (var i = 0; i < 5; i++)
        {
            source.ByChannel["Application"].Add(new SystemEvent { EventTime = now.AddSeconds(i), Provider = "StormSource", EventId = 700 + i, Level = 2 });
        }

        await service.AggregateEventsAsync(TimeSpan.FromHours(24));
        var added1 = await service.RunAnomalyScanAsync(TimeSpan.FromHours(24));

        Assert.Equal(2, added1);
        var anomalies = await service.GetAnomaliesAsync();
        Assert.Equal(2, anomalies.Count);
        Assert.Contains(anomalies, a => a.RuleCode == "EVENT_CRASH");
        Assert.Contains(anomalies, a => a.RuleCode == "EVENT_ERROR_STORM");
        Assert.Equal("high", anomalies.First(a => a.RuleCode == "EVENT_CRASH").Severity);

        // 再次扫描：24h 内同规则+同标题去重
        var added2 = await service.RunAnomalyScanAsync(TimeSpan.FromHours(24));
        Assert.Equal(0, added2);
    }

    [Fact]
    public async Task RunAnomalyScan_detects_audit_failure_rate()
    {
        var (_, service, _) = CreateHarness();
        for (var i = 0; i < 7; i++)
        {
            await service.WriteAuditAsync("WEMP.X", $"x.op.{i}", null, null, i < 4 ? "failed" : "success");
        }

        var added = await service.RunAnomalyScanAsync(TimeSpan.FromHours(24));

        Assert.Equal(1, added);
        var anomalies = await service.GetAnomaliesAsync();
        var anomaly = Assert.Single(anomalies);
        Assert.Equal("AUDIT_FAILURE_RATE", anomaly.RuleCode);
        Assert.Contains("4/7", anomaly.Title);
    }

    [Fact]
    public async Task ResolveAnomaly_marks_resolved_and_hides_by_default()
    {
        var (_, service, source) = CreateHarness();
        source.ByChannel["Application"] =
        [
            new SystemEvent { EventTime = DateTime.Now, Provider = "AppError", EventId = 1001, Level = 2 },
        ];

        await service.AggregateEventsAsync(TimeSpan.FromHours(24));
        await service.RunAnomalyScanAsync(TimeSpan.FromHours(24));

        var anomaly = Assert.Single(await service.GetAnomaliesAsync());
        var resolved = await service.ResolveAnomalyAsync(anomaly.Id);
        Assert.True(resolved);

        Assert.Empty(await service.GetAnomaliesAsync());
        Assert.Single(await service.GetAnomaliesAsync(includeResolved: true));
    }

    [Fact]
    public async Task ResolveAnomaly_returns_false_for_missing()
    {
        var (_, service, _) = CreateHarness();
        Assert.False(await service.ResolveAnomalyAsync(999));
    }
}
