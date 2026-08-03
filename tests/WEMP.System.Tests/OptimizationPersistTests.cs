using Microsoft.EntityFrameworkCore;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;

namespace WEMP.System.Tests;

/// <summary>
/// 系统测试：Windows 优化执行完整流程（持久化视角）。
/// 真实 SQLite 文件数据库模拟应用重启：种子 → 执行 → 历史持久 → 新实例读取 → 回滚。
/// </summary>
public class OptimizationPersistTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private (OptimizationService Service, Dictionary<string, FakeAction> Actions) CreateHarness(
        Func<IEnumerable<FakeAction>>? actionsFactory = null)
    {
        var actions = actionsFactory is not null
            ? actionsFactory().ToDictionary(a => a.ItemType, StringComparer.OrdinalIgnoreCase)
            : DefaultActions();
        var factory = new OptimizationActionFactory(actions.Values);
        return (new OptimizationService(_db.CreateFactory(), factory), actions);
    }

    private Dictionary<string, FakeAction> DefaultActions()
    {
        // 覆盖知识库 seed 的全部类别；game 类别经工厂映射到 registry 执行器
        return new[]
        {
            "appx", "background", "bios", "device", "disk", "game", "gpu", "guide", "hags",
            "memory", "network", "pagefile", "power", "registry", "scheduled-task", "service",
            "startup", "timer", "visual", "windows-feature",
        }.ToDictionary(c => c, c => new FakeAction(c));
    }

    [Fact]
    public async Task 一键优化_重启后历史记录持久化()
    {
        await new OptimizationSeedService(_db.CreateFactory()).EnsureSeedAsync();
        var (service, _) = CreateHarness();

        var result = await service.ApplyOneKeyAsync();
        Assert.True(result.SuccessCount > 0);

        // 模拟应用重启：新服务实例（同一数据库文件）
        var (restarted, actions) = CreateHarness();

        var history = await restarted.GetHistoryAsync(100);
        Assert.Equal(result.SuccessCount, history.Count);
        Assert.All(history, r => Assert.Equal("success", r.Result));
        Assert.All(actions.Values, a => Assert.Equal(0, a.ApplyCount)); // 新实例未执行
    }

    [Fact]
    public async Task 执行后回滚_状态恢复为未优化()
    {
        await new OptimizationSeedService(_db.CreateFactory()).EnsureSeedAsync();
        var (service, actions) = CreateHarness();

        await service.ApplyOneKeyAsync();
        var rollback = await service.RollbackAllAsync();

        Assert.True(rollback.SuccessCount > 0);
        Assert.Equal(rollback.Results.Count, rollback.SuccessCount + rollback.FailureCount);
        // 不可恢复条目（无备份）不回滚，故 RestoreCount ≤ ApplyCount
        Assert.All(actions.Values, a => Assert.True(a.RestoreCount <= a.ApplyCount));

        // 历史记录标记为已回滚（失败回滚同样写记录，只验证成功数被覆盖）
        await using var context = _db.CreateContext();
        var rolledBack = await context.OptimizationRecords.CountAsync(r => r.Action == "rollback");
        Assert.True(rolledBack >= rollback.SuccessCount);
    }

    [Fact]
    public async Task 单项失败_记录失败原因且可继续优化()
    {
        await new OptimizationSeedService(_db.CreateFactory()).EnsureSeedAsync();
        var (service, actions) = CreateHarness();
        actions["registry"].ApplyBehavior = () => throw new InvalidOperationException("模拟服务启动失败");

        var result = await service.ApplyOneKeyAsync();

        Assert.True(result.FailureCount >= 1);
        Assert.Contains(result.Results, r => !r.Success && r.Message.Contains("模拟服务启动失败"));

        // 失败后再次执行（registry 之外的条目仍可成功）
        actions["registry"].ApplyBehavior = null;
        var retry = await service.ApplyOneKeyAsync();
        Assert.True(retry.SuccessCount >= result.SuccessCount);
    }
}
