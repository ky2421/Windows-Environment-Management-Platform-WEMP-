using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog;
using WEMP.Optimization.Execution;

namespace WEMP.GameMode.Services;

/// <summary>游戏模式系统状态切换抽象（便于测试注入替身）。</summary>
public interface IGameStateSwitcher
{
    /// <summary>进入游戏模式，返回用于恢复的状态快照。</summary>
    Task<GameStateSnapshot> EnterGameModeAsync(CancellationToken cancellationToken);

    /// <summary>退出游戏模式，按快照恢复系统状态。</summary>
    Task RestoreAsync(GameStateSnapshot snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// 游戏模式系统状态切换：进入游戏时切换高性能电源计划并释放非必要后台进程，
/// 退出时恢复原电源计划并重启已结束的进程。切换前保存快照用于恢复。
/// </summary>
public sealed partial class SystemStateSwitcher : IGameStateSwitcher
{
    private const string HighPerformanceScheme = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    /// <summary>进入游戏时释放的非必要后台进程白名单。</summary>
    private static readonly string[] ReleaseProcesses =
    [
        "OneDrive", "Teams", "Skype", "Spotify", "Discord", "Slack",
        "SearchApp", "Widgets", "GameBarPresenceWriter",
    ];

    /// <summary>进入游戏模式，返回用于恢复的状态快照。</summary>
    public async Task<GameStateSnapshot> EnterGameModeAsync(CancellationToken cancellationToken)
    {
        var originalScheme = await GetActiveSchemeAsync(cancellationToken);
        var killed = new List<ProcessBackup>();

        // 1. 切换高性能电源计划
        var powerResult = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/setactive {HighPerformanceScheme}", cancellationToken);
        if (!powerResult.Success)
        {
            Log.Warning("游戏模式切换电源计划失败（需管理员权限）：{Error}", powerResult.Output.Trim());
        }

        // 2. 结束非必要后台进程（记录路径，退出时重启）
        foreach (var name in ReleaseProcesses)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    var path = process.MainModule?.FileName ?? "";
                    process.Kill(entireProcessTree: false);
                    killed.Add(new ProcessBackup(process.ProcessName, path));
                    Log.Information("游戏模式释放后台进程：{Process}", process.ProcessName);
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
        }

        return new GameStateSnapshot(originalScheme, killed);
    }

    /// <summary>退出游戏模式，按快照恢复系统状态。</summary>
    public async Task RestoreAsync(GameStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(snapshot.OriginalScheme))
        {
            var powerResult = await ProcessRunner.RunAsync(
                "powercfg.exe", $"/setactive {snapshot.OriginalScheme}", cancellationToken);
            if (!powerResult.Success)
            {
                Log.Warning("游戏模式恢复电源计划失败：{Error}", powerResult.Output.Trim());
            }
        }

        foreach (var process in snapshot.KilledProcesses)
        {
            if (string.IsNullOrEmpty(process.ExecutablePath))
            {
                continue;
            }

            try
            {
                if (Process.GetProcessesByName(process.Name).Length == 0)
                {
                    Process.Start(new ProcessStartInfo(process.ExecutablePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Log.Warning("游戏模式恢复进程失败：{Process} - {Error}", process.Name, ex.Message);
            }
        }
    }

    private static async Task<string> GetActiveSchemeAsync(CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("powercfg.exe", "/getactivescheme", cancellationToken);
        var match = SchemeGuidRegex().Match(result.Output);
        return match.Success ? match.Groups[1].Value : "";
    }

    [GeneratedRegex(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex SchemeGuidRegex();
}

/// <summary>游戏模式系统状态快照（用于退出时恢复）。</summary>
public sealed record GameStateSnapshot(string OriginalScheme, List<ProcessBackup> KilledProcesses);
