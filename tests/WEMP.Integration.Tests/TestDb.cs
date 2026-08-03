using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;

namespace WEMP.Integration.Tests;

/// <summary>
/// 集成测试共享基座：真实 SQLite 文件数据库（模拟应用运行环境），
/// 每次测试独立文件，用后删除。
/// </summary>
internal sealed class TestDb : IDisposable
{
    public TestDb()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wemp-it-{Guid.NewGuid():N}.db");
        Connection = new SqliteConnection($"Data Source={Path}");
        Connection.Open();
    }

    public string Path { get; }

    public SqliteConnection Connection { get; }

    public WempDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(Connection).Options;
        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public TestDbFactory CreateFactory() => new(Connection);

    public void Dispose()
    {
        Connection.Dispose();
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // 测试进程偶发占用，忽略
        }
    }

    /// <summary>共享同一连接、每次新建上下文的工厂。</summary>
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
}

/// <summary>假执行器：记录调用次数，模拟成功/失败，不触碰真实系统。</summary>
internal sealed class FakeAction : IOptimizationAction
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

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
        => Task.FromResult<object?>(new Dictionary<string, int> { ["before"] = 1 });

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
