using WEMP.Backup.UI;
using WEMP.Core.Abstractions;

namespace WEMP.Backup;

/// <summary>
/// 备份恢复模块：任务化全量/增量文件备份（glob 过滤、自动备份）、
/// 备份记录与文件条目管理、按记录还原。
/// </summary>
public sealed class BackupModule : IModule
{
    public string Name => "WEMP.Backup";

    public string DisplayName => "备份恢复";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "backup",
            "备份恢复",
            typeof(BackupPageViewModel),
            typeof(BackupPage),
            Order: 6),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
