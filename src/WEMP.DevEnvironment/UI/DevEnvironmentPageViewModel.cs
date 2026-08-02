using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.DevEnvironment.Services;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.DevEnvironment.UI;

/// <summary>开发环境页面视图模型：模板库、实例列表、部署/验证/回滚。</summary>
public partial class DevEnvironmentPageViewModel : ObservableObject
{
    private readonly IDevEnvironmentService _service;

    public ObservableCollection<EnvTemplate> Templates { get; } = [];
    public ObservableCollection<EnvInstance> Instances { get; } = [];

    [ObservableProperty]
    private EnvTemplate? _selectedTemplate;

    [ObservableProperty]
    private EnvInstance? _selectedInstance;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "就绪";

    public DevEnvironmentPageViewModel(IDevEnvironmentService service)
    {
        _service = service;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var seeded = await _service.EnsureSeedAsync();
            await LoadTemplatesAsync();
            await LoadInstancesAsync();
            Status = seeded > 0 ? $"已就绪（新增 {seeded} 个内置模板）" : "已就绪";
        }
        catch (Exception ex)
        {
            Status = $"初始化失败：{ex.Message}";
            Log.Error(ex, "开发环境页面初始化失败");
        }
    }

    [RelayCommand]
    private async Task DeployAsync()
    {
        if (SelectedTemplate is null)
        {
            Status = "请先选择模板";
            return;
        }

        IsBusy = true;
        try
        {
            Status = $"正在部署 {SelectedTemplate.Name}…";
            var instance = await _service.DeployAsync(SelectedTemplate.Id);
            await LoadInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(i => i.Id == instance.Id);
            Status = $"部署完成：{instance.Name}（{instance.Status}）";
        }
        catch (Exception ex)
        {
            Status = $"部署失败：{ex.Message}";
            Log.Error(ex, "环境部署失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (SelectedInstance is null)
        {
            Status = "请先选择实例";
            return;
        }

        IsBusy = true;
        try
        {
            Status = $"正在验证 {SelectedInstance.Name}…";
            var result = await _service.ValidateAsync(SelectedInstance.Id);
            await LoadInstancesAsync();
            Status = result.Passed ? $"验证通过：{SelectedInstance.Name}" : $"验证未通过：{result.Message}";
        }
        catch (Exception ex)
        {
            Status = $"验证失败：{ex.Message}";
            Log.Error(ex, "环境验证失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (SelectedInstance is null)
        {
            Status = "请先选择实例";
            return;
        }

        IsBusy = true;
        try
        {
            Status = $"正在回滚 {SelectedInstance.Name}…";
            await _service.RollbackAsync(SelectedInstance.Id);
            await LoadInstancesAsync();
            Status = $"已回滚：{SelectedInstance.Name}";
        }
        catch (Exception ex)
        {
            Status = $"回滚失败：{ex.Message}";
            Log.Error(ex, "环境回滚失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadTemplatesAsync()
    {
        var templates = await _service.GetTemplatesAsync();
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }
    }

    private async Task LoadInstancesAsync()
    {
        var instances = await _service.GetInstancesAsync();
        Instances.Clear();
        foreach (var instance in instances)
        {
            Instances.Add(instance);
        }
    }
}
