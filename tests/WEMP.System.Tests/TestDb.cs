using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;

namespace WEMP.System.Tests;

/// <summary>
/// 系统测试共享基座：真实 SQLite 文件数据库 + 真实文件系统，
/// 模拟应用运行环境（持久化跨实例）。
/// </summary>
internal sealed class TestDb : IDisposable
{
    public TestDb()
    {
        Path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), $"wemp-sys-{Guid.NewGuid():N}.db");
        Connection = new SqliteConnection($"Data Source={Path}");
        Connection.Open();
    }

    public string Path { get; }

    public SqliteConnection Connection { get; }

    public TestDbFactory CreateFactory() => new(Connection);

    public WempDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(Connection).Options;
        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose()
    {
        Connection.Dispose();
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // 忽略偶发占用
        }
    }

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

/// <summary>假执行器：记录调用次数，不触碰真实系统。</summary>
internal sealed class FakeAction : IOptimizationAction
{
    public FakeAction(string itemType)
    {
        ItemType = itemType;
    }

    public string ItemType { get; }

    public bool SupportsBackup => true;

    public int ApplyCount { get; private set; }

    public int RestoreCount { get; private set; }

    /// <summary>应用时执行的额外行为（可抛出异常模拟失败）。</summary>
    public Action? ApplyBehavior { get; set; }

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
        => Task.FromResult<object?>(new Dictionary<string, int> { ["before"] = 1 });

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        ApplyCount++;
        ApplyBehavior?.Invoke();
        return Task.FromResult<object?>(new Dictionary<string, int> { ["after"] = 2 });
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        RestoreCount++;
        return Task.CompletedTask;
    }
}
