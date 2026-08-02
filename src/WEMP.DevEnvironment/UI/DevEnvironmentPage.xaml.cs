using System.Windows.Controls;

namespace WEMP.DevEnvironment.UI;

/// <summary>开发环境页面。</summary>
public partial class DevEnvironmentPage : UserControl
{
    public DevEnvironmentPage(DevEnvironmentPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
