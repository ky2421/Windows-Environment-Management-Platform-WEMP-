using System;
using System.Globalization;
using System.Windows.Data;

namespace WEMP.App.Converters;

/// <summary>
/// 把模块页面 Key 映射为 Segoe MDL2 Assets 图标码点（Unicode 转义形式）。
/// 未识别的 Key 返回空串。
/// </summary>
public sealed class PageIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var glyph = value switch
        {
            "system-info" => "\uE7F8",   // PC
            "optimization" => "\uE713",  // Settings
            "gamemode" => "\uE7AE",      // Play
            "packages" => "\uE8F1",      // Download
            "devenv" => "\uE943",        // DeveloperTools
            "logging" => "\uE7C3",       // History
            "backup" => "\uE7B8",        // Upload
            _ => string.Empty,
        };
        return glyph;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
