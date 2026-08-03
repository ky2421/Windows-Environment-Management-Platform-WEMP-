using System.Globalization;
using System.Windows.Data;

namespace WEMP.SystemInfo.UI;

/// <summary>
/// 将 0-100 的评分转换为环形仪表 Path 的弧线数据。
/// 画布 120x120、圆心 (60,60)、半径 54，弧线自 12 点方向顺时针绘制。
/// </summary>
public sealed class ScoreToArcConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var score = value is double d ? d : 0;
        score = Math.Clamp(score, 0, 100);

        if (score <= 0)
        {
            // 零分时返回空弧
            return "M60,60 L60,60";
        }

        // 100 分时避免 ArcSegment 退化为零长度弧
        var angle = score >= 100 ? 359.9 : score * 3.6;
        var rad = angle * Math.PI / 180.0;
        var x = 60 + 54 * Math.Sin(rad);
        var y = 60 - 54 * Math.Cos(rad);
        var largeArc = angle > 180 ? 1 : 0;
        return $"M60,6 A54,54 0 {largeArc} 1 {x:F1},{y:F1}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
