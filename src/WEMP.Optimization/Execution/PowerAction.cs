using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 电源计划优化执行器：切换系统活动电源方案（powercfg）。
/// 需管理员权限。高性能方案 GUID 为系统固定值。
/// </summary>
public sealed partial class PowerAction : IOptimizationAction
{
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public string ItemType => "power";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync("powercfg.exe", "/getactivescheme", cancellationToken);
        var match = GuidRegex().Match(result.Output);
        return new PowerBackup(match.Success ? match.Groups[1].Value : "");
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/setactive {HighPerformanceGuid}", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"切换电源计划失败（需管理员权限）：{result.Output.Trim()}");
        }

        return null;
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not PowerBackup b || string.IsNullOrEmpty(b.ActiveScheme))
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        var result = await ProcessRunner.RunAsync(
            "powercfg.exe", $"/setactive {b.ActiveScheme}", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"恢复电源计划失败：{result.Output.Trim()}");
        }
    }

    [GeneratedRegex(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex GuidRegex();
}

/// <summary>电源计划备份数据。</summary>
public sealed record PowerBackup(string ActiveScheme);
