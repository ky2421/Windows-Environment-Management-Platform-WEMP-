using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;

namespace WEMP.Core.Tests;

/// <summary>
/// 测试用 DbContext 工厂：所有上下文共享同一内存 SQLite 连接，
/// 模拟应用运行时的短生命周期上下文（每次操作独立实例，数据共享）。
/// </summary>
internal sealed class TestDbFactory(SqliteConnection connection) : IDbContextFactory<WempDbContext>
{
    public WempDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(connection).Options;
        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public Task<WempDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
