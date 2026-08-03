using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using WEMP.SystemInfo.Models;

namespace WEMP.SystemInfo.Detection;

/// <summary>
/// 基于 WMI（root\cimv2）与注册表的系统信息采集实现。
/// WMI 查询在本地执行，通常为毫秒级；开发环境检测通过子进程探测。
/// </summary>
public sealed partial class WmiSystemInfoProvider : ISystemInfoProvider
{
    private const string CimV2 = @"root\cimv2";

    public async Task<SystemInfoSnapshot> DetectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = new SystemInfoSnapshot
        {
            Os = DetectOs(),
            Cpu = DetectCpu(),
            Gpus = DetectGpus(),
            Memory = DetectMemory(),
            DevTools = await DevEnvironmentDetector.DetectAsync(cancellationToken).ConfigureAwait(false),
            Network = DetectNetwork(),
        };

        (snapshot.Disks, snapshot.Volumes) = DetectDisks();

        return snapshot;
    }

    private static OsInfo DetectOs()
    {
        using var os = Query(
            CimV2,
            "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime FROM Win32_OperatingSystem");
        using var computer = Query(
            CimV2,
            "SELECT BootupState FROM Win32_ComputerSystem");

        var row = os.Cast<ManagementObject>().FirstOrDefault();

        return new OsInfo
        {
            Name = GetString(row, "Caption"),
            Version = GetString(row, "Version"),
            Build = GetString(row, "BuildNumber"),
            Architecture = GetString(row, "OSArchitecture").Contains("64", StringComparison.OrdinalIgnoreCase) ? "64-bit" : "32-bit",
            BootMode = GetString(computer.Cast<ManagementObject>().FirstOrDefault(), "BootupState"),
            SecureBoot = DetectSecureBoot(),
            InstallDate = ParseCimDateTime(GetString(row, "InstallDate")),
            LastBootUpTime = ParseCimDateTime(GetString(row, "LastBootUpTime")),
        };
    }

    private static CpuInfo DetectCpu()
    {
        using var cpu = Query(
            CimV2,
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation, VirtualizationFirmwareEnabled FROM Win32_Processor");

        var row = cpu.Cast<ManagementObject>().FirstOrDefault();

        return new CpuInfo
        {
            Name = GetString(row, "Name").Trim(),
            Cores = GetInt(row, "NumberOfCores"),
            Threads = GetInt(row, "NumberOfLogicalProcessors"),
            MaxClockMhz = GetInt(row, "MaxClockSpeed"),
            Virtualization = GetBool(row, "VirtualizationFirmwareEnabled"),
            Socket = GetString(row, "SocketDesignation"),
        };
    }

    private static List<GpuInfo> DetectGpus()
    {
        using var gpus = Query(
            CimV2,
            "SELECT Name, AdapterRAM, DriverVersion, VideoProcessor FROM Win32_VideoController");

        return gpus.Cast<ManagementObject>()
            .Select(row => new GpuInfo
            {
                Name = GetString(row, "Name"),
                MemoryBytes = GetLong(row, "AdapterRAM") is var b and > 0 ? b : null,
                DriverVersion = GetString(row, "DriverVersion"),
                VideoProcessor = GetString(row, "VideoProcessor"),
            })
            .ToList();
    }

    private static MemoryInfo DetectMemory()
    {
        using var os = Query(
            CimV2,
            "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
        using var modules = Query(
            CimV2,
            "SELECT Capacity FROM Win32_PhysicalMemory");

        var row = os.Cast<ManagementObject>().FirstOrDefault();

        return new MemoryInfo
        {
            TotalBytes = GetLong(row, "TotalVisibleMemorySize") * 1024,
            AvailableBytes = GetLong(row, "FreePhysicalMemory") * 1024,
            Modules = modules.Count,
        };
    }

    private static (List<DiskInfo> Disks, List<VolumeInfo> Volumes) DetectDisks()
    {
        using var disks = Query(
            CimV2,
            "SELECT Model, Size, MediaType, Partitions, InterfaceType, Index FROM Win32_DiskDrive");
        using var volumes = Query(
            CimV2,
            "SELECT DeviceID, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");

        var volumeList = volumes.Cast<ManagementObject>()
            .Select(row => new VolumeInfo
            {
                DriveLetter = GetString(row, "DeviceID"),
                FileSystem = GetString(row, "FileSystem"),
                TotalBytes = GetLong(row, "Size"),
                FreeBytes = GetLong(row, "FreeSpace"),
            })
            .Where(v => !string.IsNullOrEmpty(v.DriveLetter))
            .ToList();

        var diskList = disks.Cast<ManagementObject>()
            .Select(row => new DiskInfo
            {
                Model = GetString(row, "Model").Trim(),
                MediaType = DetectDiskMediaType(GetInt(row, "Index")),
                SizeBytes = GetLong(row, "Size"),
                Partitions = GetInt(row, "Partitions"),
                InterfaceType = GetString(row, "InterfaceType"),
            })
            .Where(d => d.SizeBytes > 0)
            .ToList();

        return (diskList, volumeList);
    }

    /// <summary>通过存储命名空间查询物理盘介质类型（0=未知, 3=HDD, 4=SSD）。</summary>
    private static string DetectDiskMediaType(int index)
    {
        try
        {
            using var physical = Query(
                @"root\Microsoft\Windows\Storage",
                "SELECT MediaType FROM MSFT_PhysicalDisk");
            foreach (ManagementObject row in physical)
            {
                if (GetInt(row, "MediaType") == 4)
                {
                    return "SSD";
                }
            }
        }
        catch (ManagementException)
        {
            // 部分系统无存储命名空间，降级为未知
        }

        return "Unknown";
    }

    private static bool DetectSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            return key?.GetValue("UEFISecureBootEnabled") is int value && value == 1;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>枚举处于连接状态的非回环网络适配器。</summary>
    private static NetworkInfo DetectNetwork()
    {
        var adapters = new List<string>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    adapters.Add(nic.Name);
                }
            }
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        {
            // 无法枚举适配器时视为未连接
        }

        return new NetworkInfo
        {
            IsAvailable = adapters.Count > 0,
            ActiveAdapters = adapters,
        };
    }

    // ---- WMI 工具方法 ----

    private static ManagementObjectCollection Query(string scope, string wql)
    {
        return new ManagementObjectSearcher(scope, wql).Get();
    }

    private static string GetString(ManagementBaseObject? row, string property)
    {
        return row?[property]?.ToString() ?? "";
    }

    private static int GetInt(ManagementBaseObject? row, string property)
    {
        return row?[property] is { } v && int.TryParse(v.ToString(), out var result) ? result : 0;
    }

    private static long GetLong(ManagementBaseObject? row, string property)
    {
        return row?[property] is { } v && long.TryParse(v.ToString(), out var result) ? result : 0;
    }

    private static bool GetBool(ManagementBaseObject? row, string property)
    {
        return row?[property] is { } v && bool.TryParse(v.ToString(), out var result) && result;
    }

    /// <summary>WMI 的 CIM 日期时间（YYYYMMDDHHMMSS.ffffff+ZZZ）转 <see cref="DateTime"/>。</summary>
    private static DateTime? ParseCimDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = CimDateTimeRegex().Match(value);
        if (!match.Success)
        {
            return null;
        }

        var date = match.Groups[1].Value;
        return DateTime.TryParseExact(
            date,
            "yyyyMMddHHmmss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : null;
    }

    [GeneratedRegex(@"^(\d{14})\.\d*[+-]\d{3}$")]
    private static partial Regex CimDateTimeRegex();
}
