using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.GameMode.Detection;
using WEMP.GameMode.Services;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.GameMode.UI;

/// <summary>游戏模式页面视图模型。</summary>
public partial class GameModePageViewModel : ObservableObject
{
    private readonly IGameSessionService _service;
    private readonly IGameDetector _detector;
    private readonly ICustomGameLibrary _customLibrary;
    private readonly DispatcherTimer _clock;

    public ObservableCollection<GameSession> History { get; } = [];
    public ObservableCollection<CustomGame> CustomGames { get; } = [];

    [ObservableProperty]
    private bool _isAutoMonitor;

    [ObservableProperty]
    private string _currentGame = "未在游戏中";

    [ObservableProperty]
    private string _elapsed = "--:--:--";

    [ObservableProperty]
    private bool _hasSession;

    [ObservableProperty]
    private string _status = "正在加载…";

    [ObservableProperty]
    private bool _isAdministrator;

    /// <summary>非管理员时提示权限说明。</summary>
    public bool ShowPermissionHint => !IsAdministrator;

    [ObservableProperty]
    private string _newGameName = "";

    [ObservableProperty]
    private string _newProcessName = "";

    [ObservableProperty]
    private string _libraryStatus = "";

    public GameModePageViewModel(IGameSessionService service, IGameDetector detector, ICustomGameLibrary customLibrary)
    {
        _service = service;
        _detector = detector;
        _customLibrary = customLibrary;
        _service.SessionStarted += OnSessionStarted;
        _service.SessionEnded += OnSessionEnded;

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => RefreshElapsed();

        IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        OnPropertyChanged(nameof(ShowPermissionHint));
    }

    public async Task InitializeAsync()
    {
        IsAutoMonitor = _service.IsAutoMonitorEnabled;

        if (_service.CurrentSession is { } current)
        {
            SetCurrentSession(current);
        }

        await RefreshHistoryAsync();
        RefreshCustomGamesAsync();
        Status = $"自动监测已{(IsAutoMonitor ? "开启" : "关闭")}";
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        var pid = ForegroundWindow.GetForegroundProcessId();
        if (pid is null)
        {
            Status = "未检测到前台窗口";
            return;
        }

        if (!_detector.IsGameProcessById(pid.Value))
        {
            Status = "前台进程不在游戏库中，可在下方自定义游戏库中添加";
            return;
        }

        var session = await _service.StartSessionAsync(pid.Value);
        if (session is null)
        {
            Status = "已有进行中的会话";
        }
    }

    [RelayCommand]
    private async Task EndSessionAsync()
    {
        var session = await _service.EndCurrentSessionAsync();
        if (session is null)
        {
            Status = "当前没有进行中的会话";
        }
    }

    private async void OnSessionStarted(object? sender, GameSession session)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            SetCurrentSession(session);
            Status = $"检测到游戏：{session.GameName}";
        });
    }

    private async void OnSessionEnded(object? sender, GameSession session)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            ClearCurrentSession();
            await RefreshHistoryAsync();
            Status = $"游戏已退出：{session.GameName}，时长 {FormatDuration(session.DurationSeconds)}";
        });
    }

    private void SetCurrentSession(GameSession session)
    {
        _clock.Start();
        HasSession = true;
        CurrentGame = session.GameName;
        RefreshElapsed();
    }

    private void ClearCurrentSession()
    {
        _clock.Stop();
        HasSession = false;
        CurrentGame = "未在游戏中";
        Elapsed = "--:--:--";
    }

    private void RefreshElapsed()
    {
        if (_service.CurrentSession is { StartedAt: var started })
        {
            Elapsed = FormatDuration((long)(DateTime.Now - started).TotalSeconds);
        }
    }

    private async Task RefreshHistoryAsync()
    {
        var history = await _service.GetHistoryAsync(10);
        History.Clear();
        foreach (var session in history)
        {
            History.Add(session);
        }
    }

    private static string FormatDuration(long seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private void RefreshCustomGamesAsync()
    {
        CustomGames.Clear();
        foreach (var game in _customLibrary.GetAll())
        {
            CustomGames.Add(game);
        }
    }

    [RelayCommand]
    private async Task AddCustomGameAsync()
    {
        try
        {
            var game = await _customLibrary.AddAsync(NewGameName, NewProcessName);
            CustomGames.Add(game);
            LibraryStatus = $"已添加：{game.Name}（{game.ProcessName}）";
            NewGameName = "";
            NewProcessName = "";
        }
        catch (InvalidOperationException ex)
        {
            LibraryStatus = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加自定义游戏失败");
            LibraryStatus = $"添加失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveCustomGameAsync(CustomGame game)
    {
        var removed = await _customLibrary.RemoveAsync(game.Id);
        if (!removed)
        {
            LibraryStatus = "条目不存在，可能已被删除";
            return;
        }

        CustomGames.Remove(game);
        LibraryStatus = $"已删除：{game.Name}";
    }
}
