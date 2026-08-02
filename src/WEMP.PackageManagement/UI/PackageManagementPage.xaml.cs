using System.Windows;
using System.Windows.Controls;

namespace WEMP.PackageManagement.UI;

/// <summary>软件包管理页面。</summary>
public partial class PackageManagementPage : UserControl
{
    public PackageManagementPage(PackageManagementPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is PackageManagementPageViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
