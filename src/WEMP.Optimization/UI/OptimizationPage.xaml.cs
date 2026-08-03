using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WEMP.Optimization.UI;

/// <summary>系统优化页面：加载后自动初始化知识库与历史记录。</summary>
public partial class OptimizationPage : UserControl
{
    /// <summary>bool → 前景色（true 绿 / false 红，深色主题色值）。</summary>
    public static readonly IValueConverter SuccessForegroundConverter =
        new BoolToBrushConverter(Color.FromRgb(0x3F, 0xB6, 0x6F), Color.FromRgb(0xE5, 0x53, 0x4B));

    public OptimizationPage(OptimizationPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is OptimizationPageViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private sealed class BoolToBrushConverter(Color trueColor, Color falseColor) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var brush = value is true ? trueColor : falseColor;
            return new SolidColorBrush(brush);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
