using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;

namespace WEMP.Integration.Tests;

/// <summary>
/// 集成测试：EF Core 迁移在真实 SQLite 文件上的完整性与幂等性。
/// 应用以 Migrate() 初始化数据库，测试验证迁移产物表齐全、可重复执行。
/// </summary>
public class DbMigrationTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private WempDbContext CreateMigratedContext()
    {
        var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(_db.Connection).Options;
        var context = new WempDbContext(options);
        context.Database.Migrate();
        return context;
    }

    [Fact]
    public void Migrate_创建全部实体表()
    {
        using var context = CreateMigratedContext();

        // 期望表名取自 EF 模型（与实体定义同步，防手写清单漂移）
        var expected = context.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(expected);

        foreach (var table in expected)
        {
            var sql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}'";
            var found = context.Database.SqlQueryRaw<string>(sql).Any();
            Assert.True(found, $"迁移后缺少表：{table}");
        }
    }

    [Fact]
    public void Migrate_记录迁移历史且可重复执行()
    {
        using (var context = CreateMigratedContext())
        {
            var applied = context.Database.GetAppliedMigrations().ToList();
            Assert.NotEmpty(applied);
        }

        // 二次迁移幂等：不抛异常
        using var again = CreateMigratedContext();
        Assert.True(again.Database.GetAppliedMigrations().Any());
    }

    [Fact]
    public async Task Migrate后_核心表可读写()
    {
        using var context = CreateMigratedContext();

        context.AppSettings.Add(new WEMP.Infrastructure.Data.Entities.AppSetting
        {
            Key = "it.probe",
            Value = "ok",
            Module = "IntegrationTests",
            UpdatedAt = DateTime.Now,
        });
        await context.SaveChangesAsync();

        var value = await context.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == "it.probe")
            .Select(s => s.Value)
            .SingleAsync();
        Assert.Equal("ok", value);
    }
}
