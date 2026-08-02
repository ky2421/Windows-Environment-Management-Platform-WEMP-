using WEMP.Core.Abstractions;

namespace WEMP.Logging;

/// <summary>日志中心模块（占位：业务功能在后续阶段开发）。</summary>
public sealed class LoggingModule : IModule
{
    public string Name => "WEMP.Logging";
    public string DisplayName => "日志中心";
    public Version Version => new(0, 1, 0);
    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
