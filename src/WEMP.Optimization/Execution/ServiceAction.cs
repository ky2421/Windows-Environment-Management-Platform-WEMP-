using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// Windows 服务优化执行器：通过 sc.exe 设置启动类型与启停服务。
/// 需要管理员权限，否则操作失败并记录原因。
/// </summary>
public sealed partial class ServiceAction : IOptimizationAction
{
    public string ItemType => "service";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var states = new List<ServiceBackup>();
        foreach (var service in target.GetServices())
        {
            if (string.IsNullOrWhiteSpace(service))
            {
                continue;
            }

            var startType = await QueryStartTypeAsync(service, cancellationToken);
            var running = await IsRunningAsync(service, cancellationToken);
            states.Add(new ServiceBackup(service, startType, running));
        }

        return states;
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 目标启动类型：默认禁用；startMode 可指定 manual/auto（如 Edge 更新服务改手动）
        var startMode = string.IsNullOrWhiteSpace(target.StartMode) ? "disabled" : target.StartMode.Trim();

        foreach (var service in target.GetServices())
        {
            if (string.IsNullOrWhiteSpace(service))
            {
                continue;
            }

            // 设置启动类型
            var config = await ProcessRunner.RunAsync(
                "sc.exe", $"config {service} start= {startMode}", cancellationToken);
            if (!config.Success)
            {
                throw new InvalidOperationException(
                    $"设置服务 {service} 启动类型失败（需管理员权限）：{config.Output.Trim()}");
            }

            // 停止服务；1062 = 服务未运行，可忽略
            var stop = await ProcessRunner.RunAsync("sc.exe", $"stop {service}", cancellationToken);
            if (!stop.Success && !stop.Output.Contains("1062", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"停止服务 {service} 失败：{stop.Output.Trim()}");
            }
        }

        return null;
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<ServiceBackup> states)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var state in states)
        {
            var startMode = state.StartType switch
            {
                "AUTO_START" => "auto",
                "DEMAND_START" => "demand",
                "DISABLED" => "disabled",
                _ => "demand",
            };

            var config = await ProcessRunner.RunAsync(
                "sc.exe", $"config {state.Name} start= {startMode}", cancellationToken);
            if (!config.Success)
            {
                throw new InvalidOperationException($"恢复服务 {state.Name} 启动类型失败：{config.Output.Trim()}");
            }

            if (state.WasRunning)
            {
                await ProcessRunner.RunAsync("sc.exe", $"start {state.Name}", cancellationToken);
            }
        }
    }

    /// <summary>查询服务启动类型（sc qc 输出解析）。</summary>
    private static async Task<string> QueryStartTypeAsync(
        string service, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("sc.exe", $"qc {service}", cancellationToken);
        var match = StartTypeRegex().Match(result.Output);
        return match.Success ? match.Groups[1].Value : "UNKNOWN";
    }

    /// <summary>查询服务是否正在运行（sc query 输出解析）。</summary>
    private static async Task<bool> IsRunningAsync(string service, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync("sc.exe", $"query {service}", cancellationToken);
        return result.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"START_TYPE\s+:\s+\d+\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex StartTypeRegex();
}

/// <summary>服务备份数据：单个服务的启动类型与运行状态。</summary>
public sealed record ServiceBackup(string Name, string StartType, bool WasRunning);
