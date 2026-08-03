using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WEMP.Infrastructure.Data.Entities;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Services;

namespace WEMP.PackageManagement.UI;

/// <summary>软件包管理页面视图模型。</summary>
public partial class PackageManagementPageViewModel : ObservableObject
{
    private readonly IPackageManagerService _packages;
    private readonly ISoftwareGroupService _groups;

    public ObservableCollection<InstalledSoftware> Installed { get; } = [];
    public ObservableCollection<WingetPackage> Upgradable { get; } = [];
    public ObservableCollection<SoftwareGroup> Groups { get; } = [];
    public ObservableCollection<PackageOperation> Operations { get; } = [];

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _newGroupName = "";

    [ObservableProperty]
    private string _newGroupDescription = "";

    [ObservableProperty]
    private string _newItemPackageId = "";

    [ObservableProperty]
    private SoftwareGroup? _selectedGroup;

    [RelayCommand]
    private void SelectGroup(SoftwareGroup? group)
    {
        if (group is not null)
        {
            SelectedGroup = group;
        }
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "就绪";

    [ObservableProperty]
    private bool _isAdministrator;

    /// <summary>非管理员时提示权限说明。</summary>
    public bool ShowPermissionHint => !IsAdministrator;

    public PackageManagementPageViewModel(
        IPackageManagerService packages,
        ISoftwareGroupService groups)
    {
        _packages = packages;
        _groups = groups;

        IsAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        OnPropertyChanged(nameof(ShowPermissionHint));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await LoadGroupsAsync();
            await LoadOperationsAsync();
            _ = Task.Run(RefreshInstalledAsync); // 首次同步后台执行
        }
        catch (Exception ex)
        {
            Status = $"初始化失败：{ex.Message}";
            Log.Error(ex, "软件包管理页面初始化失败");
        }
    }

    [RelayCommand]
    private async Task RefreshInstalledAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsBusy = true);
        try
        {
            var count = await _packages.SyncInstalledAsync();
            var list = await _packages.GetInstalledAsync(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);

            // ObservableCollection 的增删必须在 UI 线程（绑定了 CollectionView）
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Installed.Clear();
                foreach (var item in list)
                {
                    Installed.Add(item);
                }

                Status = $"已同步 {count} 个软件包";
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Status = $"同步失败：{ex.Message}");
            Log.Error(ex, "同步已安装软件失败");
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(InstalledSoftware? software)
    {
        if (software?.PackageId is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Status = $"正在卸载 {software.Name}…";
            var operation = await _packages.UninstallAsync(software.PackageId);
            Status = operation.Result == "success" ? $"卸载完成：{software.Name}" : $"卸载失败：{software.Name}（退出码 {operation.ExitCode}）";
            await LoadOperationsAsync();
        }
        catch (Exception ex)
        {
            Status = $"卸载失败：{ex.Message}";
            Log.Error(ex, "卸载软件失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshUpgradableAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _packages.GetUpgradableAsync();
            Upgradable.Clear();
            foreach (var item in list)
            {
                Upgradable.Add(item);
            }

            Status = $"可升级 {Upgradable.Count} 个软件包";
        }
        catch (Exception ex)
        {
            Status = $"查询可升级失败：{ex.Message}";
            Log.Error(ex, "查询可升级软件失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpgradeAllAsync()
    {
        IsBusy = true;
        try
        {
            Status = "正在升级全部软件（可能需要较长时间）…";
            var operation = await _packages.UpgradeAllAsync();
            Status = operation.Result == "success" ? "升级完成" : $"升级完成（存在失败项，退出码 {operation.ExitCode}）";
            await LoadOperationsAsync();
        }
        catch (Exception ex)
        {
            Status = $"升级失败：{ex.Message}";
            Log.Error(ex, "升级全部软件失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            Status = "请输入分组名称";
            return;
        }

        await _groups.CreateGroupAsync(NewGroupName, string.IsNullOrWhiteSpace(NewGroupDescription) ? null : NewGroupDescription);
        NewGroupName = "";
        NewGroupDescription = "";
        Status = "分组已创建";
        await LoadGroupsAsync();
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(SoftwareGroup? group)
    {
        if (group is null)
        {
            return;
        }

        await _groups.DeleteGroupAsync(group.Id);
        Status = $"已删除分组：{group.Name}";
        await LoadGroupsAsync();
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        if (SelectedGroup is null)
        {
            Status = "请先选择一个分组";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewItemPackageId))
        {
            Status = "请输入包 ID（如 Microsoft.VisualStudioCode）";
            return;
        }

        await _groups.AddItemAsync(SelectedGroup.Id, NewItemPackageId);
        NewItemPackageId = "";
        Status = "已添加到分组";
        await LoadGroupsAsync();
    }

    [RelayCommand]
    private async Task RemoveItemAsync(SoftwareGroupItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _groups.RemoveItemAsync(item.Id);
        await LoadGroupsAsync();
    }

    [RelayCommand]
    private async Task InstallGroupAsync(SoftwareGroup? group)
    {
        if (group is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Status = $"正在安装分组「{group.Name}」…";
            var count = await _groups.InstallGroupAsync(group.Id);
            Status = $"分组「{group.Name}」安装完成（{count} 个包）";
            await LoadOperationsAsync();
        }
        catch (Exception ex)
        {
            Status = $"安装失败：{ex.Message}";
            Log.Error(ex, "安装分组失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGroupsAsync()
    {
        var list = await _groups.GetGroupsAsync();
        Groups.Clear();
        foreach (var group in list)
        {
            Groups.Add(group);
        }
    }

    private async Task LoadOperationsAsync()
    {
        var list = await _packages.GetOperationsAsync(20);
        Operations.Clear();
        foreach (var operation in list)
        {
            Operations.Add(operation);
        }
    }
}
