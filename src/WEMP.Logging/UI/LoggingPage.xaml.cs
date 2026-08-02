using System.Windows.Controls;

namespace WEMP.Logging.UI;

/// <summary>日志中心页面。</summary>
public partial class LoggingPage : UserControl
{
    public LoggingPage(LoggingPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
