using WEMP.Core.Abstractions;
using WEMP.PackageManagement.UI;

namespace WEMP.PackageManagement;

/// <summary>
/// 软件包管理模块：基于 winget 的已安装软件同步、安装/卸载/升级、
/// 软件分组批量安装；操作写入 package_operations 并审计。
/// </summary>
public sealed class PackageManagementModule : IModule
{
    public string Name => "WEMP.PackageManagement";

    public string DisplayName => "软件包管理";

    public Version Version => new(0, 2, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "packages",
            "软件包管理",
            typeof(PackageManagementPageViewModel),
            typeof(PackageManagementPage),
            Order: 3),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
