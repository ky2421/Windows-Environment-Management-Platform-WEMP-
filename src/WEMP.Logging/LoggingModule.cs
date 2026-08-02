using WEMP.Core.Abstractions;
using WEMP.Logging.UI;

namespace WEMP.Logging;

/// <summary>
/// 日志中心模块：审计日志查询与统计、Windows 事件日志聚合、
/// 异常规则扫描（崩溃/意外关机/错误风暴/审计失败率）与异常处置。
/// </summary>
public sealed class LoggingModule : IModule
{
    public string Name => "WEMP.Logging";

    public string DisplayName => "日志中心";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "logging",
            "日志中心",
            typeof(LoggingPageViewModel),
            typeof(LoggingPage),
            Order: 5),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
