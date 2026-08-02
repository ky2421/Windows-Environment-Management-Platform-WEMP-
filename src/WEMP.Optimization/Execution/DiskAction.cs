using System.IO;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 磁盘优化执行器：
/// - disk.cleanup：清理临时文件（不可回滚，仅记录清理统计）
/// - disk.hiberfil：关闭/开启休眠（powercfg /h，白名单命令）
/// </summary>
public sealed class DiskAction : IOptimizationAction
{
    public string ItemType => "disk";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsHibernationCommand(target))
        {
            return Task.FromResult<object?>(new DiskBackup(HibernationEnabled: File.Exists(@"C:\hiberfil.sys")));
        }

        // 清理类操作不可回滚
        return Task.FromResult<object?>(null);
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsHibernationCommand(target))
        {
            var result = await ProcessRunner.RunAsync(
                "powercfg.exe", "/h off", cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"关闭休眠失败（需管理员权限）：{result.Output.Trim()}");
            }

            return new CleanupStats(0, 0, "休眠已关闭");
        }

        // 清理临时文件
        var paths = new List<string>
        {
            Path.GetTempPath(),
        };
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.LocalApplicationData,
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(Path.Combine(path, "Temp"));
            }
        }

        var (files, freed) = CleanTemporaryFiles(paths);
        return new CleanupStats(files, freed, $"已清理 {files} 个临时文件，释放约 {FormatSize(freed)}");
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsHibernationCommand(target))
        {
            var result = await ProcessRunner.RunAsync("powercfg.exe", "/h on", cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"开启休眠失败：{result.Output.Trim()}");
            }
        }
        else
        {
            throw new NotSupportedException("清理类优化不可回滚");
        }

        await Task.CompletedTask;
    }

    private static bool IsHibernationCommand(OptimizationTarget target)
    {
        return target.Command?.Contains("hiberfil", StringComparison.OrdinalIgnoreCase) == true
            || target.Command?.Contains("powercfg /h", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static (int Files, long FreedBytes) CleanTemporaryFiles(IEnumerable<string> roots)
    {
        var files = 0;
        long freed = 0;

        foreach (var root in roots)
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
                    info.Delete();
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

        return files.Take(5000); // 单次最多清理 5000 个文件，避免长时间扫描
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F1} MB" : $"{bytes / 1024.0:F0} KB";
}

/// <summary>磁盘备份数据。</summary>
public sealed record DiskBackup(bool HibernationEnabled);

/// <summary>清理统计（作为优化后结果数据）。</summary>
public sealed record CleanupStats(int FilesCleaned, long FreedBytes, string Message);
