using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;

namespace WEMP.PackageManagement.Winget;

/// <summary>软件包源抽象（当前实现：winget CLI）。</summary>
public interface IPackageProvider
{
    /// <summary>列出已安装软件。</summary>
    Task<List<WingetPackage>> ListAsync(CancellationToken cancellationToken);

    /// <summary>列出可升级软件。</summary>
    Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken);

    /// <summary>安装指定包。</summary>
    Task<CommandResult> InstallAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>卸载指定包。</summary>
    Task<CommandResult> UninstallAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>升级全部可升级软件。</summary>
    Task<CommandResult> UpgradeAllAsync(CancellationToken cancellationToken);
}
