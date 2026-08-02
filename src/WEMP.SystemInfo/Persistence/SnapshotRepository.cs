using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.Infrastructure.Data.Entities;
using WEMP.SystemInfo.Models;

namespace WEMP.SystemInfo.Persistence;

/// <summary>将检测结果映射为 <see cref="SystemSnapshot"/> 实体并写入 SQLite。</summary>
public sealed class SnapshotRepository(WempDbContext db) : ISnapshotRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<long> SaveAsync(SystemInfoSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var entity = new SystemSnapshot
        {
            CapturedAt = snapshot.CapturedAt.ToLocalTime(),
            Trigger = "manual",
            Hostname = Environment.MachineName,
            OsName = snapshot.Os.Name,
            OsVersion = snapshot.Os.Version,
            OsBuild = snapshot.Os.Build,
            OsArch = snapshot.Os.Architecture,
            OsInstallDate = snapshot.Os.InstallDate,
            BootMode = snapshot.Os.BootMode,
            SecureBoot = snapshot.Os.SecureBoot,
            CpuModel = snapshot.Cpu.Name,
            CpuCores = snapshot.Cpu.Cores,
            CpuThreads = snapshot.Cpu.Threads,
            CpuVirtualization = snapshot.Cpu.Virtualization,
            RamTotalMb = snapshot.Memory.TotalBytes / (1024 * 1024),
            RamAvailableMb = snapshot.Memory.AvailableBytes / (1024 * 1024),
            GpuJson = Serialize(snapshot.Gpus),
            DiskJson = Serialize(new { disks = snapshot.Disks, volumes = snapshot.Volumes }),
            DevEnvJson = Serialize(snapshot.DevTools),
        };

        db.SystemSnapshots.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await db.SystemSnapshots
            .OrderByDescending(s => s.CapturedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
