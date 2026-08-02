using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Backup.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

// 临时目录全流程（不触碰真实用户数据）
var root = Path.Combine(Path.GetTempPath(), $"wemp-probe-{Guid.NewGuid():N}");
var source = Path.Combine(root, "src");
var dest = Path.Combine(root, "dest");
var restore = Path.Combine(root, "restore");
Directory.CreateDirectory(Path.Combine(source, "docs"));
Directory.CreateDirectory(dest);
Directory.CreateDirectory(restore);

File.WriteAllText(Path.Combine(source, "readme.txt"), "文档 v1");
File.WriteAllText(Path.Combine(source, "docs", "notes.md"), "笔记 v1");
File.WriteAllText(Path.Combine(source, "cache.tmp"), "临时");

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<WempDbContext>().UseSqlite(connection).Options;
var db = new WempDbContext(options);
db.Database.EnsureCreated();
var service = new BackupService(db);

try
{
    // 1. 任务：增量模式 + 排除 *.tmp
    var task = await service.CreateTaskAsync(new BackupTask
    {
        Name = "探针任务",
        SourcePath = source,
        DestinationPath = dest,
        Mode = "incremental",
        ExcludePatterns = "**/*.tmp",
    });
    Console.WriteLine($"任务已创建：{task.Name} [{task.Mode}] 排除 **/*.tmp");

    // 2. 全量备份
    var full = await service.RunBackupAsync(task.Id);
    Console.WriteLine($"全量备份：状态 {full.Status}，文件 {full.FileCount}，字节 {full.TotalBytes}（期望 2，tmp 被排除）");

    // 3. 增量：改 1 个 + 新增 1 个
    File.WriteAllText(Path.Combine(source, "readme.txt"), "文档 v2");
    File.SetLastWriteTimeUtc(Path.Combine(source, "readme.txt"), DateTime.UtcNow.AddSeconds(30));
    File.WriteAllText(Path.Combine(source, "docs", "new.md"), "新增");
    var inc = await service.RunBackupAsync(task.Id);
    Console.WriteLine($"增量备份：状态 {inc.Status}，类型 {inc.BackupType}，文件 {inc.FileCount}（期望 2）");
    var entries = await service.GetRecordEntriesAsync(inc.Id);
    foreach (var e in entries)
    {
        Console.WriteLine($"  条目 {e.Action} {e.RelativePath}");
    }

    // 4. 还原到自定义目录
    var result = await service.RestoreAsync(inc.Id, restore);
    Console.WriteLine($"还原：{result.FileCount} 个文件 → {result.TargetPath}");
    Console.WriteLine($"还原内容校验：readme.txt = '{File.ReadAllText(Path.Combine(restore, "readme.txt"))}'，new.md 存在 = {File.Exists(Path.Combine(restore, "docs", "new.md"))}");

    // 5. 自动备份到期执行
    var auto = await service.CreateTaskAsync(new BackupTask
    {
        Name = "自动任务",
        SourcePath = source,
        DestinationPath = dest,
        Mode = "incremental",
        AutoBackup = true,
        AutoIntervalHours = 1,
    });
    var ran = await service.RunDueAutoBackupsAsync();
    Console.WriteLine($"自动备份执行任务数：{ran}（期望 1）");
    var autoRecords = await service.GetRecordsAsync(auto.Id);
    Console.WriteLine($"自动任务记录：{autoRecords.Count} 条（期望 1）");
    var autoTask = await service.GetTaskAsync(auto.Id);
    Console.WriteLine($"任务上次备份时间已更新：{autoTask?.LastBackupAt:yyyy-MM-dd HH:mm:ss}");

    // 6. 删除任务（级联删记录）
    var deleted = await service.DeleteTaskAsync(task.Id);
    Console.WriteLine($"删除任务：{deleted}，库内记录 {db.BackupRecords.Count()} 条（期望 1，仅自动任务）");
}
finally
{
    Directory.Delete(root, recursive: true);
    await db.DisposeAsync();
}

// 真实库只读检查（应用迁移 = App 启动行为，仅建表不改数据）
var realOptions = new DbContextOptionsBuilder<WempDbContext>()
    .UseSqlite(WempDatabase.CreateConnectionString())
    .Options;
using var realDb = new WempDbContext(realOptions);
realDb.Database.Migrate();
Console.WriteLine($"真实库 backup_tasks：{realDb.BackupTasks.Count()} 条（迁移已应用，未写入业务数据）");
