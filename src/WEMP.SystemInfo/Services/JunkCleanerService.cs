using System.IO;
using System.Runtime.InteropServices;

namespace WEMP.SystemInfo.Services;

/// <summary>垃圾清理结果。</summary>
public sealed record JunkCleanResult(int FilesCleaned, long FreedBytes);

/// <summary>
/// 磁盘垃圾清理服务：统计并清理用户/系统临时文件，清空回收站。
/// 删除失败（占用、权限）的文件自动跳过，不会中断清理。
/// </summary>
public sealed class JunkCleanerService
{
    /// <summary>扫描统计垃圾文件（不删除）。</summary>
    public JunkCleanResult Scan()
    {
        var (files, bytes) = EnumerateTemporaryFiles(measureOnly: true);
        return new JunkCleanResult(files, bytes);
    }

    /// <summary>清理临时文件并清空回收站，返回实际清理统计。</summary>
    public JunkCleanResult Clean()
    {
        var (files, bytes) = EnumerateTemporaryFiles(measureOnly: false);
        EmptyRecycleBin();
        return new JunkCleanResult(files, bytes);
    }

    private static (int Files, long Bytes) EnumerateTemporaryFiles(bool measureOnly)
    {
        var files = 0;
        long freed = 0;

        foreach (var root in GetTempRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in SafeEnumerateFiles(root))
            {
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    if (!measureOnly)
                    {
                        info.Delete();
                    }

                    files++;
                    freed += size;
                }
                catch (IOException)
                {
                    // 文件被占用，跳过
                }
                catch (UnauthorizedAccessException)
                {
                    // 权限不足，跳过
                }
            }
        }

        return (files, freed);
    }

    /// <summary>临时目录集合：用户临时目录 + Windows 临时目录 + 本地应用数据临时目录。</summary>
    private static IEnumerable<string> GetTempRoots()
    {
        var roots = new List<string> { Path.GetTempPath() };
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.LocalApplicationData,
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path))
            {
                roots.Add(Path.Combine(path, "Temp"));
            }
        }

        return roots;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return files.Take(5000); // 单次最多处理 5000 个文件，避免长时间扫描
    }

    private static void EmptyRecycleBin()
    {
        try
        {
            _ = SHEmptyRecycleBin(IntPtr.Zero, null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        }
        catch (Exception)
        {
            // 回收站清空失败不影响临时文件清理结果
        }
    }

    private const uint SHERB_NOCONFIRMATION = 0x1;
    private const uint SHERB_NOPROGRESSUI = 0x2;
    private const uint SHERB_NOSOUND = 0x4;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
