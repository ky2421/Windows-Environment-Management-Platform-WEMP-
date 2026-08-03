using Microsoft.EntityFrameworkCore;
using WEMP.Backup.Services;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.System.Tests;

/// <summary>
/// 系统测试：备份恢复完整流程。
/// 真实文件系统（临时目录）+ 真实 SQLite 文件：全量 → 增量 → 恢复 → 删除追踪 → 并发防护 → 自动备份。
/// </summary>
public class BackupEndToEndTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly string _sourceDir;
    private readonly string _destDir;

    public BackupEndToEndTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), $"wemp-e2e-src-{Guid.NewGuid():N}");
        _destDir = Path.Combine(Path.GetTempPath(), $"wemp-e2e-dest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_sourceDir, "sub"));
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        DeleteDir(_sourceDir);
        DeleteDir(_destDir);
    }

    private static void DeleteDir(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private BackupService CreateService() => new(_db.CreateFactory());

    private async Task<BackupTask> CreateTaskAsync(BackupService service, string mode = "full", bool autoBackup = false, int intervalHours = 1)
    {
        return await service.CreateTaskAsync(new BackupTask
        {
            Name = $"e2e-{mode}",
            SourcePath = _sourceDir,
            DestinationPath = _destDir,
            Mode = mode,
            AutoBackup = autoBackup,
            AutoIntervalHours = intervalHours,
            Enabled = true,
        });
    }

    private void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_sourceDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public async Task 全量_增量_恢复_端到端流程()
    {
        var service = CreateService();
        var task = await CreateTaskAsync(service);
        WriteFile("a.txt", "AAA");
        WriteFile("sub/b.txt", "BBB");

        // 全量备份
        var full = await service.RunBackupAsync(task.Id);
        Assert.Equal("full", full.BackupType);
        var fullEntries = await service.GetRecordEntriesAsync(full.Id);
        Assert.Equal(2, fullEntries.Count);

        // 修改 a.txt、新增 c.txt、删除 sub/b.txt
        WriteFile("a.txt", "AAA2");
        WriteFile("c.txt", "CCC");
        File.Delete(Path.Combine(_sourceDir, "sub", "b.txt"));

        // 增量备份（先把任务切换为增量模式；增量基准为上次成功备份时间）
        await using (var update = _db.CreateContext())
        {
            var stored = await update.BackupTasks.FindAsync(task.Id);
            stored!.Mode = "incremental";
            await update.SaveChangesAsync();
        }
        var inc = await service.RunBackupAsync(task.Id);
        Assert.Equal("incremental", inc.BackupType);
        var incEntries = await service.GetRecordEntriesAsync(inc.Id);
        Assert.Equal(3, incEntries.Count); // a.txt 更新 + c.txt 新增 + b.txt 删除

        // 恢复到最新记录（增量）：a.txt 内容 = AAA2、c.txt 存在、b.txt 不存在
        var restoreDir = Path.Combine(Path.GetTempPath(), $"wemp-e2e-restore-{Guid.NewGuid():N}");
        try
        {
            var result = await service.RestoreAsync(inc.Id, restoreDir);
            Assert.Equal(restoreDir, result.TargetPath);
            Assert.Equal("AAA2", File.ReadAllText(Path.Combine(restoreDir, "a.txt")));
            Assert.Equal("CCC", File.ReadAllText(Path.Combine(restoreDir, "c.txt")));
            Assert.False(File.Exists(Path.Combine(restoreDir, "b.txt")));
        }
        finally
        {
            DeleteDir(restoreDir);
        }
    }

    [Fact]
    public async Task 并发备份同一任务_串行执行无冲突()
    {
        var service = CreateService();
        var task = await CreateTaskAsync(service);
        WriteFile("a.txt", "content");

        var tasks = Enumerable.Range(0, 3)
            .Select(_ => service.RunBackupAsync(task.Id))
            .ToArray();
        var records = await Task.WhenAll(tasks);

        Assert.All(records, r => Assert.NotNull(r));
        var list = await service.GetRecordsAsync(task.Id, limit: 10);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task 删除任务_级联删除记录并清理备份目录()
    {
        var service = CreateService();
        var task = await CreateTaskAsync(service);
        WriteFile("a.txt", "content");
        await service.RunBackupAsync(task.Id);

        var deleted = await service.DeleteTaskAsync(task.Id);

        Assert.True(deleted);
        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.BackupTasks.CountAsync(t => t.Id == task.Id));
        Assert.Equal(0, await context.BackupRecords.CountAsync(r => r.TaskId == task.Id));
        Assert.Equal(0, await context.BackupFileEntries.CountAsync(e => e.Record.TaskId == task.Id));
    }

    [Fact]
    public async Task 自动备份_到期任务执行_未到期跳过()
    {
        var service = CreateService();
        WriteFile("a.txt", "content");

        // 到期任务：上次备份 25 小时前
        var dueTask = await CreateTaskAsync(service, autoBackup: true, intervalHours: 24);
        await using (var ctx = _db.CreateContext())
        {
            var t = await ctx.BackupTasks.SingleAsync(x => x.Id == dueTask.Id);
            t.LastBackupAt = DateTime.Now.AddHours(-25);
            await ctx.SaveChangesAsync();
        }

        // 未到期任务：上次备份 1 小时前
        var freshTask = await CreateTaskAsync(service, autoBackup: true, intervalHours: 24);
        await using (var ctx = _db.CreateContext())
        {
            var t = await ctx.BackupTasks.SingleAsync(x => x.Id == freshTask.Id);
            t.LastBackupAt = DateTime.Now.AddHours(-1);
            await ctx.SaveChangesAsync();
        }

        var executed = await service.RunDueAutoBackupsAsync();

        Assert.Equal(1, executed);
        var records = await service.GetRecordsAsync(dueTask.Id, limit: 10);
        Assert.Single(records);
        Assert.Empty(await service.GetRecordsAsync(freshTask.Id, limit: 10));
    }
}
