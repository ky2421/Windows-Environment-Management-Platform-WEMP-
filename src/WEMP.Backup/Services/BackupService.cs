using System.IO;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Backup.Services;

/// <summary>备份恢复服务实现。</summary>
public sealed class BackupService : IBackupService
{
    private readonly WempDbContext _db;

    public BackupService(WempDbContext db)
    {
        _db = db;
    }

    public Task<List<BackupTask>> GetTasksAsync(CancellationToken cancellationToken = default)
        => _db.BackupTasks.AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<BackupTask?> GetTaskAsync(long taskId, CancellationToken cancellationToken = default)
        => _db.BackupTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

    public async Task<BackupTask> CreateTaskAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        ValidateTask(task);
        task.CreatedAt = DateTime.Now;
        task.UpdatedAt = task.CreatedAt;
        _db.BackupTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("备份任务已创建：{Name}（源 {Source} → 目标 {Dest}）", task.Name, task.SourcePath, task.DestinationPath);
        return task;
    }

    public async Task<BackupTask?> UpdateTaskAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        ValidateTask(task);
        var existing = await _db.BackupTasks.FindAsync([task.Id], cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        existing.Name = task.Name;
        existing.SourcePath = task.SourcePath;
        existing.DestinationPath = task.DestinationPath;
        existing.Mode = task.Mode;
        existing.IncludePatterns = task.IncludePatterns;
        existing.ExcludePatterns = task.ExcludePatterns;
        existing.AutoBackup = task.AutoBackup;
        existing.AutoIntervalHours = task.AutoIntervalHours;
        existing.Enabled = task.Enabled;
        existing.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    public async Task<bool> DeleteTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.BackupTasks.FindAsync([taskId], cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return false;
        }

        _db.BackupTasks.Remove(task); // 记录/条目级联删除；磁盘备份文件保留
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Log.Information("备份任务已删除：{Name}", task.Name);
        return true;
    }

    public async Task<BackupRecord> RunBackupAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var task = await _db.BackupTasks.FindAsync([taskId], cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"备份任务不存在：{taskId}");

        var record = new BackupRecord
        {
            TaskId = task.Id,
            StartedAt = DateTime.Now,
            Status = "running",
        };
        _db.BackupRecords.Add(record);

        try
        {
            var (files, basePath) = CollectSourceFiles(task);

            // 增量：仅复制修改时间晚于上次成功备份结束时间的文件（统一按 UTC 比较）
            var incremental = task.Mode == "incremental";
            DateTime? baselineUtc = null;
            if (incremental)
            {
                var last = await _db.BackupRecords.AsNoTracking()
                    .Where(r => r.TaskId == task.Id && r.Status == "success")
                    .OrderByDescending(r => r.FinishedAt)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                baselineUtc = last?.FinishedAt?.ToUniversalTime();
            }

            var destinationRoot = Path.Combine(task.DestinationPath, $"{task.Id}_{Sanitize(task.Name)}");
            var destinationDir = Path.Combine(destinationRoot, $"{record.StartedAt:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(destinationDir);

            var copied = 0;
            long totalBytes = 0;
            foreach (var file in files)
            {
                var relative = GetRelativePath(basePath, file);
                if (incremental && baselineUtc.HasValue && File.GetLastWriteTimeUtc(file) <= baselineUtc.Value)
                {
                    continue;
                }

                var target = Path.Combine(destinationDir, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);

                _db.BackupFileEntries.Add(new BackupFileEntry
                {
                    Record = record,
                    RelativePath = relative,
                    FileSize = new FileInfo(file).Length,
                    ModifiedAt = File.GetLastWriteTime(file),
                    Action = incremental ? "changed" : "new",
                });
                copied++;
                totalBytes += new FileInfo(target).Length;
            }

            record.BackupType = incremental ? "incremental" : "full";
            record.FileCount = copied;
            record.TotalBytes = totalBytes;
            record.Status = "success";
            record.FinishedAt = DateTime.Now;
            record.Message = $"已备份 {copied} 个文件（{(incremental ? "增量" : "全量")}）";
            task.LastBackupAt = record.FinishedAt;
            task.UpdatedAt = record.FinishedAt.Value;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("备份完成：任务 {Name} {Type} {Count} 文件 {Bytes} 字节", task.Name, record.BackupType, copied, totalBytes);
            return record;
        }
        catch (Exception ex)
        {
            record.Status = "failed";
            record.Error = ex.Message;
            record.FinishedAt = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Error(ex, "备份失败：任务 {Name}", task.Name);
            return record;
        }
    }

    public Task<List<BackupRecord>> GetRecordsAsync(long taskId, int limit = 100, CancellationToken cancellationToken = default)
        => _db.BackupRecords.AsNoTracking()
            .Where(r => r.TaskId == taskId)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<List<BackupFileEntry>> GetRecordEntriesAsync(long recordId, CancellationToken cancellationToken = default)
        => _db.BackupFileEntries.AsNoTracking()
            .Where(e => e.RecordId == recordId)
            .OrderBy(e => e.RelativePath)
            .ToListAsync(cancellationToken);

    public async Task<RestoreResult> RestoreAsync(long recordId, string? targetPath = null, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var record = await _db.BackupRecords.AsNoTracking()
            .Include(r => r.Task)
            .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException($"备份记录不存在：{recordId}");

        var entries = await _db.BackupFileEntries.AsNoTracking()
            .Where(e => e.RecordId == recordId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("备份记录中没有可还原的文件");
        }

        var sourceDir = Path.Combine(record.Task!.DestinationPath, $"{record.Task.Id}_{Sanitize(record.Task.Name)}", $"{record.StartedAt:yyyyMMdd_HHmmss}");
        var target = targetPath ?? record.Task.SourcePath;
        var targetIsDirectory = Directory.Exists(target) || Path.GetExtension(target).Length == 0;

        var restored = 0;
        foreach (var entry in entries)
        {
            var sourceFile = Path.Combine(sourceDir, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourceFile))
            {
                continue;
            }

            var targetFile = targetIsDirectory
                ? Path.Combine(target, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))
                : target;
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            if (File.Exists(targetFile) && !overwrite)
            {
                continue;
            }

            File.Copy(sourceFile, targetFile, overwrite: true);
            restored++;
        }

        Log.Information("备份还原完成：记录 {RecordId} → {Target}，共 {Count} 个文件", recordId, target, restored);
        return new RestoreResult(restored, target);
    }

    public async Task<bool> DeleteRecordAsync(long recordId, CancellationToken cancellationToken = default)
    {
        var record = await _db.BackupRecords.FindAsync([recordId], cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return false;
        }

        _db.BackupRecords.Remove(record); // 条目级联删除；备份文件保留
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> RunDueAutoBackupsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var due = await _db.BackupTasks.AsNoTracking()
            .Where(t => t.Enabled && t.AutoBackup)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var ran = 0;
        foreach (var task in due)
        {
            if (task.LastBackupAt.HasValue && now.Subtract(task.LastBackupAt.Value).TotalHours < task.AutoIntervalHours)
            {
                continue;
            }

            await RunBackupAsync(task.Id, cancellationToken).ConfigureAwait(false);
            ran++;
        }

        return ran;
    }

    private static void ValidateTask(BackupTask task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
        {
            throw new InvalidOperationException("任务名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(task.SourcePath))
        {
            throw new InvalidOperationException("源路径不能为空");
        }

        if (!File.Exists(task.SourcePath) && !Directory.Exists(task.SourcePath))
        {
            throw new InvalidOperationException($"源路径不存在：{task.SourcePath}");
        }

        if (string.IsNullOrWhiteSpace(task.DestinationPath))
        {
            throw new InvalidOperationException("目标路径不能为空");
        }

        if (task.Mode is not ("full" or "incremental"))
        {
            throw new InvalidOperationException("备份模式必须是 full 或 incremental");
        }
    }

    private static (List<string> Files, string BasePath) CollectSourceFiles(BackupTask task)
    {
        var include = SplitPatterns(task.IncludePatterns);
        var exclude = SplitPatterns(task.ExcludePatterns);

        var files = new List<string>();
        if (File.Exists(task.SourcePath))
        {
            var rel = Path.GetFileName(task.SourcePath).Replace(Path.DirectorySeparatorChar, '/');
            if ((include.Count == 0 || GlobMatcher.IsMatch(rel, include)) && !GlobMatcher.IsMatch(rel, exclude))
            {
                files.Add(task.SourcePath);
            }

            return (files, Path.GetDirectoryName(task.SourcePath)!);
        }

        var basePath = task.SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var file in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
        {
            var rel = GetRelativePath(basePath, file);
            if (include.Count > 0 && !GlobMatcher.IsMatch(rel, include))
            {
                continue;
            }

            if (GlobMatcher.IsMatch(rel, exclude))
            {
                continue;
            }

            files.Add(file);
        }

        return (files, basePath);
    }

    private static List<string> SplitPatterns(string? patterns)
        => string.IsNullOrWhiteSpace(patterns)
            ? []
            : patterns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string GetRelativePath(string basePath, string file)
    {
        var fullBase = Path.GetFullPath(basePath);
        var fullFile = Path.GetFullPath(file);
        return Path.GetRelativePath(fullBase, fullFile).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
