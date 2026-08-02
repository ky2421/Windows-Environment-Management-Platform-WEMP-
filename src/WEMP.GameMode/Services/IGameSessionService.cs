using WEMP.GameMode.Detection;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.GameMode.Services;

/// <summary>游戏会话服务：会话状态机、持久化与自动监测开关。</summary>
public interface IGameSessionService
{
    /// <summary>当前进行中的会话；无会话时为 null。</summary>
    GameSession? CurrentSession { get; }

    /// <summary>会话开始时触发（参数为新会话）。</summary>
    event EventHandler<GameSession>? SessionStarted;

    /// <summary>会话结束时触发（参数为已结束会话）。</summary>
    event EventHandler<GameSession>? SessionEnded;

    /// <summary>为指定进程启动会话（进程须为已识别游戏），返回新会话。</summary>
    Task<GameSession?> StartSessionAsync(int processId, CancellationToken cancellationToken = default);

    /// <summary>结束当前会话，返回已结束会话。</summary>
    Task<GameSession?> EndCurrentSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>获取最近 N 条会话记录。</summary>
    Task<IReadOnlyList<GameSession>> GetHistoryAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>自动监测开关（持久化到 app_settings）。</summary>
    bool IsAutoMonitorEnabled { get; set; }
}
