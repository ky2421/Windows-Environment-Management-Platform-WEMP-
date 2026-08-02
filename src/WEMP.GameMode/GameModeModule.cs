using WEMP.Core.Abstractions;
using WEMP.GameMode.UI;

namespace WEMP.GameMode;

/// <summary>
/// 游戏模式模块：游戏会话检测（自动/手动）与系统状态切换
/// （进入游戏切换高性能电源、释放后台进程，退出自动恢复），会话记录持久化。
/// </summary>
public sealed class GameModeModule : IModule
{
    public string Name => "WEMP.GameMode";

    public string DisplayName => "游戏模式";

    public Version Version => new(0, 2, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "gamemode",
            "游戏模式",
            typeof(GameModePageViewModel),
            typeof(GameModePage),
            Order: 2),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
