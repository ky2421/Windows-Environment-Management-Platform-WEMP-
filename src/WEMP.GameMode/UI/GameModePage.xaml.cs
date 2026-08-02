using System.Windows;
using System.Windows.Controls;

namespace WEMP.GameMode.UI;

/// <summary>游戏模式页面：加载后初始化会话状态并订阅事件。</summary>
public partial class GameModePage : UserControl
{
    public GameModePage(GameModePageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is GameModePageViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        // 页面关闭时无需特殊处理；会话由 Monitor 持续管理
    }
}
