using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.PackageManagement.Services;

namespace WEMP.Core.Tests;

/// <summary>软件分组服务测试：FakePackageManager 记录安装调用。</summary>
public class SoftwareGroupServiceTests
{
    private sealed class FakePackageManager : IPackageManagerService
    {
        public List<string> InstalledIds { get; } = [];

        public Task<int> SyncInstalledAsync(CancellationToken ct = default) => Task.FromResult(0);

        public Task<IReadOnlyList<InstalledSoftware>> GetInstalledAsync(string? search = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstalledSoftware>>([]);

        public Task<IReadOnlyList<WEMP.PackageManagement.Models.WingetPackage>> GetUpgradableAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WEMP.PackageManagement.Models.WingetPackage>>([]);

        public Task<PackageOperation> InstallAsync(string packageId, string? version = null, CancellationToken ct = default)
        {
            InstalledIds.Add(packageId);
            return Task.FromResult(new PackageOperation { PackageId = packageId, Result = "success" });
        }

        public Task<PackageOperation> UninstallAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult(new PackageOperation { PackageId = packageId, Result = "success" });

        public Task<PackageOperation> UpgradeAllAsync(CancellationToken ct = default)
            => Task.FromResult(new PackageOperation { PackageId = "*", Result = "success" });

        public Task<IReadOnlyList<PackageOperation>> GetOperationsAsync(int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PackageOperation>>([]);
    }

    private static (WempDbContext Db, SoftwareGroupService Service, FakePackageManager Packages) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();

        var packages = new FakePackageManager();
        return (db, new SoftwareGroupService(db, packages), packages);
    }

    [Fact]
    public async Task Create_and_list_groups()
    {
        var (_, service, _) = CreateHarness();

        await service.CreateGroupAsync("重装必装", "装机后一键安装");
        await service.CreateGroupAsync("办公套件", null);

        var groups = await service.GetGroupsAsync();
        Assert.Equal(2, groups.Count);
        Assert.Equal("重装必装", groups[0].Name);
        Assert.Equal("办公套件", groups[1].Name);
    }

    [Fact]
    public async Task Add_and_remove_items()
    {
        var (db, service, _) = CreateHarness();
        var group = await service.CreateGroupAsync("开发工具", null);

        await service.AddItemAsync(group.Id, "Microsoft.VisualStudioCode", "VS Code");
        await service.AddItemAsync(group.Id, "Git.Git", "Git");

        var groups = await service.GetGroupsAsync();
        Assert.Equal(2, groups.Single().Items.Count);
        Assert.Equal("VS Code", groups.Single().Items.First(i => i.PackageId == "Microsoft.VisualStudioCode").DisplayName);

        // 重复添加忽略
        await service.AddItemAsync(group.Id, "Git.Git", "Git");
        Assert.Equal(2, (await service.GetGroupsAsync()).Single().Items.Count);

        // 移除
        var itemId = groups.Single().Items.First().Id;
        await service.RemoveItemAsync(itemId);
        Assert.Single((await service.GetGroupsAsync()).Single().Items);
    }

    [Fact]
    public async Task Delete_group_cascades_items()
    {
        var (db, service, _) = CreateHarness();
        var group = await service.CreateGroupAsync("临时", null);
        await service.AddItemAsync(group.Id, "Microsoft.PowerShell", null);

        await service.DeleteGroupAsync(group.Id);

        Assert.Empty(await service.GetGroupsAsync());
        Assert.Equal(0, await db.SoftwareGroupItems.CountAsync());
    }

    [Fact]
    public async Task InstallGroup_installs_items_in_order()
    {
        var (_, service, packages) = CreateHarness();
        var group = await service.CreateGroupAsync("装机必备", null);
        await service.AddItemAsync(group.Id, "Google.Chrome", null);
        await service.AddItemAsync(group.Id, "Microsoft.VisualStudioCode", null);

        var count = await service.InstallGroupAsync(group.Id);

        Assert.Equal(2, count);
        Assert.Equal(["Google.Chrome", "Microsoft.VisualStudioCode"], packages.InstalledIds);
    }

    [Fact]
    public async Task InstallGroup_returns_zero_for_missing_group()
    {
        var (_, service, packages) = CreateHarness();

        var count = await service.InstallGroupAsync(999);

        Assert.Equal(0, count);
        Assert.Empty(packages.InstalledIds);
    }
}
