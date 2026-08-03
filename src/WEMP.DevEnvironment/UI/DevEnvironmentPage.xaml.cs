using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WEMP.DevEnvironment.UI;

/// <summary>开发环境页面：加载完成后初始化模板库与实例列表。</summary>
public partial class DevEnvironmentPage : UserControl
{
    public DevEnvironmentPage(DevEnvironmentPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is DevEnvironmentPageViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    /// <summary>
    /// 鼠标停留在卡片区时直接用滚轮滚动：ListBox 会拦截滚轮事件但自身不可滚，
    /// 这里把滚轮事件转发给外层 ScrollViewer。
    /// </summary>
    private void OnCardAreaPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scroller)
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
