using Microsoft.EntityFrameworkCore;
using WEMP.PackageManagement.Services;
using WEMP.PackageManagement.Winget;

// 真实 winget 集成验证：解析 list 输出 + 同步到数据库
var cli = new WingetCli();
Console.WriteLine($"winget 路径: {cli.ExePath}");

var packages = await cli.ListAsync(CancellationToken.None);
Console.WriteLine($"解析条数: {packages.Count}");
foreach (var p in packages.Take(3))
{
    Console.WriteLine($"  [{p.Source}] {p.Name} | {p.Id} | {p.Version} | 可用={p.Available ?? "-"}");
}

var upgradable = await cli.GetUpgradableAsync(CancellationToken.None);
Console.WriteLine($"可升级条数: {upgradable.Count}");
foreach (var p in upgradable.Take(3))
{
    Console.WriteLine($"  {p.Name} {p.Version} -> {p.Available}");
}

// 真实数据库同步
var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WEMP.Infrastructure.Data.WempDbContext>()
    .UseSqlite(WEMP.Infrastructure.Data.WempDatabase.CreateConnectionString())
    .Options;
using var db = new WEMP.Infrastructure.Data.WempDbContext(options);
var service = new PackageManagerService(db, cli);
var synced = await service.SyncInstalledAsync(CancellationToken.None);
Console.WriteLine($"数据库同步: {synced} 条（installed_software 共 {db.InstalledSoftware.Count(s => s.Source == "winget")} 条）");
