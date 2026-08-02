using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Seeding;

namespace WEMP.Core.Tests;

/// <summary>优化知识库种子同步测试。</summary>
public class OptimizationSeedTests
{
    private static WempDbContext CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task EnsureSeedAsync_inserts_knowledge_base()
    {
        using var db = CreateInMemoryDb();
        var seed = new OptimizationSeedService(db);

        var added = await seed.EnsureSeedAsync();

        Assert.Equal(12, added);
        Assert.Equal(12, await db.OptimizationItems.CountAsync());
        Assert.All(await db.OptimizationItems.ToListAsync(), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Code));
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.NotNull(item.TargetJson);
            Assert.Equal(1, item.KbVersion);
        });
    }

    [Fact]
    public async Task EnsureSeedAsync_is_idempotent()
    {
        using var db = CreateInMemoryDb();
        var seed = new OptimizationSeedService(db);

        await seed.EnsureSeedAsync();
        var second = await seed.EnsureSeedAsync();

        Assert.Equal(0, second);
        Assert.Equal(12, await db.OptimizationItems.CountAsync());
    }

    [Fact]
    public async Task Knowledge_base_covers_all_categories()
    {
        using var db = CreateInMemoryDb();
        var seed = new OptimizationSeedService(db);
        await seed.EnsureSeedAsync();

        var categories = (await db.OptimizationItems
                .Select(i => i.Category)
                .Distinct()
                .ToListAsync())
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(["disk", "game", "memory", "network", "power", "registry", "service", "startup"], categories);
    }
}
