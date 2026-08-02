using WEMP.Core.Abstractions;

namespace WEMP.PackageManagement;

/// <summary>软件包管理模块（占位：业务功能在后续阶段开发）。</summary>
public sealed class PackageManagementModule : IModule
{
    public string Name => "WEMP.PackageManagement";
    public string DisplayName => "软件包管理";
    public Version Version => new(0, 1, 0);
    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
