using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;
using WEMP.Optimization.Services;

namespace WEMP.Core.Tests;

/// <summary>优化回滚测试：失败隔离、审计日志与未知条目处理。</summary>
public class OptimizationRollbackTests
{
    private sealed class FakeAction : IOptimizationAction
    {
        public FakeAction(string itemType)
        {
            ItemType = itemType;
        }

        public string ItemType { get; }

        public bool SupportsBackup { get; set; } = true;

        public bool ThrowOnApply { get; set; }

        public bool ThrowOnRestore { get; set; }

        /// <summary>按调用次数产出不同备份值，用于验证回滚的是指定记录的快照。</summary>
        public Func<object?>? BackupValueFactory { get; set; }

        /// <summary>最近一次 RestoreAsync 收到的备份值。</summary>
        public object? LastRestoredBackup { get; private set; }

        public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
            => Task.FromResult<object?>(BackupValueFactory?.Invoke()
                ?? (ItemType == "memory"
                    // memory 类别回滚时按 List<ProcessBackup> 反序列化，需返回可往返的类型
                    ? new List<ProcessBackup> { new("testproc", "C:\\test.exe") }
                    : new RegistryBackup(true, "String", "original")));

        public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("模拟应用失败");
            }

            return Task.FromResult<object?>(new Dictionary<string, int> { ["after"] = 2 });
        }

        public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        {
            if (ThrowOnRestore)
            {
                throw new InvalidOperationException("模拟回滚失败");
            }

            LastRestoredBackup = backup;
            return Task.CompletedTask;
        }
    }

    private sealed record ItemSpec(string Code, string Category, bool Recoverable = true, string? TargetJson = null);

    private static async Task<(WempDbContext Db, OptimizationService Service)> CreateHarnessAsync(
        IEnumerable<ItemSpec>? items = null,
        Action<FakeAction>? configureMemory = null,
        Action<FakeAction>? configureRegistry = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var registryAction = new FakeAction("registry");
        var memoryAction = new FakeAction("memory");
        configureRegistry?.Invoke(registryAction);
        configureMemory?.Invoke(memoryAction);

        var service = new OptimizationService(new TestDbFactory(connection), new OptimizationActionFactory([registryAction, memoryAction]));

        foreach (var spec in items ?? [new ItemSpec("t.ok", "registry"), new ItemSpec("t.fail", "memory")])
        {
            db.OptimizationItems.Add(new OptimizationItem
            {
                Code = spec.Code,
                Category = spec.Category,
                Name = $"测试项 {spec.Code}",
                Recommendation = "required",
                IsRecoverable = spec.Recoverable,
                TargetJson = spec.TargetJson ?? "{}",
                Enabled = true,
                SortOrder = 1,
                KbVersion = 1,
            });
        }

        await db.SaveChangesAsync();
        return (db, service);
    }

    [Fact]
    public async Task Rollback_isolates_failures_per_item()
    {
        var (db, service) = await CreateHarnessAsync(configureMemory: a => a.ThrowOnRestore = true);
        await service.ApplySelectedAsync(["t.ok", "t.fail"]);

        var result = await service.RollbackAsync(["t.ok", "t.fail"]);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.Results.First(r => r.ItemCode == "t.ok").Success);
        Assert.False(result.Results.First(r => r.ItemCode == "t.fail").Success);
        Assert.Contains("模拟回滚失败", result.Results.First(r => r.ItemCode == "t.fail").Message);

        // 每个条目独立落库：2 apply + 2 rollback
        Assert.Equal(4, await db.OptimizationRecords.CountAsync());
        var failedRollback = await db.OptimizationRecords.FirstAsync(r => r.ItemCode == "t.fail" && r.Action == "rollback");
        Assert.Equal("failed", failedRollback.Result);
        Assert.Contains("模拟回滚失败", failedRollback.Detail);
    }

    [Fact]
    public async Task Rollback_writes_success_audit_log()
    {
        var (db, service) = await CreateHarnessAsync();
        await service.ApplySelectedAsync(["t.ok"]);
        await service.RollbackAsync(["t.ok"]);

        var audits = await db.AuditLogs.Where(a => a.Action == "optimize.rollback").ToListAsync();
        var audit = Assert.Single(audits);
        Assert.Equal("success", audit.Result);
        Assert.Equal("info", audit.Level);
        Assert.Equal("t.ok", audit.Target);
    }

    [Fact]
    public async Task Rollback_writes_error_audit_log_on_failure()
    {
        var (db, service) = await CreateHarnessAsync(configureMemory: a => a.ThrowOnRestore = true);
        await service.ApplySelectedAsync(["t.fail"]);
        await service.RollbackAsync(["t.fail"]);

        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "optimize.rollback");
        Assert.Equal("failed", audit.Result);
        Assert.Equal("error", audit.Level);
        Assert.NotNull(audit.Message);
    }

    [Fact]
    public async Task Rollback_unknown_code_reports_failure()
    {
        var (_, service) = await CreateHarnessAsync();

        var result = await service.RollbackAsync(["no.such.code"]);

        Assert.False(result.Results.Single().Success);
        Assert.Contains("知识库中不存在该条目", result.Results.Single().Message);
        Assert.Equal("rollback", result.Results.Single().Action);
    }

    [Fact]
    public async Task Apply_failure_writes_error_level_audit()
    {
        var (db, service) = await CreateHarnessAsync(configureMemory: a => a.ThrowOnApply = true);

        var result = await service.ApplySelectedAsync(["t.fail"]);

        Assert.False(result.Results.Single().Success);
        Assert.Contains("模拟应用失败", result.Results.Single().Message);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("error", audit.Level);
        Assert.Equal("failed", audit.Result);
        Assert.Equal("optimize.apply", audit.Action);
    }

    [Fact]
    public async Task ApplySelected_throws_when_target_json_invalid()
    {
        var (db, service) = await CreateHarnessAsync(
            items: [new ItemSpec("t.bad", "registry", TargetJson: "not-json")]);

        // 非法 JSON 在 try 之外直接抛出 JsonException，不落库
        var ex = await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => service.ApplySelectedAsync(["t.bad"]));
        Assert.Contains("invalid JSON", ex.Message);
        Assert.Equal(0, await db.OptimizationRecords.CountAsync());
    }

    [Fact]
    public async Task ApplySelected_throws_when_target_json_is_null_literal()
    {
        var (db, service) = await CreateHarnessAsync(
            items: [new ItemSpec("t.bad", "registry", TargetJson: "null")]);

        // "null" 反序列化结果为 null → 缺少目标参数
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplySelectedAsync(["t.bad"]));
        Assert.Contains("缺少目标参数", ex.Message);
        Assert.Equal(0, await db.OptimizationRecords.CountAsync());
    }

    [Fact]
    public async Task Rollback_after_non_recoverable_apply_reports_no_backup()
    {
        var (db, service) = await CreateHarnessAsync(
            items: [new ItemSpec("t.norecover", "registry", Recoverable: false)]);

        var apply = await service.ApplySelectedAsync(["t.norecover"]);
        Assert.True(apply.Results.Single().Success);
        Assert.Null((await db.OptimizationRecords.SingleAsync()).BeforeJson);

        var result = await service.RollbackAsync(["t.norecover"]);

        Assert.False(result.Results.Single().Success);
        Assert.Contains("无可用备份", result.Results.Single().Message);
    }

    [Fact]
    public async Task Rollback_record_restores_that_snapshot()
    {
        var values = new Queue<string>(["v1", "v2"]);
        FakeAction? captured = null;
        var (db, service) = await CreateHarnessAsync(
            items: [new ItemSpec("t.ok", "registry")],
            configureRegistry: a =>
            {
                a.BackupValueFactory = () => new RegistryBackup(true, "String", values.Dequeue());
                captured = a;
            });

        await service.ApplySelectedAsync(["t.ok"]);
        await service.ApplySelectedAsync(["t.ok"]);
        Assert.Equal(2, await db.OptimizationRecords.CountAsync(r => r.Action == "apply"));

        // 回滚第一条记录：恢复的应是其备份快照 v1，而非最近一次的 v2
        var firstRecord = await db.OptimizationRecords.OrderBy(r => r.Id).FirstAsync();
        var result = await service.RollbackRecordAsync(firstRecord.Id);

        Assert.True(result.Results.Single().Success);
        var restored = Assert.IsType<RegistryBackup>(captured!.LastRestoredBackup);
        // 快照经 JSON 序列化往返后 Data 是 JsonElement
        var data = Assert.IsType<System.Text.Json.JsonElement>(restored.Data);
        Assert.Equal("v1", data.GetString());

        // 落库一条 trigger=history 的回滚记录
        var rollback = await db.OptimizationRecords.SingleAsync(r => r.Action == "rollback");
        Assert.Equal("history", rollback.Trigger);
        Assert.Equal("success", rollback.Result);
        Assert.Equal("t.ok", rollback.ItemCode);
    }

    [Fact]
    public async Task Rollback_record_rejects_invalid_records()
    {
        var (db, service) = await CreateHarnessAsync();
        await service.ApplySelectedAsync(["t.ok"]);

        // 不存在的记录 Id
        var missing = await service.RollbackRecordAsync(999_999);
        Assert.False(missing.Results.Single().Success);

        // 回滚记录本身（无应用快照）不可再回滚
        var applyRecord = await db.OptimizationRecords.SingleAsync(r => r.Action == "apply");
        await service.RollbackRecordAsync(applyRecord.Id);
        var rollbackRecord = await db.OptimizationRecords.SingleAsync(r => r.Action == "rollback");

        var onRollback = await service.RollbackRecordAsync(rollbackRecord.Id);
        Assert.False(onRollback.Results.Single().Success);
        Assert.Contains("不支持", onRollback.Results.Single().Message);
    }
}
