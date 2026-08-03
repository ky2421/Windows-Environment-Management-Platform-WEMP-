using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.DevEnvironment.Models;
using WEMP.DevEnvironment.Parsing;
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

    [ObservableProperty]
    private bool _isAdministrator;

    /// <summary>非管理员时提示权限说明。</summary>
    public bool ShowPermissionHint => !IsAdministrator;

    /// <summary>部署实例列表是否展开（默认收起，点击“部署实例”按钮展开）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstancesToggleText))]
    private bool _isInstancesExpanded;

    public string InstancesToggleText => IsInstancesExpanded ? "收起部署实例 ▾" : "部署实例 ▸";

    [RelayCommand]
    private void ToggleInstances() => IsInstancesExpanded = !IsInstancesExpanded;

    public DevEnvironmentPageViewModel(IDevEnvironmentService service)
    {
        _service = service;

        IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        OnPropertyChanged(nameof(ShowPermissionHint));
    }

    public async Task InitializeAsync()
    {
        Log.Information("开发环境页面初始化开始");
        try
        {
            var seeded = await _service.EnsureSeedAsync();
            await LoadTemplatesAsync();
            await LoadInstancesAsync();
            Log.Information("开发环境页面初始化完成：模板 {TemplateCount} 个，实例 {InstanceCount} 个，seeded={Seeded}", Templates.Count, Instances.Count, seeded);
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

        EnvTemplateSpec spec;
        try
        {
            spec = EnvTemplateParser.Parse(SelectedTemplate.Content);
        }
        catch (Exception ex)
        {
            Status = $"模板解析失败：{ex.Message}";
            Log.Error(ex, "模板解析失败：{Id}", SelectedTemplate.Id);
            return;
        }

        // 弹窗让用户选择要安装的工具
        Log.Information("部署前弹窗：模板 {Template}，工具 {Count} 个", SelectedTemplate.Name, spec.Tools.Count);
        try
        {
            var picker = new TemplateToolPickerWindow(spec)
            {
                Owner = Application.Current.MainWindow,
            };
            var dialogResult = picker.ShowDialog();
            Log.Information("工具选择弹窗返回：{Result}", dialogResult);
            if (dialogResult != true || picker.SelectedToolNames is null)
            {
                return; // 用户取消
            }

            // 模态进度对话框：部署流水线在对话框内异步执行，完成后自动关闭
            Status = $"正在部署 {SelectedTemplate.Name}…";
            var progressWindow = new DeployProgressWindow(SelectedTemplate.Name, progress =>
                _service.DeployAsync(SelectedTemplate.Id, selectedTools: picker.SelectedToolNames, progress: progress))
            {
                Owner = Application.Current.MainWindow,
            };
            progressWindow.ShowDialog();

            if (progressWindow.ErrorMessage is not null)
            {
                Status = $"部署失败：{progressWindow.ErrorMessage}";
                return;
            }

            if (progressWindow.DeployedInstance is not null)
            {
                await LoadInstancesAsync();
                SelectedInstance = Instances.FirstOrDefault(i => i.Id == progressWindow.DeployedInstance.Id);
                Status = $"部署完成：{progressWindow.DeployedInstance.Name}（{progressWindow.DeployedInstance.Status}）";
            }
        }
        catch (Exception ex)
        {
            Status = $"工具选择弹窗失败：{ex.Message}";
            Log.Error(ex, "工具选择弹窗失败：模板 {Template}", SelectedTemplate.Name);
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

        var instanceId = SelectedInstance.Id;
        var instanceName = SelectedInstance.Name;
        IsBusy = true;
        try
        {
            Status = $"正在验证 {instanceName}…";
            var result = await _service.ValidateAsync(instanceId);
            await LoadInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(i => i.Id == instanceId);
            Status = result.Passed ? $"验证通过：{instanceName}" : $"验证未通过：{result.Message}";
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

        var instanceId = SelectedInstance.Id;
        var instanceName = SelectedInstance.Name;
        IsBusy = true;
        try
        {
            Status = $"正在回滚 {instanceName}…";
            await _service.RollbackAsync(instanceId);
            await LoadInstancesAsync();
            SelectedInstance = Instances.FirstOrDefault(i => i.Id == instanceId);
            Status = $"已回滚：{instanceName}";
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
        // IDE 模板排到最后，其余按原顺序（Id）排列
        foreach (var template in templates.OrderBy(t => IdeTemplateKeys.Contains(t.TemplateKey)))
        {
            Templates.Add(template);
        }
    }

    /// <summary>归属“常用IDE开发工具”的模板键，排序时排到最后。</summary>
    private static readonly HashSet<string> IdeTemplateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "vscode",
        "intellij-idea",
        "pycharm",
        "webstorm",
        "clion",
        "androidstudio",
        "ide-common",
    };

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
