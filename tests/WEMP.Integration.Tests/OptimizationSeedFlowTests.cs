using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Seeding;

namespace WEMP.Integration.Tests;

/// <summary>
/// 集成测试：知识库种子同步（嵌入资源 → 数据库）。
/// 验证嵌入的 optimization-items.json 可解析、字段完整、幂等重播。
/// </summary>
public class OptimizationSeedFlowTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private OptimizationSeedService CreateSeed() => new(_db.CreateFactory());

    [Fact]
    public async Task EnsureSeedAsync_播种全部知识库条目()
    {
        var added = await CreateSeed().EnsureSeedAsync();

        await using var context = _db.CreateContext();
        var count = await context.OptimizationItems.CountAsync();
        Assert.True(count >= 70, $"期望知识库条目 ≥ 70，实际 {count}");
        Assert.Equal(count, added);
    }

    [Fact]
    public async Task EnsureSeedAsync_字段完整性()
    {
        await CreateSeed().EnsureSeedAsync();

        await using var context = _db.CreateContext();
        var items = await context.OptimizationItems.AsNoTracking().ToListAsync();
        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Code), $"Code 为空：{item.Id}");
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"Name 为空：{item.Code}");
            Assert.False(string.IsNullOrWhiteSpace(item.Category), $"Category 为空：{item.Code}");
            Assert.False(string.IsNullOrWhiteSpace(item.TargetJson), $"TargetJson 为空：{item.Code}");
            Assert.False(string.IsNullOrWhiteSpace(item.RiskLevel), $"RiskLevel 为空：{item.Code}");
        });

        // 类别覆盖：注册表 / 服务 / 计划任务 / 可选功能等应同时存在
        var categories = items.Select(i => i.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("registry", categories);
        Assert.Contains("service", categories);
    }

    [Fact]
    public async Task EnsureSeedAsync_重复播种幂等()
    {
        var seed = CreateSeed();
        var first = await seed.EnsureSeedAsync();
        var second = await seed.EnsureSeedAsync();

        Assert.Equal(0, second);

        await using var context = _db.CreateContext();
        Assert.Equal(first, await context.OptimizationItems.CountAsync());
    }

    [Fact]
    public async Task EnsureSeedAsync_知识库版本号写入()
    {
        await CreateSeed().EnsureSeedAsync();

        await using var context = _db.CreateContext();
        var versions = await context.OptimizationItems.Select(i => i.KbVersion).Distinct().ToListAsync();
        Assert.NotEmpty(versions);
        Assert.All(versions, v => Assert.True(v >= 1));
    }
}
