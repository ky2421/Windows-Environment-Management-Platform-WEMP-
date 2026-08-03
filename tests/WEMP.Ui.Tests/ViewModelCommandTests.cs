using System.IO;
using Microsoft.Data.Sqlite;
using WEMP.Backup.Services;
using WEMP.Backup.UI;
using WEMP.GameMode.Detection;
using WEMP.GameMode.Services;
using WEMP.GameMode.UI;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;
using WEMP.Optimization.UI;
using WEMP.PackageManagement.Services;
using WEMP.PackageManagement.UI;
using Xunit;

namespace WEMP.Ui.Tests;

/// <summary>
/// UI 测试：按钮响应。直接执行 ViewModel 的 RelayCommand（按钮绑定目标），
/// 验证状态变化、集合更新与错误处理；服务层用真实实现 + 内存数据库，
/// 系统副作用接口用测试替身隔离。
/// </summary>
public class ViewModelCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestInfra.TestDbFactory _factory;
    private string? _tempDir;

    public ViewModelCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _factory = new TestInfra.TestDbFactory(_connection);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (_tempDir is not null && Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string CreateTempDir()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wemp-ui-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "dest"));
        return _tempDir;
    }

    // ---- 备份页面：保存任务命令 ----

    [StaFact]
    public async Task Backup_SaveTaskCommand_创建任务并更新集合()
    {
        await UiThread.RunAsync(async () =>
        {
            var dir = CreateTempDir();
            var vm = new BackupPageViewModel(new BackupService(_factory));
            vm.Name = "测试备份";
            vm.SourcePath = Path.Combine(dir, "src");
            vm.DestinationPath = Path.Combine(dir, "dest");

            await vm.SaveTaskCommand.ExecuteAsync(null);

            Assert.Single(vm.Tasks);
            Assert.Equal("测试备份", vm.Tasks[0].Name);
            Assert.Contains("已创建任务", vm.Status);
            Assert.False(vm.IsBusy);
        });
    }

    [StaFact]
    public async Task Backup_SaveTaskCommand_源路径不存在时失败提示()
    {
        await UiThread.RunAsync(async () =>
        {
            var dir = CreateTempDir();
            var vm = new BackupPageViewModel(new BackupService(_factory));
            vm.Name = "坏路径";
            vm.SourcePath = Path.Combine(dir, "not-exist");
            vm.DestinationPath = Path.Combine(dir, "dest");

            await vm.SaveTaskCommand.ExecuteAsync(null);

            Assert.Empty(vm.Tasks);
            Assert.Contains("源路径不存在", vm.Status);
        });
    }

    // ---- 优化页面：一键优化命令 ----

    [StaFact]
    public async Task Optimization_OneKeyOptimizeCommand_执行并写入历史()
    {
        await UiThread.RunAsync(async () =>
        {
            // 种子数据（72 条）覆盖全部 20 个类别；game 类别经工厂映射到 registry 执行器
            var categories = new[]
            {
                "appx", "background", "bios", "device", "disk", "game", "gpu", "guide", "hags",
                "memory", "network", "pagefile", "power", "registry", "scheduled-task", "service",
                "startup", "timer", "visual", "windows-feature",
            };
            var service = new OptimizationService(
                _factory,
                new OptimizationActionFactory(categories.Select(c => new TestInfra.FakeAction(c))));
            var vm = new OptimizationPageViewModel(service, new OptimizationSeedService(_factory));
            await vm.InitializeAsync();

            Assert.NotEmpty(vm.Items);

            await vm.OneKeyOptimizeCommand.ExecuteAsync(null);

            Assert.Contains("成功", vm.Status);
            Assert.False(vm.IsRunning);
            Assert.NotEmpty(vm.History);
        });
    }

    // ---- 游戏模式页面：自定义游戏命令 ----

    [StaFact]
    public async Task GameMode_AddCustomGameCommand_添加并移除自定义游戏()
    {
        await UiThread.RunAsync(async () =>
        {
            var library = new CustomGameLibraryService(_factory);
            var detector = new GameLibraryDetector(library);
            var session = new GameSessionService(_factory, new TestInfra.FakeSwitcher(), detector);
            var vm = new GameModePageViewModel(session, detector, library);

            vm.NewGameName = "我的游戏";
            vm.NewProcessName = "mygame.exe";
            await vm.AddCustomGameCommand.ExecuteAsync(null);

            Assert.Single(vm.CustomGames);
            Assert.Equal("mygame", vm.CustomGames[0].ProcessName);
            Assert.Contains("已添加", vm.LibraryStatus);
            Assert.True(detector.IsGameProcess("mygame"));

            await vm.RemoveCustomGameCommand.ExecuteAsync(vm.CustomGames[0]);

            Assert.Empty(vm.CustomGames);
            Assert.False(detector.IsGameProcess("mygame"));
        });
    }

    // ---- 软件包管理页面：分组命令 ----

    [StaFact]
    public async Task PackageManagement_SelectGroup_选择分组并填充条目()
    {
        await UiThread.RunAsync(async () =>
        {
            var pkgService = new PackageManagerService(_factory, new TestInfra.FakeProvider());
            var groupService = new SoftwareGroupService(_factory, pkgService);
            await groupService.CreateGroupAsync("常用工具", "UI 测试分组");

            var vm = new PackageManagementPageViewModel(pkgService, groupService);
            await vm.InitializeAsync();

            Assert.Single(vm.Groups);

            vm.SelectGroupCommand.Execute(vm.Groups[0]);

            Assert.Equal("常用工具", vm.SelectedGroup?.Name);
        });
    }
}
