using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WEMP.PackageManagement.UI;

/// <summary>操作结果 → 文本颜色（success=绿色，failed=红色）。</summary>
public sealed class ResultToBrushConverter : IValueConverter
{
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0xB6, 0x6F));
    private static readonly Brush FailedBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x53, 0x4B));
    private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x83, 0x8E));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            "success" => SuccessBrush,
            "failed" => FailedBrush,
            _ => DefaultBrush,
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
