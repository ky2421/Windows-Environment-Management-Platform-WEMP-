using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Logging.Services;

namespace WEMP.Logging.UI;

/// <summary>日志中心页面视图模型：审计日志 / 系统事件 / 异常检测。</summary>
public partial class LoggingPageViewModel : ObservableObject
{
    private readonly ILoggingService _service;

    public ObservableCollection<AuditLog> AuditItems { get; } = [];
    public ObservableCollection<ModuleCount> ModuleStats { get; } = [];
    public ObservableCollection<SystemEvent> EventItems { get; } = [];
    public ObservableCollection<LogAnomaly> AnomalyItems { get; } = [];

    [ObservableProperty]
    private string _searchKeyword = "";

    [ObservableProperty]
    private string _selectedModule = "";

    [ObservableProperty]
    private string _selectedLevel = "";

    [ObservableProperty]
    private LogAnomaly? _selectedAnomaly;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "就绪";

    public IReadOnlyList<string> Modules { get; } = ["", "WEMP.Optimization", "WEMP.GameMode", "WEMP.PackageManagement", "WEMP.DevEnvironment", "WEMP.Logging"];

    public IReadOnlyList<string> Levels { get; } = ["", "info", "warning", "error"];

    public LoggingPageViewModel(ILoggingService service)
    {
        _service = service;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadAuditsAsync();
            await LoadStatisticsAsync();
            await LoadEventsAsync();
            await LoadAnomaliesAsync();
            Status = "已就绪";
        }
        catch (Exception ex)
        {
            Status = $"初始化失败：{ex.Message}";
            Log.Error(ex, "日志中心页面初始化失败");
        }
    }

    [RelayCommand]
    private async Task SearchAuditsAsync()
    {
        IsBusy = true;
        try
        {
            await LoadAuditsAsync();
            Status = $"审计日志 {AuditItems.Count} 条";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AggregateEventsAsync()
    {
        IsBusy = true;
        try
        {
            var added = await _service.AggregateEventsAsync(TimeSpan.FromHours(24));
            await LoadEventsAsync();
            Status = added > 0 ? $"已聚合事件，新增 {added} 条" : "事件已是最新（无新增）";
        }
        catch (Exception ex)
        {
            Status = $"事件聚合失败：{ex.Message}";
            Log.Error(ex, "事件聚合失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanAnomaliesAsync()
    {
        IsBusy = true;
        try
        {
            await _service.AggregateEventsAsync(TimeSpan.FromHours(24));
            var added = await _service.RunAnomalyScanAsync(TimeSpan.FromHours(24));
            await LoadAnomaliesAsync();
            Status = added > 0 ? $"异常扫描完成，发现 {added} 条新异常" : "异常扫描完成（无新异常）";
        }
        catch (Exception ex)
        {
            Status = $"异常扫描失败：{ex.Message}";
            Log.Error(ex, "异常扫描失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResolveAnomalyAsync()
    {
        if (SelectedAnomaly is null)
        {
            Status = "请先选择异常";
            return;
        }

        try
        {
            await _service.ResolveAnomalyAsync(SelectedAnomaly.Id);
            await LoadAnomaliesAsync();
            Status = $"已解决：{SelectedAnomaly.Title}";
        }
        catch (Exception ex)
        {
            Status = $"处置失败：{ex.Message}";
            Log.Error(ex, "异常处置失败");
        }
    }

    private async Task LoadAuditsAsync()
    {
        var (items, _) = await _service.QueryAuditsAsync(
            module: string.IsNullOrWhiteSpace(SelectedModule) ? null : SelectedModule,
            level: string.IsNullOrWhiteSpace(SelectedLevel) ? null : SelectedLevel,
            keyword: string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword,
            pageSize: 200);

        AuditItems.Clear();
        foreach (var item in items)
        {
            AuditItems.Add(item);
        }
    }

    private async Task LoadStatisticsAsync()
    {
        var stats = await _service.GetStatisticsAsync();
        ModuleStats.Clear();
        foreach (var stat in stats.ByModule.Take(8))
        {
            ModuleStats.Add(stat);
        }
    }

    private async Task LoadEventsAsync()
    {
        var events = await _service.GetSystemEventsAsync(limit: 200);
        EventItems.Clear();
        foreach (var evt in events)
        {
            EventItems.Add(evt);
        }
    }

    private async Task LoadAnomaliesAsync()
    {
        var anomalies = await _service.GetAnomaliesAsync();
        AnomalyItems.Clear();
        foreach (var anomaly in anomalies)
        {
            AnomalyItems.Add(anomaly);
        }
    }
}
