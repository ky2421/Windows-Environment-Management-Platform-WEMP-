using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WEMP.Infrastructure.Data;
using WEMP.SystemInfo.Models;
using WEMP.SystemInfo.Persistence;

namespace WEMP.Core.Tests;

/// <summary>快照持久化：检测结果应完整映射到 system_snapshots 表。</summary>
public class SnapshotRepositoryTests
{
    private static (WempDbContext Db, TestDbFactory Factory) CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<WempDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new WempDbContext(options);
        db.Database.EnsureCreated();
        return (db, new TestDbFactory(connection));
    }

    private static SystemInfoSnapshot CreateSampleSnapshot() => new()
    {
        Os = new OsInfo
        {
            Name = "Microsoft Windows 11 专业版",
            Version = "10.0.26100",
            Build = "26100",
            Architecture = "64-bit",
            BootMode = "Normal boot",
            SecureBoot = true,
            InstallDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        Cpu = new CpuInfo
        {
            Name = "Test CPU",
            Cores = 8,
            Threads = 16,
            MaxClockMhz = 3600,
            Virtualization = true,
            Socket = "LGA1700",
        },
        Memory = new MemoryInfo
        {
            TotalBytes = 16L * 1024 * 1024 * 1024,
            AvailableBytes = 8L * 1024 * 1024 * 1024,
            Modules = 2,
        },
        Gpus = [new GpuInfo { Name = "Test GPU", MemoryBytes = 8L * 1024 * 1024 * 1024, DriverVersion = "31.0.1" }],
        Disks = [new DiskInfo { Model = "TestDisk SSD", MediaType = "SSD", SizeBytes = 512L * 1024 * 1024 * 1024, Partitions = 2, InterfaceType = "NVMe" }],
        Volumes = [new VolumeInfo { DriveLetter = "C:", FileSystem = "NTFS", TotalBytes = 512L * 1024 * 1024 * 1024, FreeBytes = 256L * 1024 * 1024 * 1024 }],
        DevTools = [new DevToolInfo { Name = "git", DisplayName = "Git", Executable = "git", Version = "2.43.0" }],
    };

    [Fact]
    public async Task SaveAsync_persists_full_detection_result()
    {
        var (db, factory) = CreateInMemoryDb();
        var repository = new SnapshotRepository(factory);
        var snapshot = CreateSampleSnapshot();

        var id = await repository.SaveAsync(snapshot);

        var saved = await db.SystemSnapshots.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("Microsoft Windows 11 专业版", saved!.OsName);
        Assert.Equal("10.0.26100", saved.OsVersion);
        Assert.Equal("64-bit", saved.OsArch);
        Assert.Equal("Normal boot", saved.BootMode);
        Assert.True(saved.SecureBoot);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), saved.OsInstallDate);
        Assert.Equal("Test CPU", saved.CpuModel);
        Assert.Equal(8, saved.CpuCores);
        Assert.Equal(16, saved.CpuThreads);
        Assert.True(saved.CpuVirtualization);
        Assert.Equal(16L * 1024, saved.RamTotalMb);
        Assert.Equal(8L * 1024, saved.RamAvailableMb);
        Assert.Contains("Test GPU", saved.GpuJson);
        Assert.Contains("TestDisk SSD", saved.DiskJson);
        Assert.Contains("C:", saved.DiskJson);
        Assert.Contains("git", saved.DevEnvJson);
        Assert.Equal("manual", saved.Trigger);
    }

    [Fact]
    public async Task GetRecentAsync_returns_latest_first()
    {
        var (db, factory) = CreateInMemoryDb();
        var repository = new SnapshotRepository(factory);

        var first = CreateSampleSnapshot();
        var second = CreateSampleSnapshot();
        first.Os.Name = "First OS";
        second.Os.Name = "Second OS";

        await repository.SaveAsync(first);
        await repository.SaveAsync(second);

        var recent = await repository.GetRecentAsync(2);
        Assert.Equal(2, recent.Count);
        Assert.Equal("Second OS", recent[0].OsName);
    }

    [Fact]
    public async Task SaveAsync_keeps_only_latest_30_snapshots()
    {
        var (db, factory) = CreateInMemoryDb();
        var repository = new SnapshotRepository(factory);

        for (var i = 0; i < 40; i++)
        {
            await repository.SaveAsync(CreateSampleSnapshot());
        }

        var remaining = await db.SystemSnapshots.OrderBy(s => s.Id).ToListAsync();
        Assert.Equal(SnapshotRepository.MaxSnapshots, remaining.Count);
        // 保留最近 30 条（Id 11..40），最早的 10 条被清理
        Assert.Equal(11, remaining[0].Id);
        Assert.Equal(40, remaining[^1].Id);
    }

    [Fact]
    public async Task SaveAsync_keeps_all_when_below_limit()
    {
        var (db, factory) = CreateInMemoryDb();
        var repository = new SnapshotRepository(factory);

        for (var i = 0; i < 5; i++)
        {
            await repository.SaveAsync(CreateSampleSnapshot());
        }

        var remaining = await db.SystemSnapshots.ToListAsync();
        Assert.Equal(5, remaining.Count);
    }
}
