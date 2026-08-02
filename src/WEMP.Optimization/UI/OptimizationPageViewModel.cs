using System.Collections.ObjectModel;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.Infrastructure.Data.Entities;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.Services;

namespace WEMP.Optimization.UI;

/// <summary>优化项展示包装：可勾选，记录最近一次执行状态。</summary>
public partial class OptimizationItemViewModel : ObservableObject
{
    public OptimizationItemViewModel(OptimizationItem item)
    {
        Code = item.Code;
        Name = item.Name;
        Category = item.Category;
        CategoryText = GetCategoryText(item.Category);
        Principle = item.Principle;
        Risk = item.Risk;
        Recommendation = item.Recommendation;
        IsRecoverable = item.IsRecoverable;
        IsSelected = item.Recommendation == "required";
    }

    public string Code { get; }

    public string Name { get; }

    public string Category { get; }

    public string CategoryText { get; }

    public string? Principle { get; }

    public string? Risk { get; }

    public string Recommendation { get; }

    public bool IsRecoverable { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _stateText = "";

    [ObservableProperty]
    private bool _isSuccess;

    public void SetState(string message, bool success)
    {
        StateText = message;
        IsSuccess = success;
    }

    private static string GetCategoryText(string category) => category.ToLowerInvariant() switch
    {
        "service" => "服务",
        "registry" => "注册表",
        "game" => "游戏",
        "startup" => "启动项",
        "network" => "网络",
        "disk" => "磁盘",
        "power" => "电源",
        "memory" => "内存",
        _ => category,
    };
}

/// <summary>系统优化页面视图模型。</summary>
public partial class OptimizationPageViewModel : ObservableObject
{
    private readonly IOptimizationService _service;
    private readonly OptimizationSeedService _seed;

    public ObservableCollection<OptimizationItemViewModel> Items { get; } = [];

    public ObservableCollection<OptimizationRecord> History { get; } = [];

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _status = "正在加载…";

    [ObservableProperty]
    private bool _isAdministrator;

    /// <summary>非管理员时提示权限说明。</summary>
    public bool ShowPermissionHint => !IsAdministrator;

    /// <summary>执行期间禁用操作按钮。</summary>
    public bool CanOperate => !IsRunning;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanOperate));

    public OptimizationPageViewModel(IOptimizationService service, OptimizationSeedService seed)
    {
        _service = service;
        _seed = seed;

        IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        OnPropertyChanged(nameof(ShowPermissionHint));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _seed.EnsureSeedAsync();
            var items = await _service.GetItemsAsync();

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(new OptimizationItemViewModel(item));
            }

            await RefreshHistoryAsync();
            Status = $"共 {Items.Count} 项优化知识库条目";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "优化页面初始化失败");
            Status = $"加载失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OneKeyOptimizeAsync()
    {
        await RunAsync(
            () => _service.ApplyOneKeyAsync(),
            "一键优化");
    }

    [RelayCommand]
    private async Task OptimizeSelectedAsync()
    {
        var codes = Items.Where(i => i.IsSelected).Select(i => i.Code).ToList();
        await RunAsync(
            () => _service.ApplySelectedAsync(codes),
            "自定义优化");
    }

    [RelayCommand]
    private async Task RollbackSelectedAsync()
    {
        var codes = Items.Where(i => i.IsSelected).Select(i => i.Code).ToList();
        await RunAsync(
            () => _service.RollbackAsync(codes),
            "回滚选中项");
    }

    [RelayCommand]
    private async Task RollbackAllAsync()
    {
        await RunAsync(
            () => _service.RollbackAllAsync(),
            "回滚全部");
    }

    private async Task RunAsync(
        Func<Task<OptimizationBatchResult>> operation, string label)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        Status = $"{label}执行中…";
        try
        {
            var result = await operation();
            ApplyResults(result);
            await RefreshHistoryAsync();
            Status = $"{label}完成：成功 {result.SuccessCount}，失败 {result.FailureCount}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Label}执行异常", label);
            Status = $"{label}异常：{ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyResults(OptimizationBatchResult result)
    {
        foreach (var item in Items)
        {
            var single = result.Results.FirstOrDefault(r => r.ItemCode == item.Code);
            if (single is null)
            {
                continue;
            }

            item.SetState(
                single.Action == "rollback"
                    ? (single.Success ? "已回滚" : "回滚失败")
                    : (single.Success ? "已优化" : $"失败：{single.Message}"),
                single.Success);
        }
    }

    private async Task RefreshHistoryAsync()
    {
        var history = await _service.GetHistoryAsync(20);
        History.Clear();
        foreach (var record in history)
        {
            History.Add(record);
        }
    }
}
