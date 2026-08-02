using System.Windows;
using WEMP.App.ViewModels;

namespace WEMP.App;

/// <summary>主窗口：承载导航与模块页面。</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
