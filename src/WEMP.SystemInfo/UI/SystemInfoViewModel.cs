using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.Optimization.Services;
using WEMP.SystemInfo.Detection;
using WEMP.SystemInfo.Models;
using WEMP.SystemInfo.Persistence;
using WEMP.SystemInfo.Services;

namespace WEMP.SystemInfo.UI;

/// <summary>检测结果展示行。</summary>
public sealed record InfoRow(string Label, string Value);

/// <summary>检测结果分组。</summary>
public sealed record InfoGroup(string Title, IReadOnlyList<InfoRow> Rows);

/// <summary>首页推荐操作条目。</summary>
public sealed record RecommendItem(string Title, string ButtonText, string Action);

/// <summary>系统检测页面视图模型：触发检测、持久化并生成展示分组与首页概览。</summary>
public partial class SystemInfoViewModel : ObservableObject
{
    private readonly ISystemInfoProvider _provider;
    private readonly ISnapshotRepository _snapshots;
    private readonly JunkCleanerService _junkCleaner;
    private readonly IOptimizationService? _optimization;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _status = "尚未检测";

    [ObservableProperty]
    private bool _isAdministrator;

    /// <summary>非管理员时提示权限说明。</summary>
    public bool ShowPermissionHint => !IsAdministrator;

    [ObservableProperty]
    private DateTime? _lastCapturedAt;

    [ObservableProperty]
    private IReadOnlyList<InfoGroup> _groups = [];

    // ---- 首页概览（检测完成后填充）----

    [ObservableProperty]
    private string _osShortName = "—";

    [ObservableProperty]
    private string _buildVersion = "—";

    [ObservableProperty]
    private string _cpuShort = "—";

    [ObservableProperty]
    private string _cpuDetail = "—";

    [ObservableProperty]
    private string _ramTotal = "—";

    [ObservableProperty]
    private string _ramDetail = "—";

    [ObservableProperty]
    private string _storageTotal = "—";

    [ObservableProperty]
    private string _storageDetail = "—";

    [ObservableProperty]
    private int _storagePercent;

    [ObservableProperty]
    private double _score;

    [ObservableProperty]
    private string _scoreText = "—";

    [ObservableProperty]
    private string _scoreDesc = "点击「重新检测」获取系统状态";

    [ObservableProperty]
    private string _computerName = "—";

    [ObservableProperty]
    private string _memUsedPercentText = "—";

    [ObservableProperty]
    private string _networkState = "—";

    [ObservableProperty]
    private string _bootTimeText = "—";

    [ObservableProperty]
    private string _storageBarText = "—";

    [ObservableProperty]
    private IReadOnlyList<RecommendItem> _recommendations =
    [
        new RecommendItem("可优化的服务 12 项", "优化", "优化"),
        new RecommendItem("可清理的垃圾 2.3GB", "清理", "清理"),
        new RecommendItem("可更新的驱动 3 项", "更新", "更新"),
        new RecommendItem("可提升的项目 5 项", "查看", "查看"),
    ];

    public SystemInfoViewModel(
        ISystemInfoProvider provider,
        ISnapshotRepository snapshots,
        JunkCleanerService junkCleaner,
        IOptimizationService? optimization = null)
    {
        _provider = provider;
        _snapshots = snapshots;
        _junkCleaner = junkCleaner;
        _optimization = optimization;

        IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        OnPropertyChanged(nameof(ShowPermissionHint));
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
            BuildOverview(snapshot);
            await BuildRecommendationsAsync(snapshot);
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

    [RelayCommand]
    private async Task OneKeyOptimizeAsync()
    {
        if (_optimization is null)
        {
            Status = "优化服务不可用";
            return;
        }

        Status = "一键优化执行中…";
        try
        {
            var result = await _optimization.ApplyOneKeyAsync();
            Status = $"一键优化完成：成功 {result.SuccessCount} 项，失败 {result.FailureCount} 项";
        }
        catch (Exception ex)
        {
            Status = $"一键优化失败：{ex.Message}";
        }
    }

    /// <summary>清理垃圾：扫描临时文件与回收站，执行清理并报告释放空间。</summary>
    [RelayCommand]
    private async Task CleanJunkAsync()
    {
        IsDetecting = true;
        Status = "正在扫描垃圾文件…";
        try
        {
            var scan = await Task.Run(_junkCleaner.Scan);
            if (scan.FilesCleaned == 0)
            {
                Status = "没有可清理的垃圾文件";
                return;
            }

            // 清理会删除临时文件并清空回收站，执行前需用户确认
            var confirmed = MessageBox.Show(
                $"发现 {scan.FilesCleaned} 个垃圾文件（约 {FormatBytes(scan.FreedBytes)}）。\n" +
                "清理将删除这些临时文件，并清空回收站。是否继续？",
                "清理确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmed != MessageBoxResult.Yes)
            {
                Status = "已取消清理";
                return;
            }

            Status = $"发现 {scan.FilesCleaned} 个垃圾文件（约 {FormatBytes(scan.FreedBytes)}），正在清理…";
            var result = await Task.Run(_junkCleaner.Clean);
            Status = $"清理完成：删除 {result.FilesCleaned} 个文件，释放约 {FormatBytes(result.FreedBytes)}（含回收站）";
        }
        catch (Exception ex)
        {
            Status = $"清理失败：{ex.Message}";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    /// <summary>修复系统：后台执行 SFC /scannow 系统文件检查（需管理员权限，可能耗时数分钟）。</summary>
    [RelayCommand]
    private async Task RepairSystemAsync()
    {
        IsDetecting = true;
        Status = "正在执行系统文件检查（SFC /scannow），可能需要几分钟…";
        try
        {
            var result = await WEMP.PackageManagement.Infrastructure.ProcessRunner.RunAsync(
                "cmd.exe", "/c sfc /scannow", CancellationToken.None, timeoutSeconds: 1800);
            Status = result.Success
                ? "系统文件检查完成，未发现问题"
                : $"系统文件检查完成（退出码 {result.ExitCode}）：{Truncate(result.Output)}";
        }
        catch (Exception ex)
        {
            Status = $"系统修复失败：{ex.Message}";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    /// <summary>创建系统还原点（需管理员权限且系统保护已开启）。</summary>
    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        IsDetecting = true;
        Status = "正在创建系统还原点…";
        try
        {
            var ok = await Task.Run(() => RestorePointHelper.Create("WEMP 操作前还原点"));
            Status = ok ? "系统还原点创建成功" : "创建失败：请以管理员身份运行，并确认系统保护已开启";
        }
        catch (Exception ex)
        {
            Status = $"创建还原点失败：{ex.Message}";
        }
        finally
        {
            IsDetecting = false;
        }
    }

    [RelayCommand]
    private void RecommendAction(string action)
    {
        switch (action)
        {
            case "优化":
                _ = OneKeyOptimizeAsync();
                break;
            case "清理":
                _ = CleanJunkAsync();
                break;
            case "修复":
                _ = RepairSystemAsync();
                break;
            case "查看":
                Status = "当前没有待处理的推荐项";
                break;
            default:
                Status = $"「{action}」暂不支持";
                break;
        }
    }

    private static string Truncate(string text, int max = 120)
        => text.Length <= max ? text : $"{text[..(max - 1)]}…";

    /// <summary>基于真实检测结果生成首页推荐操作列表。</summary>
    private async Task BuildRecommendationsAsync(SystemInfoSnapshot snapshot)
    {
        var recommendations = new List<RecommendItem>();

        // 待优化知识库条目数
        if (_optimization is not null)
        {
            var items = await _optimization.GetItemsAsync();
            var pending = items.Count(i => i.Enabled);
            if (pending > 0)
            {
                recommendations.Add(new RecommendItem($"可优化项目 {pending} 项", "优化", "优化"));
            }
        }

        // 可清理的临时垃圾规模
        var junk = await Task.Run(_junkCleaner.Scan);
        if (junk.FreedBytes > 0)
        {
            recommendations.Add(new RecommendItem($"可清理垃圾 {FormatBytes(junk.FreedBytes)}", "清理", "清理"));
        }

        // 资源占用偏高时给出优化建议
        if (Score < 70)
        {
            recommendations.Add(new RecommendItem("系统资源占用偏高，建议优化", "优化", "优化"));
        }

        // 系统文件检查建议（保留一条引导性建议）
        recommendations.Add(new RecommendItem("建议定期执行系统文件检查", "修复", "修复"));

        Recommendations = recommendations;
    }

    /// <summary>根据快照填充首页概览卡片数据。</summary>
    private void BuildOverview(SystemInfoSnapshot snapshot)
    {
        var os = snapshot.Os;
        var cpu = snapshot.Cpu;
        var memory = snapshot.Memory;

        OsShortName = ExtractOsShortName(os.Name);
        BuildVersion = string.IsNullOrEmpty(os.Build)
            ? os.Version
            : $"{os.Version} (Build {os.Build})";
        CpuShort = Shorten(cpu.Name, 30);
        CpuDetail = $"{cpu.Cores} 核 {cpu.Threads} 线程"
            + (cpu.MaxClockMhz > 0 ? $" · {cpu.MaxClockMhz / 1000.0:F1} GHz" : "");
        RamTotal = FormatBytes(memory.TotalBytes);
        RamDetail = $"可用 {FormatBytes(memory.AvailableBytes)}";
        ComputerName = Environment.MachineName;

        // 存储：优先系统盘（C:），否则取第一个卷
        var volume = snapshot.Volumes
                .FirstOrDefault(v => v.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            ?? snapshot.Volumes.FirstOrDefault();

        if (volume is { TotalBytes: > 0 })
        {
            var used = volume.TotalBytes - volume.FreeBytes;
            var percent = (int)Math.Round(used * 100.0 / volume.TotalBytes);
            StorageTotal = FormatBytes(volume.TotalBytes);
            StorageDetail = $"已用 {percent}%";
            StoragePercent = Math.Clamp(percent, 0, 100);
            StorageBarText = $"已用 {FormatBytes(used)} / 共 {FormatBytes(volume.TotalBytes)} · {percent}%";
        }
        else
        {
            StorageTotal = "—";
            StorageDetail = "—";
            StoragePercent = 0;
            StorageBarText = "未检测到卷";
        }

        // 综合评分：内存可用率 60% + 磁盘可用率 40%
        var memFree = memory.TotalBytes > 0
            ? memory.AvailableBytes * 100.0 / memory.TotalBytes
            : 60;
        var diskFree = volume is { TotalBytes: > 0 }
            ? volume.FreeBytes * 100.0 / volume.TotalBytes
            : 60;
        Score = Math.Clamp(Math.Round(memFree * 0.6 + diskFree * 0.4, 0), 0, 100);
        ScoreText = Score switch
        {
            >= 85 => "优秀",
            >= 70 => "良好",
            >= 50 => "一般",
            _ => "待优化",
        };
        ScoreDesc = Score switch
        {
            >= 85 => "系统状态良好",
            >= 70 => "系统运行稳定",
            >= 50 => "建议进行优化",
            _ => "建议尽快优化",
        };
        MemUsedPercentText = memory.TotalBytes > 0
            ? $"{100 - memFree:F0}%"
            : "—";

        // 网络状态与启动时间
        var network = snapshot.Network;
        NetworkState = network.IsAvailable
            ? network.ActiveAdapters.Count > 0
                ? $"已连接（{string.Join("、", network.ActiveAdapters.Select(a => Shorten(a, 20)))}）"
                : "已连接"
            : "未连接";
        BootTimeText = snapshot.Os.LastBootUpTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未知";
    }

    private static string ExtractOsShortName(string osName)
    {
        var index = osName.IndexOf("Windows", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Shorten(osName, 24);
        }

        var parts = osName[index..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]} {parts[1]}" : osName[index..];
    }

    private static string Shorten(string text, int max)
        => text.Length <= max ? text : $"{text[..(max - 1)]}…";

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
                new InfoRow("上次启动", os.LastBootUpTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未知"),
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
            new InfoGroup("网络", BuildNetworkRows(snapshot.Network)),
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

    private static IReadOnlyList<InfoRow> BuildNetworkRows(NetworkInfo network)
    {
        var rows = new List<InfoRow> { new("状态", network.IsAvailable ? "已连接" : "未连接") };
        if (network.ActiveAdapters.Count == 0)
        {
            rows.Add(new InfoRow("适配器", "—"));
        }
        else
        {
            foreach (var adapter in network.ActiveAdapters)
            {
                rows.Add(new InfoRow("适配器", adapter));
            }
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
