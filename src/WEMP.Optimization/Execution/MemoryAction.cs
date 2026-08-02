using System.Diagnostics;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 内存优化执行器：结束白名单内的非必要后台用户进程；回滚时按原路径重新启动。
/// 仅处理用户级应用，不涉及系统进程。
/// </summary>
public sealed class MemoryAction : IOptimizationAction
{
    public string ItemType => "memory";

    public bool SupportsBackup => true;

    /// <summary>可安全结束的后台用户进程白名单。</summary>
    private static readonly string[] KillableProcesses =
    [
        "OneDrive", "Teams", "Skype", "Spotify", "Discord", "Slack",
        "SearchApp", "Widgets", "GameBarPresenceWriter",
    ];

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processes = new List<ProcessBackup>();
        foreach (var name in KillableProcesses)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    processes.Add(new ProcessBackup(
                        process.ProcessName, process.MainModule?.FileName ?? ""));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // 进程已退出或无权限访问模块路径
                }
            }
        }

        return Task.FromResult<object?>(processes);
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<ProcessBackup> processes)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var process in Process.GetProcesses())
        {
            if (!processes.Any(p => p.Name == process.ProcessName))
            {
                continue;
            }

            try
            {
                process.Kill(entireProcessTree: false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 进程已退出或权限不足
            }
            finally
            {
                process.Dispose();
            }
        }

        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<ProcessBackup> processes)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var process in processes)
        {
            if (string.IsNullOrEmpty(process.ExecutablePath))
            {
                continue;
            }

            try
            {
                if (Process.GetProcessesByName(process.Name).Length == 0)
                {
                    Process.Start(new ProcessStartInfo(process.ExecutablePath)
                    {
                        UseShellExecute = true,
                    });
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 启动失败不阻断其余恢复
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>进程备份数据。</summary>
public sealed record ProcessBackup(string Name, string ExecutablePath);
