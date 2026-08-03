using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WEMP.PackageManagement.UI;

/// <summary>
/// 图标路径 → ImageSource：图片文件直接加载；exe/dll 通过系统关联图标提取。
/// 结果按路径缓存，提取失败返回 null（不显示图标）。
/// </summary>
public sealed class InstalledIconConverter : IValueConverter
{
    private static readonly Dictionary<string, ImageSource?> Cache = new();
    private static readonly object CacheLock = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null!;
        }

        lock (CacheLock)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached ?? System.Windows.DependencyProperty.UnsetValue;
            }

            var icon = LoadIcon(path);
            Cache[path] = icon;
            return icon ?? System.Windows.DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".ico" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }

            // exe/dll：提取关联图标
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            using var bitmapSource = icon.ToBitmap();
            var hBitmap = bitmapSource.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
