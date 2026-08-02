using WEMP.Infrastructure.Data.Entities;
using WEMP.SystemInfo.Models;

namespace WEMP.SystemInfo.Persistence;

/// <summary>系统快照持久化。</summary>
public interface ISnapshotRepository
{
    /// <summary>保存一次检测结果为 system_snapshots 记录。</summary>
    Task<long> SaveAsync(SystemInfoSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>获取最近 N 次快照。</summary>
    Task<IReadOnlyList<SystemSnapshot>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
