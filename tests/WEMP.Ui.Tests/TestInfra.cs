using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.DevEnvironment.Services;
using WEMP.GameMode.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Logging.Services;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Models;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Winget;
using WEMP.SystemInfo.Detection;
using WEMP.SystemInfo.Models;
using WEMP.SystemInfo.Persistence;

namespace WEMP.Ui.Tests;

/// <summary>
/// 共享测试基础设施：内存 SQLite 上下文工厂与全部测试替身（隔离系统副作用）。
/// </summary>
internal static class TestInfra
{
    /// <summary>共享内存 SQLite 连接、每次新建上下文的工厂（模拟应用短生命周期上下文）。</summary>
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

    // ---- 游戏模式：状态切换替身 ----

    internal sealed class FakeSwitcher : IGameStateSwitcher
    {
        public Task<GameStateSnapshot> EnterGameModeAsync(CancellationToken cancellationToken)
            => Task.FromResult(new GameStateSnapshot("balanced", []));

        public Task RestoreAsync(GameStateSnapshot snapshot, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // ---- 优化：动作替身（只计数，不改系统） ----

    internal sealed class FakeAction : IOptimizationAction
    {
        public FakeAction(string itemType = "registry")
        {
            ItemType = itemType;
        }

        public string ItemType { get; }

        public bool SupportsBackup => true;

        public int ApplyCount { get; private set; }

        public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
            => Task.FromResult<object?>(new Dictionary<string, int>());

        public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.FromResult<object?>(new Dictionary<string, int>());
        }

        public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // ---- 软件包管理：winget 提供者替身 ----

    internal sealed class FakeProvider : IPackageProvider
    {
        public Task<List<WingetPackage>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(new List<WingetPackage>());

        public Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken)
            => Task.FromResult(new List<WingetPackage>());

        public Task<CommandResult> InstallAsync(string packageId, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));

        public Task<CommandResult> UninstallAsync(string packageId, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));

        public Task<CommandResult> UpgradeAllAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));
    }

    // ---- 开发环境：工具安装 / 环境变量 / 配置文件 / 校验 四替身 ----

    internal sealed class FakeInstaller : IToolInstaller
    {
        public Task<ToolInstallResult> InstallAsync(string toolName, string? version, bool optional, CancellationToken cancellationToken = default)
            => Task.FromResult(new ToolInstallResult(true, "ok", $"已安装 {toolName}"));

        public string? ResolvePackageId(string toolName) => "test.package";
    }

    internal sealed class FakeEnvVars : IEnvironmentVariableService
    {
        public string? GetValue(string name, string scope = "user") => null;

        public string? SetValue(string name, string? value, string scope = "user")
        {
            var previous = GetValue(name, scope);
            return previous;
        }
    }

    internal sealed class FakeConfigWriter : IConfigFileWriter
    {
        public ConfigWriteResult Write(string path, IReadOnlyDictionary<string, string> values, string strategy)
            => new(path, true, values.Count);
    }

    internal sealed class FakeValidator : IToolValidator
    {
        public Task<ValidationResult> ValidateAsync(string command, string? expected, CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationResult(true, "ok", null));
    }

    // ---- 日志：事件源 / 异常检测替身 ----

    internal sealed class FakeEventSource : IEventSource
    {
        public Task<IReadOnlyList<SystemEvent>> ReadRecentAsync(string channel, TimeSpan window, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemEvent>>([]);
    }

    internal sealed class FakeAnomalyDetector : IAnomalyDetector
    {
        public IReadOnlyList<LogAnomaly> Detect(IReadOnlyList<SystemEvent> events, IReadOnlyList<AuditLog> audits)
            => [];
    }

    // ---- 系统信息：检测 / 快照仓库替身 ----

    internal sealed class FakeSystemInfoProvider : ISystemInfoProvider
    {
        public Task<SystemInfoSnapshot> DetectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemInfoSnapshot());
    }

    internal sealed class FakeSnapshotRepository : ISnapshotRepository
    {
        public Task<long> SaveAsync(SystemInfoSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task<IReadOnlyList<SystemSnapshot>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemSnapshot>>([]);
    }
}
