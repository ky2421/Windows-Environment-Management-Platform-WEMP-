using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Services;
using WEMP.PackageManagement.Winget;

namespace WEMP.Core.Tests;

/// <summary>软件包管理服务测试：FakeProvider 隔离 winget 调用。</summary>
public class PackageManagerServiceTests
{
    private sealed class FakeProvider : IPackageProvider
    {
        public List<WingetPackage> Installed { get; } =
        [
            new("Google Chrome", "Google.Chrome", "136.0.0.0", null, "winget"),
            new("VS Code", "Microsoft.VisualStudioCode", "1.90.0", null, "winget"),
        ];

        public CommandResult InstallResult { get; set; } = new(0, "安装成功", 5);

        public List<string> InstalledIds { get; } = [];

        public Task<List<WingetPackage>> ListAsync(CancellationToken ct) => Task.FromResult(Installed);

        public Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken ct)
            => Task.FromResult<List<WingetPackage>>([]);

        public Task<CommandResult> InstallAsync(string packageId, CancellationToken ct)
        {
            InstalledIds.Add(packageId);
            return Task.FromResult(InstallResult);
        }

        public Task<CommandResult> UninstallAsync(string packageId, CancellationToken ct)
            => Task.FromResult(new CommandResult(0, "卸载成功", 3));

        public Task<CommandResult> UpgradeAllAsync(CancellationToken ct)
            => Task.FromResult(new CommandResult(0, "升级完成", 20));
    }

    private static (WempDbContext Db, PackageManagerService Service, FakeProvider Provider) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var provider = new FakeProvider();
        return (db, new PackageManagerService(new TestDbFactory(connection), provider), provider);
    }

    [Fact]
    public async Task SyncInstalled_replaces_winget_list()
    {
        var (db, service, _) = CreateHarness();

        var count = await service.SyncInstalledAsync();

        Assert.Equal(2, count);
        Assert.Equal(2, await db.InstalledSoftware.CountAsync(s => s.Source == "winget"));
        var chrome = await db.InstalledSoftware.FirstAsync(s => s.PackageId == "Google.Chrome");
        Assert.Equal("Google.Chrome", chrome.PackageId);
        Assert.Equal("Google Chrome", chrome.Name);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("software.sync", audit.Action);
    }

    [Fact]
    public async Task SyncInstalled_does_not_touch_other_sources()
    {
        var (db, service, _) = CreateHarness();
        db.InstalledSoftware.Add(new WEMP.Infrastructure.Data.Entities.InstalledSoftware
        {
            Name = "RegistryItem", Version = "1.0", Source = "registry", DetectedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        await service.SyncInstalledAsync();

        Assert.Equal(1, await db.InstalledSoftware.CountAsync(s => s.Source == "registry"));
        Assert.Equal(2, await db.InstalledSoftware.CountAsync(s => s.Source == "winget"));
    }

    [Fact]
    public async Task Install_writes_successful_operation()
    {
        var (db, service, provider) = CreateHarness();

        var operation = await service.InstallAsync("Google.Chrome");

        Assert.Equal("install", operation.Action);
        Assert.Equal("success", operation.Result);
        Assert.Equal(0, operation.ExitCode);
        Assert.NotNull(operation.FinishedAt);
        Assert.Contains("安装成功", operation.DetailJson);
        Assert.Equal(["Google.Chrome"], provider.InstalledIds);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("software.install", audit.Action);
    }

    [Fact]
    public async Task Install_failure_is_recorded()
    {
        var (db, service, provider) = CreateHarness();
        var wingetErrorCode = unchecked((int)0x8A15000B); // winget 管理员权限错误码
        provider.InstallResult = new CommandResult(wingetErrorCode, "需要管理员权限", 2);

        var operation = await service.InstallAsync("Google.Chrome");

        Assert.Equal("failed", operation.Result);
        Assert.Equal(wingetErrorCode, operation.ExitCode);
        Assert.Equal("failed", (await db.AuditLogs.SingleAsync()).Result);
    }

    [Fact]
    public async Task Uninstall_writes_operation()
    {
        var (db, service, _) = CreateHarness();

        var operation = await service.UninstallAsync("Google.Chrome");

        Assert.Equal("uninstall", operation.Action);
        Assert.Equal("success", operation.Result);
    }

    [Fact]
    public async Task GetInstalled_filters_by_keyword()
    {
        var (db, service, _) = CreateHarness();
        await service.SyncInstalledAsync();

        var chrome = await service.GetInstalledAsync("Chrome");
        Assert.Single(chrome);
        Assert.Equal("Google.Chrome", chrome[0].PackageId);

        var all = await service.GetInstalledAsync();
        Assert.Equal(2, all.Count);
    }
}
