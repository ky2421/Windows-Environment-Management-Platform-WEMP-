using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using WEMP.Backup.Services;
using WEMP.Backup.UI;
using WEMP.DevEnvironment.Models;
using WEMP.DevEnvironment.Services;
using WEMP.DevEnvironment.UI;
using WEMP.GameMode.Detection;
using WEMP.GameMode.Services;
using WEMP.GameMode.UI;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Logging.Services;
using WEMP.Logging.UI;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;
using WEMP.Optimization.UI;
using WEMP.PackageManagement.Services;
using WEMP.PackageManagement.UI;
using WEMP.SystemInfo.Services;
using WEMP.SystemInfo.UI;
using Xunit;

namespace WEMP.Ui.Tests;

/// <summary>
/// UI 测试：页面加载。在 UI 线程构造每个模块页面（触发 XAML InitializeComponent），
/// 验证全部 StaticResource 引用（主题样式、转换器）可解析、无 XamlParseException。
/// 页面构造器注入真实服务 + 内存数据库；系统副作用接口用测试替身。
/// </summary>
public class PageLoadTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestInfra.TestDbFactory _factory;

    public PageLoadTests()
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
    }

    [StaFact]
    public void BackupPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var vm = new BackupPageViewModel(new BackupService(_factory));
            var page = new BackupPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void DevEnvironmentPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var service = new DevEnvironmentService(
                _factory,
                new TestInfra.FakeInstaller(),
                new TestInfra.FakeEnvVars(),
                new TestInfra.FakeConfigWriter(),
                new TestInfra.FakeValidator());
            var vm = new DevEnvironmentPageViewModel(service);
            var page = new DevEnvironmentPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void GameModePage_加载成功()
    {
        UiThread.Run(() =>
        {
            var library = new CustomGameLibraryService(_factory);
            var detector = new GameLibraryDetector(library);
            var session = new GameSessionService(_factory, new TestInfra.FakeSwitcher(), detector);
            var vm = new GameModePageViewModel(session, detector, library);
            var page = new GameModePage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void LoggingPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var service = new LoggingService(
                _factory,
                new AuditLogService(_factory),
                new TestInfra.FakeEventSource(),
                new TestInfra.FakeAnomalyDetector());
            var vm = new LoggingPageViewModel(service);
            var page = new LoggingPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void OptimizationPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var service = new OptimizationService(_factory, new OptimizationActionFactory([new TestInfra.FakeAction()]));
            var vm = new OptimizationPageViewModel(service, new OptimizationSeedService(_factory));
            var page = new OptimizationPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void PackageManagementPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var pkgService = new PackageManagerService(_factory, new TestInfra.FakeProvider());
            var groupService = new SoftwareGroupService(_factory, pkgService);
            var vm = new PackageManagementPageViewModel(pkgService, groupService);
            var page = new PackageManagementPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void SystemInfoPage_加载成功()
    {
        UiThread.Run(() =>
        {
            var vm = new SystemInfoViewModel(
                new TestInfra.FakeSystemInfoProvider(),
                new TestInfra.FakeSnapshotRepository(),
                new JunkCleanerService());
            var page = new SystemInfoPage(vm);
            Assert.IsAssignableFrom<UserControl>(page);
            Assert.Same(vm, page.DataContext);
        });
    }

    [StaFact]
    public void DeployProgressWindow_加载成功()
    {
        UiThread.Run(() =>
        {
            var window = new DeployProgressWindow(
                "测试环境",
                _ => Task.FromResult(new EnvInstance { Name = "测试实例" }));
            Assert.NotNull(window);
        });
    }

    [StaFact]
    public void 全部页面_同一UI线程构造无异常()
    {
        UiThread.Run(() =>
        {
            var devService = new DevEnvironmentService(
                _factory,
                new TestInfra.FakeInstaller(),
                new TestInfra.FakeEnvVars(),
                new TestInfra.FakeConfigWriter(),
                new TestInfra.FakeValidator());
            var gameLibrary = new CustomGameLibraryService(_factory);
            var gameDetector = new GameLibraryDetector(gameLibrary);
            var pkgService = new PackageManagerService(_factory, new TestInfra.FakeProvider());

            var pages = new UserControl[]
            {
                new BackupPage(new BackupPageViewModel(new BackupService(_factory))),
                new DevEnvironmentPage(new DevEnvironmentPageViewModel(devService)),
                new GameModePage(new GameModePageViewModel(
                    new GameSessionService(_factory, new TestInfra.FakeSwitcher(), gameDetector),
                    gameDetector,
                    gameLibrary)),
                new LoggingPage(new LoggingPageViewModel(new LoggingService(
                    _factory,
                    new AuditLogService(_factory),
                    new TestInfra.FakeEventSource(),
                    new TestInfra.FakeAnomalyDetector()))),
                new OptimizationPage(new OptimizationPageViewModel(
                    new OptimizationService(_factory, new OptimizationActionFactory([new TestInfra.FakeAction()])),
                    new OptimizationSeedService(_factory))),
                new PackageManagementPage(new PackageManagementPageViewModel(pkgService, new SoftwareGroupService(_factory, pkgService))),
                new SystemInfoPage(new SystemInfoViewModel(
                    new TestInfra.FakeSystemInfoProvider(),
                    new TestInfra.FakeSnapshotRepository(),
                    new JunkCleanerService())),
            };
            Assert.Equal(7, pages.Length);
        });
    }
}
