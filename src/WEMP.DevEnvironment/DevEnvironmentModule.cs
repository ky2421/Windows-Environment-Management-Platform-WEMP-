using WEMP.Core.Abstractions;

namespace WEMP.DevEnvironment;

/// <summary>开发环境模块（占位：业务功能在后续阶段开发）。</summary>
public sealed class DevEnvironmentModule : IModule
{
    public string Name => "WEMP.DevEnvironment";
    public string DisplayName => "开发环境";
    public Version Version => new(0, 1, 0);
    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
