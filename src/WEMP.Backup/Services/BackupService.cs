using System.IO;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Backup.Services;

/// <summary>备份恢复服务实现。</summary>
public sealed class BackupService : IBackupService
{
    private readonly IDbContextFactory<WempDbContext> _dbFactory;

    // BackupService 以单例注册；后台自动备份与 UI 手动操作可能并发，
    // 保持门闩串行化文件系统操作（同一时刻仅一个备份在跑）。
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(IDbContextFactory<WempDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<BackupTask>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.BackupTasks.AsNoTracking()
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupTask?> GetTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.BackupTasks.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupTask> CreateTaskAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            ValidateTask(task);
            task.CreatedAt = DateTime.Now;
            task.UpdatedAt = task.CreatedAt;
            db.BackupTasks.Add(task);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("备份任务已创建：{Name}（源 {Source} → 目标 {Dest}）", task.Name, task.SourcePath, task.DestinationPath);
            return task;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupTask?> UpdateTaskAsync(BackupTask task, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            ValidateTask(task);
            var existing = await db.BackupTasks.FindAsync([task.Id], cancellationToken).ConfigureAwait(false);
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
            existing.LastBackupAt = task.LastBackupAt;
            existing.UpdatedAt = DateTime.Now;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteTaskAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var task = await db.BackupTasks.FindAsync([taskId], cancellationToken).ConfigureAwait(false);
            if (task is null)
            {
                return false;
            }

            db.BackupTasks.Remove(task); // 记录/条目级联删除
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 同步清理该任务的目标根目录，避免磁盘留下孤儿备份文件
            TryDeleteDirectory(Path.Combine(task.DestinationPath, $"{task.Id}_{Sanitize(task.Name)}"));
            Log.Information("备份任务已删除：{Name}", task.Name);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupRecord> RunBackupAsync(long taskId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await RunBackupCoreAsync(taskId, db, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BackupRecord> RunBackupCoreAsync(long taskId, WempDbContext db, CancellationToken cancellationToken)
    {
        var task = await db.BackupTasks.FindAsync([taskId], cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"备份任务不存在：{taskId}");

        var record = new BackupRecord
        {
            TaskId = task.Id,
            StartedAt = DateTime.Now,
            Status = "running",
        };
        db.BackupRecords.Add(record);

        try
        {
            var (files, basePath) = CollectSourceFiles(task);

            // 增量：仅复制修改时间晚于上次成功备份结束时间的文件（统一按 UTC 比较）
            var incremental = task.Mode == "incremental";
            DateTime? baselineUtc = null;
            if (incremental)
            {
                var last = await db.BackupRecords.AsNoTracking()
                    .Where(r => r.TaskId == task.Id && r.Status == "success")
                    .OrderByDescending(r => r.FinishedAt)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                baselineUtc = last?.FinishedAt?.ToUniversalTime();
            }

            var destinationRoot = Path.Combine(task.DestinationPath, $"{task.Id}_{Sanitize(task.Name)}");
            var destinationDir = Path.Combine(destinationRoot, $"{record.StartedAt:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(destinationDir);

            // 删除追踪：将上次快照（历史备份目录中的文件全集）与当前源对比，
            // 源中已消失的文件写入 deleted 条目，使增量链能还原删除操作。
            List<string> deleted = [];
            if (incremental && baselineUtc.HasValue)
            {
                var previous = CollectSnapshotPaths(destinationRoot);
                var current = files
                    .Select(f => GetRelativePath(basePath, f))
                    .ToHashSet(StringComparer.Ordinal);
                deleted = previous.Except(current, StringComparer.Ordinal).ToList();
            }

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

                db.BackupFileEntries.Add(new BackupFileEntry
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

            // 删除追踪条目：元数据取该路径在历史快照中的最后记录，避免 UI 显示失真
            var deletedCount = 0;
            if (deleted.Count > 0)
            {
                var history = (await db.BackupFileEntries.AsNoTracking()
                        .Where(e => deleted.Contains(e.RelativePath))
                        .ToListAsync(cancellationToken).ConfigureAwait(false))
                    .GroupBy(e => e.RelativePath)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Id).First());

                foreach (var path in deleted)
                {
                    history.TryGetValue(path, out var last);
                    db.BackupFileEntries.Add(new BackupFileEntry
                    {
                        Record = record,
                        RelativePath = path,
                        FileSize = last?.FileSize ?? 0,
                        ModifiedAt = last?.ModifiedAt ?? record.StartedAt,
                        Action = "deleted",
                    });
                    deletedCount++;
                }
            }

            record.BackupType = incremental ? "incremental" : "full";
            record.FileCount = copied;
            record.TotalBytes = totalBytes;
            record.Status = "success";
            record.FinishedAt = DateTime.Now;
            record.Message = deletedCount > 0
                ? $"已备份 {copied} 个文件（{(incremental ? "增量" : "全量")}），删除 {deletedCount} 个文件"
                : $"已备份 {copied} 个文件（{(incremental ? "增量" : "全量")}）";
            task.LastBackupAt = record.FinishedAt;
            task.UpdatedAt = record.FinishedAt.Value;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Information("备份完成：任务 {Name} {Type} {Count} 文件 {Bytes} 字节", task.Name, record.BackupType, copied, totalBytes);
            return record;
        }
        catch (Exception ex)
        {
            record.Status = "failed";
            record.Error = ex.Message;
            record.FinishedAt = DateTime.Now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            Log.Error(ex, "备份失败：任务 {Name}", task.Name);
            return record;
        }
    }

    public async Task<List<BackupRecord>> GetRecordsAsync(long taskId, int limit = 100, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.BackupRecords.AsNoTracking()
                .Where(r => r.TaskId == taskId)
                .OrderByDescending(r => r.StartedAt)
                .Take(limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<BackupFileEntry>> GetRecordEntriesAsync(long recordId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            return await db.BackupFileEntries.AsNoTracking()
                .Where(e => e.RecordId == recordId)
                .OrderBy(e => e.RelativePath)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RestoreResult> RestoreAsync(long recordId, string? targetPath = null, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var record = await db.BackupRecords.AsNoTracking()
                .Include(r => r.Task)
                .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"备份记录不存在：{recordId}");

            // 链式还原：增量记录只保存变化文件，需从目标记录回溯到最早的记录，
            // 按时间正序合并全部条目（后者覆盖前者）重建该时刻的完整快照状态；
            // deleted 条目表示该时刻文件已不存在，还原时删除目标中的对应文件。
            var records = await db.BackupRecords.AsNoTracking()
                .Where(r => r.TaskId == record.TaskId && r.Id <= record.Id)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var recordIds = records.Select(r => r.Id).ToList();
            var entriesByRecord = (await db.BackupFileEntries.AsNoTracking()
                    .Where(e => recordIds.Contains(e.RecordId))
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToLookup(e => e.RecordId);

            var chain = new Dictionary<string, (BackupFileEntry Entry, BackupRecord Owner)>(StringComparer.Ordinal);
            foreach (var rec in records)
            {
                foreach (var entry in entriesByRecord[rec.Id])
                {
                    chain[entry.RelativePath] = (entry, rec);
                }
            }

            if (chain.Count == 0)
            {
                throw new InvalidOperationException("备份记录中没有可还原的文件");
            }

            var target = targetPath ?? record.Task!.SourcePath;
            var targetIsDirectory = Directory.Exists(target) || Path.GetExtension(target).Length == 0;
            var root = Path.Combine(record.Task!.DestinationPath, $"{record.Task.Id}_{Sanitize(record.Task.Name)}");

            var restored = 0;
            foreach (var (relative, (entry, owner)) in chain)
            {
                var targetFile = targetIsDirectory
                    ? Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar))
                    : target;

                if (entry.Action == "deleted")
                {
                    // 快照时刻该文件不存在：overwrite 时删除目标中的残留文件
                    if (File.Exists(targetFile) && overwrite)
                    {
                        File.Delete(targetFile);
                        restored++;
                    }

                    continue;
                }

                var sourceFile = Path.Combine(root, $"{owner.StartedAt:yyyyMMdd_HHmmss}", relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

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
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteRecordAsync(long recordId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var record = await db.BackupRecords.FindAsync([recordId], cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return false;
            }

            db.BackupRecords.Remove(record); // 条目级联删除
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 同步清理该记录对应的备份目录，避免磁盘留下孤儿备份文件
            var task = await db.BackupTasks.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == record.TaskId, cancellationToken).ConfigureAwait(false);
            if (task is not null)
            {
                var root = Path.Combine(task.DestinationPath, $"{task.Id}_{Sanitize(task.Name)}");
                TryDeleteDirectory(Path.Combine(root, $"{record.StartedAt:yyyyMMdd_HHmmss}"));
            }

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 删除备份目录；目录不存在或删除失败（如文件被占用）时仅告警，
    /// 不中断删除操作本身，残余文件交由用户手动清理。
    /// </summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                Log.Information("已清理备份目录：{Path}", path);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "备份目录清理失败，文件已保留：{Path}", path);
        }
    }

    /// <summary>
    /// 收集任务目标根目录下全部历史备份目录中的文件相对路径（'/' 分隔），
    /// 构成上次快照的文件全集，用于增量删除追踪。
    /// </summary>
    private static HashSet<string> CollectSnapshotPaths(string destinationRoot)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(destinationRoot))
        {
            return paths;
        }

        foreach (var dir in Directory.EnumerateDirectories(destinationRoot))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                paths.Add(Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        return paths;
    }

    public async Task<int> RunDueAutoBackupsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTime.Now;
            var due = await db.BackupTasks.AsNoTracking()
                .Where(t => t.Enabled && t.AutoBackup)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var ran = 0;
            foreach (var task in due)
            {
                if (task.LastBackupAt.HasValue && now.Subtract(task.LastBackupAt.Value).TotalHours < task.AutoIntervalHours)
                {
                    continue;
                }

                // 门闩已在方法入口持有，直接调用核心方法避免自死锁
                await RunBackupCoreAsync(task.Id, db, cancellationToken).ConfigureAwait(false);
                ran++;
            }

            return ran;
        }
        finally
        {
            _gate.Release();
        }
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
