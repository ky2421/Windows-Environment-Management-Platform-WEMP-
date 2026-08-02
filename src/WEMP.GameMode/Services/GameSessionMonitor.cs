using Serilog;
using WEMP.GameMode.Detection;

namespace WEMP.GameMode.Services;

/// <summary>
/// 游戏会话自动监测：周期性检测前台窗口进程，
/// 命中游戏库且无会话时自动开始，前台离开游戏时自动结束。
/// </summary>
public sealed class GameSessionMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly IGameSessionService _sessionService;
    private readonly IGameDetector _detector;
    private Timer? _timer;

    public GameSessionMonitor(IGameSessionService sessionService, IGameDetector detector)
    {
        _sessionService = sessionService;
        _detector = detector;
    }

    /// <summary>启动自动监测（仅当设置开启时生效）。</summary>
    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new Timer(OnTick, null, PollInterval, PollInterval);
        Log.Information("游戏模式自动监测已启动（间隔 {Interval}s）", PollInterval.TotalSeconds);
    }

    /// <summary>停止自动监测。</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private async void OnTick(object? state)
    {
        try
        {
            if (!_sessionService.IsAutoMonitorEnabled)
            {
                return;
            }

            var pid = ForegroundWindow.GetForegroundProcessId();

            if (pid is not null && _detector.IsGameProcessById(pid.Value))
            {
                if (_sessionService.CurrentSession is null)
                {
                    await _sessionService.StartSessionAsync(pid.Value);
                }
            }
            else if (_sessionService.CurrentSession is not null)
            {
                await _sessionService.EndCurrentSessionAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "游戏模式自动监测异常");
        }
    }
}
