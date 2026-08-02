using WEMP.Core.Abstractions;
using WEMP.SystemInfo.Persistence;
using WEMP.SystemInfo.UI;

namespace WEMP.SystemInfo;

/// <summary>系统检测模块：采集操作系统、硬件与开发环境信息并持久化为快照。</summary>
public sealed class SystemInfoModule : IModule
{
    public string Name => "WEMP.SystemInfo";

    public string DisplayName => "系统检测";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "system-info",
            "系统检测",
            typeof(SystemInfoViewModel),
            typeof(SystemInfoPage),
            Order: 0),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
