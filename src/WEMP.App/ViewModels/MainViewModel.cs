using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;

namespace WEMP.App.ViewModels;

/// <summary>
/// 主窗口视图模型：聚合各模块注册的导航页面，负责页面切换时的视图/视图模型解析。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public ObservableCollection<PageRegistration> NavigationItems { get; } = [];

    [ObservableProperty]
    private PageRegistration? _selectedItem;

    [ObservableProperty]
    private object? _currentView;

    public MainViewModel(IServiceProvider services, IModuleHost moduleHost)
    {
        _services = services;

        foreach (var module in moduleHost.Modules)
        {
            foreach (var page in module.Pages)
            {
                NavigationItems.Add(page);
            }
        }

        var sorted = NavigationItems.OrderBy(p => p.Order).ToList();
        NavigationItems.Clear();
        foreach (var item in sorted)
        {
            NavigationItems.Add(item);
        }

        SelectedItem = NavigationItems.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(PageRegistration? value)
    {
        if (value is null)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            var viewModel = _services.GetRequiredService(value.ViewModelType);
            var view = _services.GetRequiredService(value.ViewType);
            if (view is FrameworkElement element)
            {
                element.DataContext = viewModel;
            }

            CurrentView = view;
        });
    }
}
