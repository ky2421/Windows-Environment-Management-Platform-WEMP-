using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WEMP.Optimization.UI;

/// <summary>系统优化页面：加载后自动初始化知识库与历史记录。</summary>
public partial class OptimizationPage : UserControl
{
    /// <summary>bool → 前景色（true 绿 / false 红）。</summary>
    public static readonly IValueConverter SuccessForegroundConverter =
        new BoolToBrushConverter(Colors.SeaGreen, Colors.Firebrick);

    /// <summary>结果字符串 → 前景色（success 绿 / failed 红 / 其他灰）。</summary>
    public static readonly IValueConverter ResultForegroundConverter =
        new ResultToBrushConverter();

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

    private sealed class ResultToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var color = value switch
            {
                "success" => Colors.SeaGreen,
                "failed" => Colors.Firebrick,
                _ => Colors.Gray,
            };

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
