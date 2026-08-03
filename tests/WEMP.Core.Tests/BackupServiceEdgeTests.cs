using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Backup.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Core.Tests;

/// <summary>
/// 备份服务边界测试：缺失任务/记录、空备份、目标文件缺失、不覆盖还原与增量零变更。
/// </summary>
public class BackupServiceEdgeTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;

    public BackupServiceEdgeTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), $"wemp-src-{Guid.NewGuid():N}");
        _destDir = Path.Combine(Path.GetTempPath(), $"wemp-dest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir))
        {
            Directory.Delete(_sourceDir, recursive: true);
        }

        if (Directory.Exists(_destDir))
        {
            Directory.Delete(_destDir, recursive: true);
        }
    }

    private static (WempDbContext Db, BackupService Service) CreateHarness()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return (db, new BackupService(new TestDbFactory(connection)));
    }

    private BackupTask CreateTask(string mode = "full")
        => new()
        {
            Name = "边界测试",
            SourcePath = _sourceDir,
            DestinationPath = _destDir,
            Mode = mode,
        };

    [Fact]
    public async Task RunBackup_throws_when_task_missing()
    {
        var (_, service) = CreateHarness();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunBackupAsync(999));
        Assert.Contains("备份任务不存在", ex.Message);
    }

    [Fact]
    public async Task Restore_throws_when_record_missing()
    {
        var (_, service) = CreateHarness();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(999));
        Assert.Contains("备份记录不存在", ex.Message);
    }

    [Fact]
    public async Task Restore_throws_when_record_has_no_entries()
    {
        var (_, service) = CreateHarness();
        var task = await service.CreateTaskAsync(CreateTask());

        // 空源目录：备份成功但 0 个条目
        var record = await service.RunBackupAsync(task.Id);
        Assert.Equal(0, record.FileCount);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(record.Id));
        Assert.Contains("没有可还原的文件", ex.Message);
    }

    [Fact]
    public async Task Restore_skips_missing_backup_files_without_throwing()
    {
        var (_, service) = CreateHarness();
        var file = Path.Combine(_sourceDir, "a.txt");
        File.WriteAllText(file, "AAA");

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);
        Assert.Equal(1, record.FileCount);

        // 备份目录被整体删除 → 还原时跳过缺失文件，不抛异常
        var destRoot = Path.Combine(_destDir, $"{task.Id}_边界测试");
        Directory.Delete(destRoot, recursive: true);

        var result = await service.RestoreAsync(record.Id);

        Assert.Equal(0, result.FileCount);
    }

    [Fact]
    public async Task Restore_without_overwrite_keeps_existing_target()
    {
        var (_, service) = CreateHarness();
        var file = Path.Combine(_sourceDir, "a.txt");
        File.WriteAllText(file, "AAA");

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);

        // 源文件被修改后还原（overwrite: false）→ 保留新内容
        File.WriteAllText(file, "NEW");
        var skipped = await service.RestoreAsync(record.Id, overwrite: false);
        Assert.Equal(0, skipped.FileCount);
        Assert.Equal("NEW", File.ReadAllText(file));

        // overwrite: true → 覆盖为备份内容
        var restored = await service.RestoreAsync(record.Id, overwrite: true);
        Assert.Equal(1, restored.FileCount);
        Assert.Equal("AAA", File.ReadAllText(file));
    }

    [Fact]
    public async Task Incremental_backup_with_no_changes_produces_zero_files()
    {
        var (_, service) = CreateHarness();
        var file = Path.Combine(_sourceDir, "a.txt");
        File.WriteAllText(file, "AAA");

        var task = await service.CreateTaskAsync(CreateTask(mode: "incremental"));
        var first = await service.RunBackupAsync(task.Id);
        Assert.Equal(1, first.FileCount);

        // 文件时间戳推到过去，确保晚于首次备份结束时间 → 全部跳过
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-1));
        var second = await service.RunBackupAsync(task.Id);

        Assert.Equal(0, second.FileCount);
        Assert.Empty(await service.GetRecordEntriesAsync(second.Id));
    }

    [Fact]
    public async Task CreateTask_rejects_empty_name()
    {
        var (_, service) = CreateHarness();
        var task = CreateTask();
        task.Name = "   ";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTaskAsync(task));
        Assert.Contains("任务名称不能为空", ex.Message);
    }

    [Fact]
    public async Task CreateTask_rejects_empty_destination()
    {
        var (_, service) = CreateHarness();
        var task = CreateTask();
        task.DestinationPath = string.Empty;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTaskAsync(task));
        Assert.Contains("目标路径不能为空", ex.Message);
    }

    [Fact]
    public async Task CreateTask_rejects_invalid_mode()
    {
        var (_, service) = CreateHarness();
        var task = CreateTask(mode: "bogus");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTaskAsync(task));
        Assert.Contains("备份模式必须是 full 或 incremental", ex.Message);
    }

    [Fact]
    public async Task UpdateTask_returns_null_when_task_missing()
    {
        var (_, service) = CreateHarness();

        var result = await service.UpdateTaskAsync(CreateTask());
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteRecord_returns_false_when_record_missing()
    {
        var (_, service) = CreateHarness();

        Assert.False(await service.DeleteRecordAsync(999));
    }

    [Fact]
    public async Task DeleteTask_returns_false_when_task_missing()
    {
        var (_, service) = CreateHarness();

        Assert.False(await service.DeleteTaskAsync(999));
    }
}
