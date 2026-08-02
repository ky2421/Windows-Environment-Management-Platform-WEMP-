using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.SystemInfo.Detection;
using WEMP.SystemInfo.Models;
using WEMP.SystemInfo.Persistence;

namespace WEMP.SystemInfo.UI;

/// <summary>检测结果展示行。</summary>
public sealed record InfoRow(string Label, string Value);

/// <summary>检测结果分组。</summary>
public sealed record InfoGroup(string Title, IReadOnlyList<InfoRow> Rows);

/// <summary>系统检测页面视图模型：触发检测、持久化并生成展示分组。</summary>
public partial class SystemInfoViewModel : ObservableObject
{
    private readonly ISystemInfoProvider _provider;
    private readonly ISnapshotRepository _snapshots;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _status = "尚未检测";

    [ObservableProperty]
    private DateTime? _lastCapturedAt;

    [ObservableProperty]
    private IReadOnlyList<InfoGroup> _groups = [];

    public SystemInfoViewModel(ISystemInfoProvider provider, ISnapshotRepository snapshots)
    {
        _provider = provider;
        _snapshots = snapshots;
    }

    [RelayCommand]
    private async Task DetectAsync(CancellationToken cancellationToken)
    {
        IsDetecting = true;
        Status = "正在检测系统信息…";
        try
        {
            // WMI 查询与子进程探测均为阻塞调用，放后台线程执行
            var snapshot = await Task.Run(
                () => _provider.DetectAsync(cancellationToken),
                cancellationToken);

            var id = await _snapshots.SaveAsync(snapshot, cancellationToken);

            Log.Information(
                "系统检测完成：快照 #{SnapshotId} OS={Os} CPU={Cpu} 内存={RamGb}GB 开发工具={DevTools}",
                id,
                snapshot.Os.Name,
                snapshot.Cpu.Name,
                snapshot.Memory.TotalBytes / (1024.0 * 1024 * 1024),
                string.Join("、", snapshot.DevTools.Select(t => $"{t.DisplayName} {t.Version}")));

            Groups = BuildGroups(snapshot);
            LastCapturedAt = DateTime.Now;
            Status = $"检测完成，已保存为快照 #{id}";
        }
        catch (OperationCanceledException)
        {
            Status = "检测已取消";
        }
        catch (Exception ex)
        {
            Status = $"检测失败：{ex.Message}";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    private static IReadOnlyList<InfoGroup> BuildGroups(SystemInfoSnapshot snapshot)
    {
        var os = snapshot.Os;
        var cpu = snapshot.Cpu;
        var memory = snapshot.Memory;

        return
        [
            new InfoGroup("操作系统", [
                new InfoRow("系统", os.Name),
                new InfoRow("版本", $"{os.Version} (Build {os.Build})"),
                new InfoRow("架构", os.Architecture),
                new InfoRow("启动模式", string.IsNullOrEmpty(os.BootMode) ? "未知" : os.BootMode),
                new InfoRow("安全启动", os.SecureBoot ? "已启用" : "未启用"),
                new InfoRow("安装日期", os.InstallDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "未知"),
                new InfoRow("主机名", Environment.MachineName),
                new InfoRow("检测时间", snapshot.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            ]),
            new InfoGroup("CPU", [
                new InfoRow("型号", cpu.Name),
                new InfoRow("物理核心", cpu.Cores.ToString()),
                new InfoRow("逻辑处理器", cpu.Threads.ToString()),
                new InfoRow("标称主频", cpu.MaxClockMhz > 0 ? $"{cpu.MaxClockMhz} MHz" : "未知"),
                new InfoRow("虚拟化", cpu.Virtualization ? "已启用" : "未启用"),
                new InfoRow("插槽", cpu.Socket),
            ]),
            new InfoGroup("显卡", BuildGpuRows(snapshot.Gpus)),
            new InfoGroup("内存", [
                new InfoRow("总量", FormatBytes(memory.TotalBytes)),
                new InfoRow("可用", FormatBytes(memory.AvailableBytes)),
                new InfoRow("已用", FormatBytes(memory.UsedBytes)),
                new InfoRow("内存条", memory.Modules.ToString()),
            ]),
            new InfoGroup("磁盘", BuildDiskRows(snapshot)),
            new InfoGroup("开发环境", BuildDevToolRows(snapshot.DevTools)),
        ];
    }

    private static IReadOnlyList<InfoRow> BuildGpuRows(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus.Count == 0)
        {
            return [new InfoRow("未检测到", "—")];
        }

        return gpus.SelectMany(gpu => new[]
        {
            new InfoRow("名称", gpu.Name),
            new InfoRow("显存", gpu.MemoryBytes is { } bytes ? FormatBytes(bytes) : "未知"),
            new InfoRow("驱动版本", gpu.DriverVersion),
            new InfoRow("处理单元", gpu.VideoProcessor),
        }).ToList();
    }

    private static IReadOnlyList<InfoRow> BuildDiskRows(SystemInfoSnapshot snapshot)
    {
        var rows = new List<InfoRow>();

        foreach (var disk in snapshot.Disks)
        {
            rows.Add(new InfoRow("磁盘", $"{disk.Model}（{disk.MediaType}，{FormatBytes(disk.SizeBytes)}，{disk.Partitions} 个分区）"));
        }

        foreach (var volume in snapshot.Volumes)
        {
            rows.Add(new InfoRow("卷", $"{volume.DriveLetter}  {volume.FileSystem}  {FormatBytes(volume.FreeBytes)} / {FormatBytes(volume.TotalBytes)} 可用"));
        }

        if (rows.Count == 0)
        {
            rows.Add(new InfoRow("未检测到", "—"));
        }

        return rows;
    }

    private static IReadOnlyList<InfoRow> BuildDevToolRows(IReadOnlyList<DevToolInfo> tools)
    {
        if (tools.Count == 0)
        {
            return [new InfoRow("未检测到", "尚未安装常用开发工具")];
        }

        return tools
            .OrderBy(t => t.DisplayName)
            .Select(t => new InfoRow(t.DisplayName, $"{t.Version}（{t.Executable}）"))
            .ToList();
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        return bytes switch
        {
            >= (long)gb => $"{bytes / gb:F1} GB",
            >= (long)mb => $"{bytes / mb:F1} MB",
            >= (long)kb => $"{bytes / kb:F0} KB",
            _ => $"{bytes} B",
        };
    }
}
