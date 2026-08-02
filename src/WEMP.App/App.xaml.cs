using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WEMP.App.ViewModels;
using WEMP.Core;
using WEMP.Core.Abstractions;
using WEMP.Infrastructure;
using WEMP.Infrastructure.Data;

namespace WEMP.App;

/// <summary>
/// 应用入口：构建依赖注入容器、初始化日志、加载模块并显示主窗口。
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;
    private IModuleHost? _moduleHost;
    private WEMP.GameMode.Services.GameSessionMonitor? _gameMonitor;
    private WEMP.GameMode.Services.IGameSessionService? _sessionService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(WempDatabase.DefaultDataDirectory, "logs", "wemp-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        try
        {
            Log.Information("启动：日志初始化完成");

            var services = new ServiceCollection();
            services.AddWempCore();
            services.AddWempInfrastructure();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainViewModel>();

            // 注册各模块页面（模块占位阶段为空，业务开发时由模块自身注册）
            RegisterModulePages(services);

            _services = services.BuildServiceProvider();
            Log.Information("启动：DI 容器构建完成");

            // 数据库就绪：应用迁移并创建数据库文件
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WempDbContext>();
                db.Database.Migrate();
            }
            Log.Information("启动：数据库迁移完成");

            _moduleHost = _services.GetRequiredService<IModuleHost>();
            _moduleHost.LoadFromAssemblies(
                typeof(WEMP.SystemInfo.SystemInfoModule).Assembly,
                typeof(WEMP.Optimization.OptimizationModule).Assembly,
                typeof(WEMP.GameMode.GameModeModule).Assembly,
                typeof(WEMP.DevEnvironment.DevEnvironmentModule).Assembly,
                typeof(WEMP.PackageManagement.PackageManagementModule).Assembly,
                typeof(WEMP.Backup.BackupModule).Assembly,
                typeof(WEMP.Logging.LoggingModule).Assembly);

            await _moduleHost.InitializeAllAsync().ConfigureAwait(true);
            await _moduleHost.ActivateAllAsync().ConfigureAwait(true);
            Log.Information("启动：模块初始化完成，共 {Count} 个模块", _moduleHost.Modules.Count);

            // 启动游戏模式自动监测（按设置开关生效）
            _gameMonitor = _services.GetRequiredService<WEMP.GameMode.Services.GameSessionMonitor>();
            _sessionService = _services.GetRequiredService<WEMP.GameMode.Services.IGameSessionService>();
            _gameMonitor.Start();
            Log.Information("启动：游戏模式监测器已就绪");

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            Log.Information("启动：主窗口已显示");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WEMP 启动失败");
            MessageBox.Show(
                $"WEMP 启动失败：{ex.Message}",
                "WEMP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            // 停止游戏监测并结束进行中的会话（保存记录、恢复系统状态）
            _gameMonitor?.Dispose();
            if (_sessionService is WEMP.GameMode.Services.GameSessionService concrete)
            {
                await concrete.DisposeAsync().ConfigureAwait(true);
            }

            if (_moduleHost is not null)
            {
                await _moduleHost.ShutdownAllAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "模块关闭失败");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void RegisterModulePages(IServiceCollection services)
    {
        // 系统检测模块
        services.AddSingleton<WEMP.SystemInfo.Detection.ISystemInfoProvider, WEMP.SystemInfo.Detection.WmiSystemInfoProvider>();
        services.AddSingleton<WEMP.SystemInfo.Persistence.ISnapshotRepository, WEMP.SystemInfo.Persistence.SnapshotRepository>();
        services.AddTransient<WEMP.SystemInfo.UI.SystemInfoViewModel>();
        services.AddTransient<WEMP.SystemInfo.UI.SystemInfoPage>();

        // 软件包管理模块
        services.AddSingleton<WEMP.PackageManagement.Winget.IPackageProvider, WEMP.PackageManagement.Winget.WingetCli>();
        services.AddSingleton<WEMP.PackageManagement.Services.IPackageManagerService, WEMP.PackageManagement.Services.PackageManagerService>();
        services.AddSingleton<WEMP.PackageManagement.Services.ISoftwareGroupService, WEMP.PackageManagement.Services.SoftwareGroupService>();
        services.AddTransient<WEMP.PackageManagement.UI.PackageManagementPageViewModel>();
        services.AddTransient<WEMP.PackageManagement.UI.PackageManagementPage>();

        // 游戏模式模块
        services.AddSingleton<WEMP.GameMode.Detection.IGameDetector, WEMP.GameMode.Detection.GameLibraryDetector>();
        services.AddSingleton<WEMP.GameMode.Services.IGameStateSwitcher, WEMP.GameMode.Services.SystemStateSwitcher>();
        services.AddSingleton<WEMP.GameMode.Services.IGameSessionService, WEMP.GameMode.Services.GameSessionService>();
        services.AddSingleton<WEMP.GameMode.Services.GameSessionMonitor>();
        services.AddTransient<WEMP.GameMode.UI.GameModePageViewModel>();
        services.AddTransient<WEMP.GameMode.UI.GameModePage>();

        // 系统优化模块
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.RegistryAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.ServiceAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.StartupAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.NetworkAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.DiskAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.PowerAction>();
        services.AddSingleton<WEMP.Optimization.Execution.IOptimizationAction, WEMP.Optimization.Execution.MemoryAction>();
        services.AddSingleton<WEMP.Optimization.Execution.OptimizationActionFactory>();
        services.AddSingleton<WEMP.Optimization.Services.IOptimizationService, WEMP.Optimization.Services.OptimizationService>();
        services.AddSingleton<WEMP.Optimization.Seeding.OptimizationSeedService>();
        services.AddTransient<WEMP.Optimization.UI.OptimizationPageViewModel>();
        services.AddTransient<WEMP.Optimization.UI.OptimizationPage>();

        // 其余模块业务页面在业务开发阶段注册
    }
}
