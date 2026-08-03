using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Seeding;

namespace WEMP.Core.Tests;

/// <summary>优化知识库种子同步测试。</summary>
public class OptimizationSeedTests
{
    private static (WempDbContext Db, TestDbFactory Factory) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return (db, new TestDbFactory(connection));
    }

    [Fact]
    public async Task EnsureSeedAsync_inserts_knowledge_base()
    {
        var (db, factory) = CreateInMemoryDb();
        var seed = new OptimizationSeedService(factory);

        var added = await seed.EnsureSeedAsync();

        Assert.Equal(72, added);
        Assert.Equal(72, await db.OptimizationItems.CountAsync());
        Assert.All(await db.OptimizationItems.ToListAsync(), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Code));
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.NotNull(item.TargetJson);
            Assert.Equal(7, item.KbVersion);
            Assert.Contains(item.RiskLevel, new[] { "safe", "advanced", "aggressive" });
        });
    }

    [Fact]
    public async Task EnsureSeedAsync_is_idempotent()
    {
        var (db, factory) = CreateInMemoryDb();
        var seed = new OptimizationSeedService(factory);

        await seed.EnsureSeedAsync();
        var second = await seed.EnsureSeedAsync();

        Assert.Equal(0, second);
        Assert.Equal(72, await db.OptimizationItems.CountAsync());
    }

    [Fact]
    public async Task Knowledge_base_covers_all_categories()
    {
        var (db, factory) = CreateInMemoryDb();
        var seed = new OptimizationSeedService(factory);
        await seed.EnsureSeedAsync();

        var categories = (await db.OptimizationItems
                .Select(i => i.Category)
                .Distinct()
                .ToListAsync())
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(
            ["appx", "background", "bios", "device", "disk", "game", "gpu", "guide", "hags", "memory", "network", "pagefile", "power", "registry", "scheduled-task", "service", "startup", "timer", "visual", "windows-feature"],
            categories);
    }
}
