using Microsoft.EntityFrameworkCore;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;

namespace WEMP.Integration.Tests;

/// <summary>
/// 集成测试：知识库 → 服务 → 执行 → 审计 完整链路。
/// 用真实种子数据（72 条）驱动优化服务，假执行器隔离系统副作用；
/// 验证每条知识库条目均可解析目标、匹配执行器并产生审计记录。
/// </summary>
public class OptimizationFullFlowTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    /// <summary>为知识库中出现的每个类别注册假执行器，返回执行器集合。</summary>
    private async Task<(OptimizationService Service, Dictionary<string, FakeAction> Actions)> CreateHarnessAsync()
    {
        await new OptimizationSeedService(_db.CreateFactory()).EnsureSeedAsync();

        await using var context = _db.CreateContext();
        var categories = await context.OptimizationItems
            .AsNoTracking()
            .Select(i => i.Category)
            .Distinct()
            .ToListAsync();

        var actions = categories.ToDictionary(
            c => c.ToLowerInvariant(),
            c => new FakeAction(c.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        // game 类别复用 registry 执行器（工厂映射），保证键存在
        var factory = new OptimizationActionFactory(actions.Values);

        var service = new OptimizationService(_db.CreateFactory(), factory);
        return (service, actions);
    }

    [Fact]
    public async Task 一键优化_全量知识库条目成功并写入审计()
    {
        var (service, actions) = await CreateHarnessAsync();

        var result = await service.ApplyOneKeyAsync();

        Assert.True(result.SuccessCount > 0);
        Assert.Equal(0, result.FailureCount);
        // game 类别经工厂映射到 registry 执行器，其自身执行器不会被调用
        Assert.All(actions.Where(kv => kv.Key != "game"), kv => Assert.True(kv.Value.ApplyCount > 0));

        // 审计日志：每个成功条目一条
        await using var context = _db.CreateContext();
        var auditCount = await context.AuditLogs.CountAsync(l => l.Module == "Optimization" && l.Result == "success");
        Assert.Equal(result.SuccessCount, auditCount);
    }

    [Fact]
    public async Task 一键优化后_回滚全部并恢复()
    {
        var (service, actions) = await CreateHarnessAsync();

        await service.ApplyOneKeyAsync();
        var rollback = await service.RollbackAllAsync();

        Assert.True(rollback.SuccessCount > 0);
        Assert.Equal(rollback.Results.Count, rollback.SuccessCount + rollback.FailureCount);
        // 不可恢复条目（无备份）不回滚，故 RestoreCount ≤ ApplyCount
        Assert.All(actions.Values, a => Assert.True(a.RestoreCount <= a.ApplyCount));
    }

    [Fact]
    public async Task 单条目执行失败_不影响其他条目()
    {
        var (service, actions) = await CreateHarnessAsync();

        // 让 registry 类别模拟失败
        var registry = actions["registry"];
        registry.ThrowOnApply = true;

        var result = await service.ApplyOneKeyAsync();

        Assert.True(result.FailureCount >= 1);
        Assert.True(result.SuccessCount > 0);
        Assert.Contains(result.Results, r => !r.Success && r.Message.Contains("模拟应用失败"));
    }

    [Fact]
    public async Task 历史记录_按时间倒序返回()
    {
        var (service, _) = await CreateHarnessAsync();

        await service.ApplySelectedAsync(["svc.sysmain"]);
        await Task.Delay(20);
        await service.ApplySelectedAsync(["svc.wsearch"]);

        var history = await service.GetHistoryAsync(10);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].ExecutedAt >= history[1].ExecutedAt);
    }

    [Fact]
    public async Task 并发执行_多个任务同时操作数据库()
    {
        var (service, _) = await CreateHarnessAsync();

        var task1 = service.ApplyOneKeyAsync();
        var task2 = service.ApplyOneKeyAsync();
        var task3 = service.ApplySelectedAsync(["registry.fax"]);

        var results = await Task.WhenAll(task1, task2, task3);

        Assert.All(results, r => Assert.NotNull(r));
        // 并发下 DbContext 短生命周期工厂不应抛出冲突异常
        await using var context = _db.CreateContext();
        var records = await context.OptimizationRecords.CountAsync();
        Assert.True(records > 0);
    }
}
