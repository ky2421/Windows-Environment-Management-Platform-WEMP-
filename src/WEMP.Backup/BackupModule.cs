using WEMP.Core.Abstractions;

namespace WEMP.Backup;

/// <summary>备份与恢复模块（占位：业务功能在后续阶段开发）。</summary>
public sealed class BackupModule : IModule
{
    public string Name => "WEMP.Backup";
    public string DisplayName => "备份与恢复";
    public Version Version => new(0, 1, 0);
    public IReadOnlyList<PageRegistration> Pages => [];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
