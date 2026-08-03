using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.GameMode.Detection;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.GameMode.Services;

/// <summary>
/// 游戏会话服务实现：识别游戏进程 → 进入会话（记录 + 系统切换）→ 退出会话（恢复 + 计时）。
/// </summary>
public sealed class GameSessionService(
    IDbContextFactory<WempDbContext> dbFactory,
    IGameStateSwitcher stateSwitcher,
    IGameDetector detector) : IGameSessionService, IAsyncDisposable
{
    private const string AutoMonitorKey = "gamemode.autoMonitor";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private GameSession? _currentSession;
    private GameStateSnapshot? _currentSnapshot;

    public GameSession? CurrentSession => _currentSession;

    public event EventHandler<GameSession>? SessionStarted;

    public event EventHandler<GameSession>? SessionEnded;

    public bool IsAutoMonitorEnabled
    {
        get
        {
            using var db = dbFactory.CreateDbContext();
            var setting = db.AppSettings.AsNoTracking()
                .FirstOrDefault(s => s.Key == AutoMonitorKey);
            return setting is not null && setting.Value == "true";
        }
        set
        {
            using var db = dbFactory.CreateDbContext();
            var setting = db.AppSettings.FirstOrDefault(s => s.Key == AutoMonitorKey);
            if (setting is null)
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = AutoMonitorKey,
                    Value = value ? "true" : "false",
                    Module = "GameMode",
                    UpdatedAt = DateTime.Now,
                });
            }
            else
            {
                setting.Value = value ? "true" : "false";
            }

            db.SaveChanges();
        }
    }

    public async Task<GameSession?> StartSessionAsync(int processId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_currentSession is not null)
            {
                return null; // 已有进行中的会话
            }

            string processName;
            try
            {
                using var process = Process.GetProcessById(processId);
                processName = process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                Log.Warning("游戏进程已退出：PID {Pid}", processId);
                return null;
            }

            if (!detector.IsGameProcess(processName))
            {
                return null;
            }

            var session = new GameSession
            {
                GameName = processName,
                ProcessName = processName,
                ProcessId = processId,
                StartedAt = DateTime.Now,
            };

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            db.GameSessions.Add(session);
            db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                Module = "GameMode",
                Action = "game.session.start",
                Target = processName,
                Result = "success",
                User = Environment.UserName,
            });
            await db.SaveChangesAsync(cancellationToken);

            // 进入游戏模式：切换电源 + 释放后台进程（失败不阻断会话）
            try
            {
                _currentSnapshot = await stateSwitcher.EnterGameModeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "进入游戏模式系统切换失败：{Game}", processName);
                _currentSnapshot = null;
            }

            _currentSession = session;
            Log.Information("游戏会话开始：{Game}（PID {Pid}）", processName, processId);
            SessionStarted?.Invoke(this, session);

            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GameSession?> EndCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = _currentSession;
            if (session is null)
            {
                return null;
            }

            session.EndedAt = DateTime.Now;
            session.DurationSeconds = (long)(session.EndedAt.Value - session.StartedAt).TotalSeconds;

            // 恢复系统状态（失败不影响记录）
            if (_currentSnapshot is not null)
            {
                try
                {
                    await stateSwitcher.RestoreAsync(_currentSnapshot, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "退出游戏模式系统恢复失败");
                }

                _currentSnapshot = null;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            db.GameSessions.Attach(session);
            db.Entry(session).Property(s => s.EndedAt).IsModified = true;
            db.Entry(session).Property(s => s.DurationSeconds).IsModified = true;
            db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.Now,
                Module = "GameMode",
                Action = "game.session.end",
                Target = session.GameName,
                Message = $"时长 {session.DurationSeconds} 秒",
                Result = "success",
                User = Environment.UserName,
            });
            await db.SaveChangesAsync(cancellationToken);

            _currentSession = null;
            Log.Information("游戏会话结束：{Game}，时长 {Duration}s", session.GameName, session.DurationSeconds);
            SessionEnded?.Invoke(this, session);

            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<GameSession>> GetHistoryAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.GameSessions
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // 应用退出时结束进行中的会话（保存记录）
        if (_currentSession is not null)
        {
            await EndCurrentSessionAsync();
        }

        _gate.Dispose();
    }
}
