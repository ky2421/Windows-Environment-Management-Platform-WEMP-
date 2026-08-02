using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Winget;

namespace WEMP.PackageManagement.Services;

/// <summary>软件包管理服务实现：winget 驱动，操作写入 package_operations 并审计。</summary>
public sealed class PackageManagerService(
    WempDbContext db,
    IPackageProvider provider) : IPackageManagerService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    // 操作详情保留中文输出（winget 输出可能含中文软件名）
    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<int> SyncInstalledAsync(CancellationToken cancellationToken = default)
    {
        var packages = await provider.ListAsync(cancellationToken);

        db.InstalledSoftware.RemoveRange(db.InstalledSoftware.Where(s => s.Source == "winget"));
        var now = DateTime.Now;
        foreach (var package in packages.Where(p => !string.IsNullOrEmpty(p.Id)))
        {
            db.InstalledSoftware.Add(new InstalledSoftware
            {
                Name = string.IsNullOrEmpty(package.Name) ? package.Id : package.Name,
                Version = package.Version,
                Source = "winget",
                PackageId = package.Id,
                DetectedAt = now,
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = now,
            Module = "PackageManagement",
            Action = "software.sync",
            Target = "winget",
            Message = $"同步 {packages.Count} 条",
            Result = "success",
            User = Environment.UserName,
        });
        await db.SaveChangesAsync(cancellationToken);

        Log.Information("软件清单同步完成：{Count} 条", packages.Count);
        return packages.Count;
    }

    public async Task<IReadOnlyList<InstalledSoftware>> GetInstalledAsync(
        string? search = null, CancellationToken cancellationToken = default)
    {
        IQueryable<InstalledSoftware> query = db.InstalledSoftware
            .Where(s => s.Source == "winget")
            .OrderBy(s => s.Name);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.Name.Contains(search) || (s.PackageId ?? "").Contains(search));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken = default)
        => await provider.GetUpgradableAsync(cancellationToken);

    public async Task<PackageOperation> InstallAsync(
        string packageId, string? version = null, CancellationToken cancellationToken = default)
        => await ExecuteOperationAsync("install", packageId, version,
            ct => provider.InstallAsync(packageId, ct), cancellationToken);

    public async Task<PackageOperation> UninstallAsync(string packageId, CancellationToken cancellationToken = default)
        => await ExecuteOperationAsync("uninstall", packageId, null,
            ct => provider.UninstallAsync(packageId, ct), cancellationToken);

    public async Task<PackageOperation> UpgradeAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var operation = new PackageOperation
            {
                Action = "upgrade-all",
                Provider = "winget",
                PackageId = "*",
                StartedAt = DateTime.Now,
                Result = "pending",
            };
            db.PackageOperations.Add(operation);
            await db.SaveChangesAsync(cancellationToken);

            var result = await provider.UpgradeAllAsync(cancellationToken);
            await FinalizeOperationAsync(operation, result, "software.upgrade-all", cancellationToken);
            return operation;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PackageOperation>> GetOperationsAsync(
        int count, CancellationToken cancellationToken = default)
        => await db.PackageOperations
            .OrderByDescending(o => o.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    private async Task<PackageOperation> ExecuteOperationAsync(
        string action,
        string packageId,
        string? version,
        Func<CancellationToken, Task<CommandResult>> executor,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var operation = new PackageOperation
            {
                Action = action,
                Provider = "winget",
                PackageId = packageId,
                RequestedVersion = version,
                StartedAt = DateTime.Now,
                Result = "pending",
            };
            db.PackageOperations.Add(operation);
            await db.SaveChangesAsync(cancellationToken);

            var result = await executor(cancellationToken);
            await FinalizeOperationAsync(operation, result, $"software.{action}", cancellationToken);
            return operation;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task FinalizeOperationAsync(
        PackageOperation operation, CommandResult result, string auditAction, CancellationToken cancellationToken)
    {
        operation.FinishedAt = DateTime.Now;
        operation.Result = result.Success ? "success" : "failed";
        operation.ExitCode = result.ExitCode;
        operation.DetailJson = JsonSerializer.Serialize(
            new { output = result.Output, durationSeconds = result.DurationSeconds },
            DetailJsonOptions);

        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.Now,
            Module = "PackageManagement",
            Action = auditAction,
            Target = operation.PackageId,
            Message = result.Success ? "成功" : "失败",
            Result = result.Success ? "success" : "failed",
            User = Environment.UserName,
        });
        await db.SaveChangesAsync(cancellationToken);

        Log.Information("软件操作完成：{Action} {Package} 结果={Result} 退出码={ExitCode}",
            operation.Action, operation.PackageId, operation.Result, operation.ExitCode);
    }
}
