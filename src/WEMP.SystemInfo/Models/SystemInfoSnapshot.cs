namespace WEMP.SystemInfo.Models;

/// <summary>一次系统检测的完整结果。</summary>
public sealed class SystemInfoSnapshot
{
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public OsInfo Os { get; set; } = new();

    public CpuInfo Cpu { get; set; } = new();

    public List<GpuInfo> Gpus { get; set; } = [];

    public MemoryInfo Memory { get; set; } = new();

    public List<DiskInfo> Disks { get; set; } = [];

    /// <summary>逻辑卷（盘符）列表，与物理盘无直接关联。</summary>
    public List<VolumeInfo> Volumes { get; set; } = [];

    public List<DevToolInfo> DevTools { get; set; } = [];
}

/// <summary>操作系统信息。</summary>
public sealed class OsInfo
{
    /// <summary>例如「Microsoft Windows 11 专业版」。</summary>
    public string Name { get; set; } = "";

    /// <summary>例如 10.0.26100。</summary>
    public string Version { get; set; } = "";

    public string Build { get; set; } = "";

    /// <summary>64-bit / 32-bit。</summary>
    public string Architecture { get; set; } = "";

    /// <summary>Normal boot / Safe Mode 等。</summary>
    public string BootMode { get; set; } = "";

    public bool SecureBoot { get; set; }

    public DateTime? InstallDate { get; set; }
}

/// <summary>CPU 信息。</summary>
public sealed class CpuInfo
{
    public string Name { get; set; } = "";

    public int Cores { get; set; }

    public int Threads { get; set; }

    /// <summary>标称主频（MHz）。</summary>
    public long MaxClockMhz { get; set; }

    public bool Virtualization { get; set; }

    public string Socket { get; set; } = "";
}

/// <summary>显卡信息。</summary>
public sealed class GpuInfo
{
    public string Name { get; set; } = "";

    /// <summary>显存（字节）。WMI 对大于 4GB 的显存可能返回 0。</summary>
    public long? MemoryBytes { get; set; }

    public string DriverVersion { get; set; } = "";

    public string VideoProcessor { get; set; } = "";
}

/// <summary>内存信息。</summary>
public sealed class MemoryInfo
{
    public long TotalBytes { get; set; }

    public long AvailableBytes { get; set; }

    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    /// <summary>物理内存条数量。</summary>
    public int Modules { get; set; }
}

/// <summary>物理磁盘。</summary>
public sealed class DiskInfo
{
    public string Model { get; set; } = "";

    /// <summary>SSD / HDD / Unknown。</summary>
    public string MediaType { get; set; } = "";

    public long SizeBytes { get; set; }

    public int Partitions { get; set; }

    public string InterfaceType { get; set; } = "";
}

/// <summary>逻辑卷（盘符）。</summary>
public sealed class VolumeInfo
{
    public string DriveLetter { get; set; } = "";

    public string FileSystem { get; set; } = "";

    public long TotalBytes { get; set; }

    public long FreeBytes { get; set; }
}
