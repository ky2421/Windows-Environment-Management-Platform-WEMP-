using System.ComponentModel;
using System.Diagnostics;
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

            var session = _sessionService.CurrentSession;
            var pid = ForegroundWindow.GetForegroundProcessId();

            // 有进行中的会话：只要游戏进程仍存活就继续计时。
            // 玩家切到浏览器/其他窗口不打断会话，进程退出才结束。
            if (session is not null)
            {
                if (!IsProcessAlive(session.ProcessId))
                {
                    await _sessionService.EndCurrentSessionAsync();
                }
                else if (pid is not null && pid.Value != session.ProcessId
                         && _detector.IsGameProcessById(pid.Value))
                {
                    // 前台切到另一个已识别游戏（如先开 A 再开 B）：结束旧会话，切换新会话
                    await _sessionService.EndCurrentSessionAsync();
                    await _sessionService.StartSessionAsync(pid.Value);
                }

                return;
            }

            // 无会话：前台命中游戏库则开始
            if (pid is not null && _detector.IsGameProcessById(pid.Value))
            {
                await _sessionService.StartSessionAsync(pid.Value);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "游戏模式自动监测异常");
        }
    }

    private static bool IsProcessAlive(int? processId)
    {
        if (processId is null)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false; // 进程不存在或已退出
        }
        catch (Win32Exception)
        {
            return true; // 无权限访问（罕见）：保守保持会话，避免误截断
        }
    }
}
