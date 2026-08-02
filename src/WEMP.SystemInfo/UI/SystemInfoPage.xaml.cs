using System.Windows;
using System.Windows.Controls;

namespace WEMP.SystemInfo.UI;

/// <summary>系统检测页面：加载完成后自动执行一次检测。</summary>
public partial class SystemInfoPage : UserControl
{
    public SystemInfoPage(SystemInfoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is SystemInfoViewModel viewModel && viewModel.DetectCommand.CanExecute(null))
        {
            await viewModel.DetectCommand.ExecuteAsync(null);
        }
    }
}
