using Microsoft.EntityFrameworkCore;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Services;
using WEMP.PackageManagement.Winget;
using WEMP.Optimization.Execution;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;

namespace WEMP.Integration.Tests;

/// <summary>
/// 集成测试：跨模块审计日志协作。
/// 多个模块（Optimization / PackageManagement）向同一 audit_logs 表写入，
/// 验证记录共存、模块过滤与结果标记正确。
/// </summary>
public class AuditCrossModuleTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private sealed class FakeProvider : IPackageProvider
    {
        public Task<List<WingetPackage>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(new List<WingetPackage>
            {
                new WingetPackage("TestPackage", "test.pkg", "1.2.3", null, "winget"),
            });

        public Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken)
            => Task.FromResult(new List<WingetPackage>());

        public Task<CommandResult> InstallAsync(string packageId, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));

        public Task<CommandResult> UninstallAsync(string packageId, CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));

        public Task<CommandResult> UpgradeAllAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CommandResult(0, "ok", 0));
    }

    [Fact]
    public async Task 软件同步_写入包管理模块审计()
    {
        var provider = new FakeProvider();
        var service = new PackageManagerService(_db.CreateFactory(), provider);

        var count = await service.SyncInstalledAsync();

        Assert.Equal(1, count);
        await using var context = _db.CreateContext();
        var logs = await context.AuditLogs
            .Where(l => l.Module == "PackageManagement")
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
        Assert.Single(logs);
        Assert.Equal("software.sync", logs[0].Action);
        Assert.Equal("success", logs[0].Result);
        Assert.Equal(Environment.UserName, logs[0].User);
    }

    [Fact]
    public async Task 多模块审计_按模块过滤互不干扰()
    {
        // 优化模块：种子 + 假执行器 → 一键优化产生审计
        await new OptimizationSeedService(_db.CreateFactory()).EnsureSeedAsync();
        await using (var ctx = _db.CreateContext())
        {
            var categories = await ctx.OptimizationItems.AsNoTracking().Select(i => i.Category).Distinct().ToListAsync();
            var factory = new OptimizationActionFactory(categories.Select(c => new FakeAction(c.ToLowerInvariant())));
            var optService = new OptimizationService(_db.CreateFactory(), factory);
            await optService.ApplyOneKeyAsync();
        }

        // 软件模块：同步产生审计
        var provider = new FakeProvider();
        var pkgService = new PackageManagerService(_db.CreateFactory(), provider);
        await pkgService.SyncInstalledAsync();

        var modules = await _db.CreateContext().AuditLogs
            .Select(l => l.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();
        Assert.Contains("Optimization", modules);
        Assert.Contains("PackageManagement", modules);

        // 按模块过滤互不干扰
        var optOnly = await _db.CreateContext().AuditLogs
            .CountAsync(l => l.Module == "Optimization" && l.Module != "PackageManagement");
        Assert.True(optOnly > 0);
        var pkgOnly = await _db.CreateContext().AuditLogs
            .CountAsync(l => l.Module == "PackageManagement");
        Assert.Equal(1, pkgOnly);
    }
}
