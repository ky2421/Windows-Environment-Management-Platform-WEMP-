using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Backup.Services;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Core.Tests;

/// <summary>
/// 备份服务测试：内存库 + 临时目录做真实文件复制（全量/增量/还原/过滤）。
/// </summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;

    public BackupServiceTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), $"wemp-src-{Guid.NewGuid():N}");
        _destDir = Path.Combine(Path.GetTempPath(), $"wemp-dest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_sourceDir, "sub"));
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
        return (db, new BackupService(db));
    }

    private void CreateFiles(params (string RelativePath, string Content)[] files)
    {
        foreach (var (relative, content) in files)
        {
            var path = Path.Combine(_sourceDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    private BackupTask CreateTask(string mode = "full", string? include = null, string? exclude = null, bool auto = false, int intervalHours = 24)
        => new()
        {
            Name = "测试任务",
            SourcePath = _sourceDir,
            DestinationPath = _destDir,
            Mode = mode,
            IncludePatterns = include,
            ExcludePatterns = exclude,
            AutoBackup = auto,
            AutoIntervalHours = intervalHours,
        };

    [Fact]
    public async Task Full_backup_copies_all_files_and_records_entries()
    {
        var (db, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"), ("b.log", "BBB"), ("sub/c.txt", "CCC"));

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);

        Assert.Equal("success", record.Status);
        Assert.Equal("full", record.BackupType);
        Assert.Equal(3, record.FileCount);
        Assert.True(record.TotalBytes > 0);

        var entries = await service.GetRecordEntriesAsync(record.Id);
        Assert.Equal(3, entries.Count);
        Assert.Contains(entries, e => e.RelativePath == "a.txt");
        Assert.Contains(entries, e => e.RelativePath == "sub/c.txt");
        Assert.All(entries, e => Assert.Equal("new", e.Action));

        // 目标目录文件真实存在
        var destDir = Path.Combine(_destDir, $"{task.Id}_测试任务");
        var saved = Directory.EnumerateFiles(destDir, "*", SearchOption.AllDirectories).ToList();
        Assert.Equal(3, saved.Count);
    }

    [Fact]
    public async Task Incremental_backup_copies_only_changed_and_new_files()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"), ("b.txt", "BBB"), ("c.txt", "CCC"));

        var task = await service.CreateTaskAsync(CreateTask(mode: "incremental"));
        var first = await service.RunBackupAsync(task.Id);
        Assert.Equal(3, first.FileCount);

        // 修改 a.txt（时间戳推到未来），新增 d.txt，删除 b.txt
        File.SetLastWriteTimeUtc(Path.Combine(_sourceDir, "a.txt"), DateTime.UtcNow.AddSeconds(30));
        File.WriteAllText(Path.Combine(_sourceDir, "d.txt"), "DDD");

        var second = await service.RunBackupAsync(task.Id);

        Assert.Equal("incremental", second.BackupType);
        Assert.Equal(2, second.FileCount);
        var entries = await service.GetRecordEntriesAsync(second.Id);
        Assert.Contains(entries, e => e.RelativePath == "a.txt" && e.Action == "changed");
        Assert.Contains(entries, e => e.RelativePath == "d.txt" && e.Action == "changed");
        Assert.DoesNotContain(entries, e => e.RelativePath == "b.txt");
    }

    [Fact]
    public async Task Restore_recovers_deleted_files_with_content()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"), ("sub/b.txt", "BBB"));

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);

        // 删除源文件后还原
        File.Delete(Path.Combine(_sourceDir, "a.txt"));
        Directory.Delete(Path.Combine(_sourceDir, "sub"), recursive: true);

        var result = await service.RestoreAsync(record.Id);

        Assert.Equal(2, result.FileCount);
        Assert.Equal("AAA", File.ReadAllText(Path.Combine(_sourceDir, "a.txt")));
        Assert.Equal("BBB", File.ReadAllText(Path.Combine(_sourceDir, "sub", "b.txt")));
    }

    [Fact]
    public async Task Restore_to_custom_directory()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"));

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);

        var restoreDir = Path.Combine(_sourceDir, "..", $"restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(restoreDir);
        try
        {
            var result = await service.RestoreAsync(record.Id, restoreDir);
            Assert.Equal(1, result.FileCount);
            Assert.Equal("AAA", File.ReadAllText(Path.Combine(restoreDir, "a.txt")));
        }
        finally
        {
            Directory.Delete(restoreDir, recursive: true);
        }
    }

    [Fact]
    public async Task Include_exclude_globs_filter_files()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("keep.txt", "K"), ("skip.tmp", "S"), ("sub/keep2.txt", "K2"), ("sub/skip2.log", "S2"));

        var task = await service.CreateTaskAsync(CreateTask(include: "**/*.txt", exclude: "**/skip*"));
        var record = await service.RunBackupAsync(task.Id);

        Assert.Equal("success", record.Status);
        var entries = await service.GetRecordEntriesAsync(record.Id);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.RelativePath == "keep.txt");
        Assert.Contains(entries, e => e.RelativePath == "sub/keep2.txt");
    }

    [Fact]
    public async Task CreateTask_rejects_missing_source()
    {
        var (_, service) = CreateHarness();
        var task = CreateTask();
        task.SourcePath = Path.Combine(_sourceDir, "missing");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTaskAsync(task));
        Assert.Contains("不存在", ex.Message);
    }

    [Fact]
    public async Task RunBackup_marks_failed_when_destination_invalid()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"));

        var task = CreateTask();
        task.DestinationPath = Path.Combine(_destDir, "no", "such", "dir");
        task = await service.CreateTaskAsync(task);
        // 目标目录会自动创建，改为不可写场景：用文件路径占位
        Directory.CreateDirectory(_destDir);
        var blocker = Path.Combine(_destDir, "block");
        File.WriteAllText(blocker, "x");

        var bad = CreateTask();
        bad.DestinationPath = blocker; // 已存在文件，无法作为目录
        bad = await service.CreateTaskAsync(bad);

        var record = await service.RunBackupAsync(bad.Id);
        Assert.Equal("failed", record.Status);
        Assert.NotNull(record.Error);
    }

    [Fact]
    public async Task RunDueAutoBackups_runs_only_due_tasks()
    {
        var (_, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"));

        var due = await service.CreateTaskAsync(CreateTask(auto: true, intervalHours: 1)); // 从未备份 → 到期
        var notDue = await service.CreateTaskAsync(CreateTask(auto: true, intervalHours: 24));
        notDue.LastBackupAt = DateTime.Now.AddHours(-1);
        await service.UpdateTaskAsync(notDue);
        var disabled = await service.CreateTaskAsync(CreateTask(auto: true));
        disabled.Enabled = false;
        await service.UpdateTaskAsync(disabled);

        var ran = await service.RunDueAutoBackupsAsync();

        Assert.Equal(1, ran);
        var records = await service.GetRecordsAsync(due.Id);
        Assert.Single(records);
        Assert.Empty(await service.GetRecordsAsync(notDue.Id));
        Assert.Empty(await service.GetRecordsAsync(disabled.Id));
    }

    [Fact]
    public async Task DeleteTask_cascades_records_and_entries()
    {
        var (db, service) = CreateHarness();
        CreateFiles(("a.txt", "AAA"));

        var task = await service.CreateTaskAsync(CreateTask());
        var record = await service.RunBackupAsync(task.Id);

        var deleted = await service.DeleteTaskAsync(task.Id);
        Assert.True(deleted);
        Assert.Equal(0, await db.BackupRecords.CountAsync());
        Assert.Equal(0, await db.BackupFileEntries.CountAsync());
        Assert.Equal(0, await db.BackupTasks.CountAsync());
    }

    [Fact]
    public async Task Single_file_source_backup_and_restore()
    {
        var (_, service) = CreateHarness();
        var file = Path.Combine(_sourceDir, "solo.txt");
        File.WriteAllText(file, "SOLO");

        var task = CreateTask();
        task.SourcePath = file;
        task = await service.CreateTaskAsync(task);

        var record = await service.RunBackupAsync(task.Id);
        Assert.Equal(1, record.FileCount);

        File.Delete(file);
        var result = await service.RestoreAsync(record.Id);
        Assert.Equal(1, result.FileCount);
        Assert.Equal("SOLO", File.ReadAllText(file));
    }

    [Fact]
    public void Glob_matches_segments_and_recursive()
    {
        Assert.True(GlobMatcher.IsMatch("a/b/c.txt", ["**/*.txt"]));
        Assert.True(GlobMatcher.IsMatch("a.txt", ["*.txt"]));
        Assert.False(GlobMatcher.IsMatch("a/b.txt", ["*.txt"])); // * 不跨段
        Assert.True(GlobMatcher.IsMatch("src/main.cs", ["**/main.*"]));
        Assert.False(GlobMatcher.IsMatch("src/main.cs", ["**/main.csx"]));
        Assert.True(GlobMatcher.IsMatch("data/2024/report.csv", ["data/**"]));
    }
}
