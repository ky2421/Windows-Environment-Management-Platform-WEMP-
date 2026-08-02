using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Backup.Services;

/// <summary>
/// 备份恢复服务：任务 CRUD、全量/增量备份执行（文件复制到目标目录）、
/// 按记录还原、到期自动备份检查。
/// </summary>
public interface IBackupService
{
    Task<List<BackupTask>> GetTasksAsync(CancellationToken cancellationToken = default);

    Task<BackupTask?> GetTaskAsync(long taskId, CancellationToken cancellationToken = default);

    /// <summary>新建任务；路径校验失败抛出 <see cref="InvalidOperationException"/>。</summary>
    Task<BackupTask> CreateTaskAsync(BackupTask task, CancellationToken cancellationToken = default);

    Task<BackupTask?> UpdateTaskAsync(BackupTask task, CancellationToken cancellationToken = default);

    /// <summary>删除任务（级联删除记录与条目；已备份文件保留在磁盘）。</summary>
    Task<bool> DeleteTaskAsync(long taskId, CancellationToken cancellationToken = default);

    /// <summary>执行一次备份，返回本次记录。</summary>
    Task<BackupRecord> RunBackupAsync(long taskId, CancellationToken cancellationToken = default);

    Task<List<BackupRecord>> GetRecordsAsync(long taskId, int limit = 100, CancellationToken cancellationToken = default);

    Task<List<BackupFileEntry>> GetRecordEntriesAsync(long recordId, CancellationToken cancellationToken = default);

    /// <summary>从指定记录恢复文件到目标路径（默认恢复回源路径）。</summary>
    Task<RestoreResult> RestoreAsync(long recordId, string? targetPath = null, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>删除记录与条目（备份文件保留在磁盘）。</summary>
    Task<bool> DeleteRecordAsync(long recordId, CancellationToken cancellationToken = default);

    /// <summary>执行所有到期（启用 + 自动 + 距上次备份达到间隔）的自动备份，返回执行的任务数。</summary>
    Task<int> RunDueAutoBackupsAsync(CancellationToken cancellationToken = default);
}

/// <summary>还原结果。</summary>
public sealed record RestoreResult(int FileCount, string TargetPath);
