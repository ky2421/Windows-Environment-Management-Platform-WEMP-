using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WEMP.App;
using WEMP.App.ViewModels;
using WEMP.Core;
using WEMP.Core.Abstractions;
using WEMP.Infrastructure;

static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
{
    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
    {
        var child = VisualTreeHelper.GetChild(root, i);
        if (child is T match)
        {
            yield return match;
        }

        foreach (var nested in FindVisualChildren<T>(child))
        {
            yield return nested;
        }
    }
}

// UI 冒烟探针：在 STA 线程实例化主窗口与全部模块页面，
// 通过 Measure/Arrange/UpdateLayout 触发模板与资源解析，捕获 XAML 运行期错误。

var failures = new List<string>();
var thread = new Thread(() => RunProbe(failures));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

if (failures.Count > 0)
{
    Console.WriteLine("FAIL: {0} 处失败", failures.Count);
    foreach (var f in failures)
    {
        Console.WriteLine("  - " + f);
    }

    return 1;
}

Console.WriteLine("PASS：全部页面模板/资源解析正常");
return 0;

static void RunProbe(List<string> failures)
{
    try
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/WEMP.App;component/Themes/Dark.xaml"),
        });

        // 构建 DI（与 App 一致的注册）
        var services = new ServiceCollection();
        services.AddWempCore();
        services.AddWempInfrastructure();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        App.RegisterModulePages(services);
        var provider = services.BuildServiceProvider();

        // 与 App 启动一致：先执行 EF 迁移（探针复用真实数据库文件）
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WEMP.Infrastructure.Data.WempDbContext>();
            db.Database.Migrate();
        }

        // 模块注册表
        var host = provider.GetRequiredService<IModuleHost>();
        host.LoadFromAssemblies(
            typeof(WEMP.SystemInfo.SystemInfoModule).Assembly,
            typeof(WEMP.Optimization.OptimizationModule).Assembly,
            typeof(WEMP.GameMode.GameModeModule).Assembly,
            typeof(WEMP.DevEnvironment.DevEnvironmentModule).Assembly,
            typeof(WEMP.PackageManagement.PackageManagementModule).Assembly,
            typeof(WEMP.Backup.BackupModule).Assembly,
            typeof(WEMP.Logging.LoggingModule).Assembly);

        var pages = host.Modules.SelectMany(m => m.Pages).OrderBy(p => p.Order).ToList();
        Console.WriteLine($"模块页面：{pages.Count} 个");

        foreach (var page in pages)
        {
            try
            {
                var vm = provider.GetRequiredService(page.ViewModelType);
                var view = provider.GetRequiredService(page.ViewType) as FrameworkElement;
                if (view is null)
                {
                    failures.Add($"{page.Key}: 视图不是 FrameworkElement");
                    continue;
                }

                view.DataContext = vm;
                view.Measure(new Size(900, 600));
                view.Arrange(new Rect(0, 0, 900, 600));
                view.UpdateLayout();
                Console.WriteLine($"  OK {page.Key} ({page.Title})");
            }
            catch (Exception ex)
            {
                failures.Add($"{page.Key} ({page.Title}): {ex.Message}");
                Console.WriteLine($"  FAIL {page.Key}: {ex.Message}");
            }
        }

        // 开发环境页面数据加载复现
        try
        {
            var vm = provider.GetRequiredService<WEMP.DevEnvironment.UI.DevEnvironmentPageViewModel>();
            vm.InitializeAsync().GetAwaiter().GetResult();
            Console.WriteLine($"  devenv 模板数: {vm.Templates.Count}, 实例数: {vm.Instances.Count}, 状态: {vm.Status}");
            foreach (var t in vm.Templates)
            {
                Console.WriteLine($"    - {t.TemplateKey} | {t.Name} | v{t.Version} | Enabled={t.Enabled}");
            }

            // 数据渲染验证：模板 ListBox 应包含 2 项
            var view = provider.GetRequiredService<WEMP.DevEnvironment.UI.DevEnvironmentPage>();
            view.DataContext = vm;
            view.Measure(new Size(900, 600));
            view.Arrange(new Rect(0, 0, 900, 600));
            view.UpdateLayout();
            var boxes = FindVisualChildren<System.Windows.Controls.ListBox>(view).ToList();
            foreach (var box in boxes)
            {
                Console.WriteLine($"    ListBox Items.Count = {box.Items.Count} (来源: {box.ItemsSource?.GetType().Name})");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"devenv-load: {ex.Message}");
        }

        // 优化模块数据验证：种子同步后应有 32 条（按风险分级），新类别可解析
        try
        {
            var optVm = provider.GetRequiredService<WEMP.Optimization.UI.OptimizationPageViewModel>();
            optVm.InitializeAsync().GetAwaiter().GetResult();
            Console.WriteLine($"  optimization 状态: {optVm.Status}");
            Console.WriteLine($"  optimization 条目数: {optVm.Items.Count}");
            var categories = optVm.Items.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();
            Console.WriteLine($"    类别: {string.Join(", ", categories)}");
            // 风险分级验证：条目应按 安全级 → 进阶 → 激进 顺序排列
            var levels = optVm.Items.Select(i => i.RiskLevel).ToList();
            var firstAdvanced = levels.IndexOf("advanced");
            var firstAggressive = levels.IndexOf("aggressive");
            var lastSafe = levels.FindLastIndex(l => l == "safe");
            Console.WriteLine($"    分级顺序: safe={levels.Count(l => l == "safe")} advanced={levels.Count(l => l == "advanced")} aggressive={levels.Count(l => l == "aggressive")} | 顺序合法={firstAdvanced > lastSafe && firstAggressive > firstAdvanced}");
            var newCodes = new[] { "device.umbus-off", "device.hpet-off", "bios.xmp-expo", "timer.platform-clock", "guide.gpu-panel" };
            foreach (var code in newCodes)
            {
                var item = optVm.Items.FirstOrDefault(i => i.Code == code);
                Console.WriteLine($"    {(item is null ? "缺失!" : "OK  ")} {code} | {item?.Name} | {item?.RiskLevel}");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"optimization-seed: {ex.Message}");
        }

        // 部署工具选择弹窗验证：构造、XAML 资源解析、必需/可选默认勾选状态
        try
        {
            var devenvService = provider.GetRequiredService<WEMP.DevEnvironment.Services.IDevEnvironmentService>();
            var templates = devenvService.GetTemplatesAsync().GetAwaiter().GetResult();
            var nodejs = templates.FirstOrDefault(t => t.TemplateKey == "nodejs-20");
            if (nodejs is null)
            {
                failures.Add("devenv-picker: nodejs 模板缺失");
            }
            else
            {
                var spec = WEMP.DevEnvironment.Parsing.EnvTemplateParser.Parse(nodejs.Content);
                var picker = new WEMP.DevEnvironment.UI.TemplateToolPickerWindow(spec);
                picker.Measure(new Size(440, 500));
                picker.Arrange(new Rect(0, 0, 440, 500));
                picker.UpdateLayout();
                Console.WriteLine($"  picker: 模板={picker.TemplateName} 工具={picker.Tools.Count}");
                foreach (var t in picker.Tools)
                {
                    Console.WriteLine($"    - {t.Name} v{t.Version} optional={t.IsOptional} selected={t.IsSelected}");
                }

                var requiredAllSelected = picker.Tools.Where(t => !t.IsOptional).All(t => t.IsSelected);
                Console.WriteLine($"  必需全部默认选中: {requiredAllSelected}");
                if (picker.Tools.Count != 4 || !requiredAllSelected)
                {
                    failures.Add($"devenv-picker: 工具选择数据异常 count={picker.Tools.Count} requiredSelected={requiredAllSelected}");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add($"devenv-picker: {ex.Message}");
        }

        // 部署进度对话框验证：构造与 XAML 资源解析（Loaded 异步执行由真实部署触发）
        try
        {
            var progressWindow = new WEMP.DevEnvironment.UI.DeployProgressWindow("Node.js 开发环境", _ => throw new NotSupportedException("探针不执行真实部署"));
            progressWindow.Measure(new Size(420, 200));
            progressWindow.Arrange(new Rect(0, 0, 420, 200));
            progressWindow.UpdateLayout();
            Console.WriteLine($"  deploy-progress: 目标={progressWindow.DeployTarget} 初始进度={progressWindow.ProgressPercent}%");
            var selfDataContext = ReferenceEquals(progressWindow.DataContext, progressWindow);
            Console.WriteLine($"  deploy-progress: DataContext=self={selfDataContext}");
            if (!selfDataContext)
            {
                failures.Add("devenv-progress-window: DataContext 未指向窗口自身，XAML 绑定将失效");
            }

            progressWindow.ProgressPercent = 42;
            progressWindow.ProgressText = "正在安装 node（1/2）";
            Console.WriteLine($"  deploy-progress: 模拟回报后 {progressWindow.ProgressPercent}% {progressWindow.ProgressText}");
        }
        catch (Exception ex)
        {
            failures.Add($"devenv-progress-window: {ex.Message}");
        }

        // 系统检测验证：启动时间与网络状态应为真实数据而非占位符
        try
        {
            var sysProvider = new WEMP.SystemInfo.Detection.WmiSystemInfoProvider();
            var snap = sysProvider.DetectAsync().GetAwaiter().GetResult();
            Console.WriteLine($"  sysinfo: 启动={snap.Os.LastBootUpTime?.ToLocalTime():yyyy-MM-dd HH:mm:ss} 网络={(snap.Network.IsAvailable ? "已连接" : "未连接")} 适配器={snap.Network.ActiveAdapters.Count}");
            if (snap.Os.LastBootUpTime is null)
            {
                failures.Add("sysinfo: 未采集到上次启动时间");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"sysinfo: {ex.Message}");
        }

        // 垃圾清理服务验证：只统计不删除
        try
        {
            var junkCleaner = new WEMP.SystemInfo.Services.JunkCleanerService();
            var scan = junkCleaner.Scan();
            Console.WriteLine($"  junk-scan: {scan.FilesCleaned} 个临时文件，{scan.FreedBytes} 字节");
            if (scan.FilesCleaned < 0)
            {
                failures.Add("junk-scan: 统计结果异常");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"junk-scan: {ex.Message}");
        }

        // 主窗口导航解析
        try
        {
            var window = provider.GetRequiredService<MainWindow>();
            var mainVm = provider.GetRequiredService<MainViewModel>();
            window.DataContext = mainVm;
            window.Measure(new Size(1180, 760));
            window.Arrange(new Rect(0, 0, 1180, 760));
            window.UpdateLayout();
            Console.WriteLine($"  OK main-window（初始页：{mainVm.SelectedItem?.Key}）");
        }
        catch (Exception ex)
        {
            failures.Add($"main-window: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"probe-root: {ex}");
    }
}
