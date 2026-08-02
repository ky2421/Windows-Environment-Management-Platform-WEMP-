using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Logging.Services;

// 1. 真实 Windows 事件日志读取（只读）
var source = new WindowsEventLogSource();
foreach (var channel in new[] { "Application", "System" })
{
    var events = await source.ReadRecentAsync(channel, TimeSpan.FromHours(24));
    var errors = events.Count(e => e.Level <= 2);
    Console.WriteLine($"事件日志 [{channel}] 最近 24h：{events.Count} 条（错误/严重 {errors} 条）");
}

// 2. 临时库全流程：审计写入/查询/统计 + 聚合 + 扫描 + 处置
var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(connection).Options;
var db = new WempDbContext(options);
db.Database.EnsureCreated();

var service = new LoggingService(db, new AuditLogService(db), source, new AnomalyDetector());

await service.WriteAuditAsync("WEMP.Logging", "logging.probe.start", null, "探针写入", "success");
await service.WriteAuditAsync("WEMP.Logging", "logging.probe.end", null, "探针结束", "failed");

var (items, total) = await service.QueryAuditsAsync(module: "WEMP.Logging");
Console.WriteLine($"审计查询：{total} 条（关键字 '探针' 命中 {items.Count}）");
var (_, kwTotal) = await service.QueryAuditsAsync(keyword: "探针");
Console.WriteLine($"关键字过滤：{kwTotal} 条");

var stats = await service.GetStatisticsAsync();
Console.WriteLine($"统计：总 {stats.Total} | 按模块 {string.Join(", ", stats.ByModule.Select(m => $"{m.Module}={m.Count}"))} | 24h 失败 {stats.Failed24h}");

var added = await service.AggregateEventsAsync(TimeSpan.FromHours(24));
Console.WriteLine($"事件聚合：新增 {added} 条");
var storedEvents = await service.GetSystemEventsAsync(limit: 10);
Console.WriteLine($"已聚合事件示例：{storedEvents.Count} 条（最近：{storedEvents.FirstOrDefault()?.Provider}/{storedEvents.FirstOrDefault()?.EventId}）");

var scanned = await service.RunAnomalyScanAsync(TimeSpan.FromHours(24));
var anomalies = await service.GetAnomaliesAsync();
Console.WriteLine($"异常扫描：新增 {scanned} 条，当前未解决 {anomalies.Count} 条");
foreach (var a in anomalies)
{
    Console.WriteLine($"  [{a.Severity}] {a.RuleCode} {a.Title}");
    _ = await service.ResolveAnomalyAsync(a.Id);
}

Console.WriteLine($"处置后未解决异常：{(await service.GetAnomaliesAsync()).Count} 条");

// 3. 真实库只读查询（不写入）
var realOptions = new DbContextOptionsBuilder<WempDbContext>()
    .UseSqlite(WempDatabase.CreateConnectionString())
    .Options;
using var realDb = new WempDbContext(realOptions);
var realService = new LoggingService(realDb, new AuditLogService(realDb), source, new AnomalyDetector());
var realStats = await realService.GetStatisticsAsync();
var realEvents = await realService.GetSystemEventsAsync(limit: 1);
Console.WriteLine($"真实库：audit_logs {realStats.Total} 条（24h 失败 {realStats.Failed24h}），system_events {realDb.SystemEvents.Count()} 条");
