using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;
using WEMP.Optimization.Services;

namespace WEMP.Core.Tests;

/// <summary>优化服务逻辑测试：使用 FakeAction 替身隔离系统副作用。</summary>
public class OptimizationServiceTests
{
    private sealed class FakeAction : IOptimizationAction
    {
        public FakeAction(string itemType, bool supportsBackup = true)
        {
            ItemType = itemType;
            SupportsBackup = supportsBackup;
        }

        public string ItemType { get; }

        public bool SupportsBackup { get; }

        public bool ThrowOnApply { get; set; }

        public bool ThrowOnRestore { get; set; }

        public int ApplyCount { get; private set; }

        public int RestoreCount { get; private set; }

        public object? BackupData { get; set; } = new Dictionary<string, int> { ["before"] = 1 };

        public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
            => Task.FromResult(BackupData);

        public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        {
            ApplyCount++;
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("模拟应用失败");
            }

            return Task.FromResult<object?>(new Dictionary<string, int> { ["after"] = 2 });
        }

        public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        {
            RestoreCount++;
            if (ThrowOnRestore)
            {
                throw new InvalidOperationException("模拟回滚失败");
            }

            return Task.CompletedTask;
        }
    }

    private sealed record TestHarness(WempDbContext Db, OptimizationService Service, FakeAction Action);

    private static TestHarness CreateHarness(bool actionSupportsBackup = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var registryAction = new FakeAction("registry", actionSupportsBackup);
        var memoryAction = new FakeAction("memory", actionSupportsBackup)
        {
            // 用于验证失败隔离：memory 类别固定模拟失败
            ThrowOnApply = true,
        };
        var factory = new OptimizationActionFactory([registryAction, memoryAction]);
        return new TestHarness(db, new OptimizationService(db, factory), registryAction);
    }

    private static async Task<OptimizationItem> AddItemAsync(
        WempDbContext db, string code, string category = "registry", bool recoverable = true)
    {
        var item = new OptimizationItem
        {
            Code = code,
            Category = category,
            Name = $"测试项 {code}",
            Recommendation = "required",
            IsRecoverable = recoverable,
            TargetJson = "{}",
            Enabled = true,
            SortOrder = 1,
            KbVersion = 1,
        };

        db.OptimizationItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task ApplySelected_writes_record_and_audit_log()
    {
        var (db, service, action) = CreateHarness();
        await AddItemAsync(db, "test.success");

        var result = await service.ApplySelectedAsync(["test.success"]);

        Assert.True(result.Results.Single().Success);
        Assert.Equal(1, action.ApplyCount);

        var record = await db.OptimizationRecords.SingleAsync();
        Assert.Equal("test.success", record.ItemCode);
        Assert.Equal("apply", record.Action);
        Assert.Equal("success", record.Result);
        Assert.NotNull(record.BeforeJson);
        Assert.Contains("before", record.BeforeJson);
        Assert.Contains("after", record.AfterJson);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("Optimization", audit.Module);
        Assert.Equal("optimize.apply", audit.Action);
        Assert.Equal("success", audit.Result);
    }

    [Fact]
    public async Task ApplySelected_isolates_failures_per_item()
    {
        var (db, service, action) = CreateHarness();
        await AddItemAsync(db, "test.ok", category: "registry");
        await AddItemAsync(db, "test.fail", category: "memory");

        var result = await service.ApplySelectedAsync(["test.ok", "test.fail"]);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains("模拟应用失败", result.Results.First(r => r.ItemCode == "test.fail").Message);

        Assert.Equal(2, await db.OptimizationRecords.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync());
        var failed = await db.OptimizationRecords.FirstAsync(r => r.ItemCode == "test.fail");
        Assert.Equal("failed", failed.Result);
        Assert.NotNull(failed.Detail);
    }

    [Fact]
    public async Task Rollback_restores_from_latest_backup()
    {
        var (db, service, action) = CreateHarness();
        await AddItemAsync(db, "test.rollback");

        await service.ApplySelectedAsync(["test.rollback"]);
        var result = await service.RollbackAsync(["test.rollback"]);

        Assert.True(result.Results.Single().Success);
        Assert.Equal(1, action.RestoreCount);

        var rollback = await db.OptimizationRecords.FirstAsync(r => r.Action == "rollback");
        Assert.Equal("success", rollback.Result);
        Assert.Equal(2, await db.OptimizationRecords.CountAsync());
    }

    [Fact]
    public async Task Rollback_without_backup_reports_failure()
    {
        var (db, service, action) = CreateHarness(actionSupportsBackup: false);
        var item = await AddItemAsync(db, "test.nobackup", recoverable: false);
        item.IsRecoverable = false;
        await db.SaveChangesAsync();

        var apply = await service.ApplySelectedAsync(["test.nobackup"]);
        Assert.True(apply.Results.Single().Success);

        // 不可回滚项：apply 成功但不写备份
        var record = await db.OptimizationRecords.SingleAsync();
        Assert.Null(record.BeforeJson);

        var result = await service.RollbackAsync(["test.nobackup"]);
        Assert.False(result.Results.Single().Success);
        Assert.Contains("无可用备份", result.Results.Single().Message);
    }

    [Fact]
    public async Task ApplyOneKey_runs_all_enabled_items()
    {
        var (db, service, action) = CreateHarness();
        await AddItemAsync(db, "test.one");
        await AddItemAsync(db, "test.two");
        var disabled = await AddItemAsync(db, "test.disabled");
        disabled.Enabled = false;
        await db.SaveChangesAsync();

        var result = await service.ApplyOneKeyAsync();

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task RollbackAll_covers_only_applied_items()
    {
        var (db, service, action) = CreateHarness();
        await AddItemAsync(db, "test.a");
        await AddItemAsync(db, "test.b");

        await service.ApplySelectedAsync(["test.a"]);
        var result = await service.RollbackAllAsync();

        Assert.Single(result.Results);
        Assert.Equal("test.a", result.Results.Single().ItemCode);
        Assert.True(result.Results.Single().Success);
    }
}
