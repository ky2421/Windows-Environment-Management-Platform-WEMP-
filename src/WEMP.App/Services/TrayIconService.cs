using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Serilog;
using WEMP.App.ViewModels;
using WEMP.GameMode.Detection;
using WEMP.GameMode.Services;

namespace WEMP.App.Services;

/// <summary>
/// 系统托盘图标：双击/菜单显示主窗口，提供游戏模式快速开关
/// （自动监测、开始/结束游戏会话、自定义游戏库入口）。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IGameSessionService _sessionService;
    private readonly IGameDetector _detector;
    private readonly MainViewModel _mainViewModel;
    private readonly MainWindow _window;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoMonitorItem;
    private bool _disposed;

    public TrayIconService(
        IGameSessionService sessionService,
        IGameDetector detector,
        MainViewModel mainViewModel,
        MainWindow window)
    {
        _sessionService = sessionService;
        _detector = detector;
        _mainViewModel = mainViewModel;
        _window = window;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "WEMP — Windows 环境管理平台",
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        _autoMonitorItem = new ToolStripMenuItem("自动监测游戏")
        {
            CheckOnClick = true,
            Checked = _sessionService.IsAutoMonitorEnabled,
        };
        _autoMonitorItem.CheckedChanged += (_, _) => ToggleAutoMonitor();

        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => _autoMonitorItem.Checked = _sessionService.IsAutoMonitorEnabled;
        menu.Items.AddRange(
        [
            new ToolStripMenuItem("显示主窗口", null, (_, _) => ShowMainWindow()),
            new ToolStripMenuItem("自定义游戏库", null, (_, _) => OpenGameLibrary()),
            new ToolStripSeparator(),
            _autoMonitorItem,
            new ToolStripMenuItem("开始游戏会话", null, async (_, _) => await StartSessionAsync()),
            new ToolStripMenuItem("结束游戏会话", null, async (_, _) => await EndSessionAsync()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("退出", null, (_, _) =>
            {
                Application.Current.Shutdown();
            }),
        ]);
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void ShowMainWindow()
    {
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Show();
        _window.Activate();
    }

    /// <summary>打开主窗口并导航到游戏模式页。</summary>
    private void OpenGameLibrary()
    {
        ShowMainWindow();
        var gamePage = _mainViewModel.NavigationItems.FirstOrDefault(p => p.Key == "gamemode");
        if (gamePage is not null)
        {
            _mainViewModel.SelectedItem = gamePage;
        }
    }

    private void ToggleAutoMonitor()
    {
        try
        {
            _sessionService.IsAutoMonitorEnabled = _autoMonitorItem.Checked;
            ShowBalloon(_autoMonitorItem.Checked ? "自动监测已开启" : "自动监测已关闭");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "切换自动监测失败");
        }
    }

    private async Task StartSessionAsync()
    {
        var pid = ForegroundWindow.GetForegroundProcessId();
        if (pid is null)
        {
            ShowBalloon("未检测到前台窗口");
            return;
        }

        if (!_detector.IsGameProcessById(pid.Value))
        {
            ShowBalloon("前台进程不在游戏库中，可在“自定义游戏库”中添加");
            return;
        }

        var session = await _sessionService.StartSessionAsync(pid.Value);
        ShowBalloon(session is null ? "已有进行中的会话" : $"已进入游戏模式：{session.GameName}");
    }

    private async Task EndSessionAsync()
    {
        var session = await _sessionService.EndCurrentSessionAsync();
        ShowBalloon(session is null
            ? "当前没有进行中的会话"
            : $"已退出游戏模式，时长 {session.DurationSeconds} 秒");
    }

    private void ShowBalloon(string message)
    {
        _notifyIcon.ShowBalloonTip(2000, "WEMP", message, ToolTipIcon.Info);
    }

    private static Icon LoadIcon()
    {
        using var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/WEMP.App;component/Assets/app.ico"))?.Stream
            ?? throw new InvalidOperationException("无法加载托盘图标资源");
        return new Icon(stream);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
