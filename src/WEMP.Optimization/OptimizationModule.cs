using WEMP.Core.Abstractions;

namespace WEMP.Optimization;

/// <summary>系统优化模块（占位：业务功能在后续阶段开发）。</summary>
public sealed class OptimizationModule : IModule
{
    public string Name => "WEMP.Optimization";
    public string DisplayName => "系统优化";
    public Version Version => new(0, 1, 0);
    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
