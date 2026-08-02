using WEMP.Infrastructure.Data.Entities;
using WEMP.PackageManagement.Models;

namespace WEMP.PackageManagement.Services;

/// <summary>软件包管理服务：已装软件同步与安装/卸载/升级操作。</summary>
public interface IPackageManagerService
{
    /// <summary>从 winget 同步已安装软件清单，返回同步条数。</summary>
    Task<int> SyncInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>查询已安装软件（可选搜索关键字）。</summary>
    Task<IReadOnlyList<InstalledSoftware>> GetInstalledAsync(string? search = null, CancellationToken cancellationToken = default);

    /// <summary>查询可升级软件。</summary>
    Task<IReadOnlyList<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken = default);

    /// <summary>安装软件（同步执行，完成后写入操作记录）。</summary>
    Task<PackageOperation> InstallAsync(string packageId, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>卸载软件。</summary>
    Task<PackageOperation> UninstallAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>升级全部可升级软件。</summary>
    Task<PackageOperation> UpgradeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>最近 N 条操作记录。</summary>
    Task<IReadOnlyList<PackageOperation>> GetOperationsAsync(int count, CancellationToken cancellationToken = default);
}
