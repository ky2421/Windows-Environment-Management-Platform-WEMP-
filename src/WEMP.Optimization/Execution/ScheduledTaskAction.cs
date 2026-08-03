using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 计划任务优化执行器：通过 schtasks 禁用/启用计划任务。
/// 用于关闭系统遥测、兼容性数据收集等后台任务。
/// 任务不存在时视为已禁用（跳过），保证知识库条目在精简系统上仍可执行。
/// </summary>
public sealed partial class ScheduledTaskAction : IOptimizationAction
{
    public string ItemType => "scheduled-task";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var states = new List<TaskBackup>();
        foreach (var task in target.GetTasks())
        {
            if (string.IsNullOrWhiteSpace(task))
            {
                continue;
            }

            states.Add(new TaskBackup(task, await IsEnabledAsync(task, cancellationToken)));
        }

        return states;
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var task in target.GetTasks())
        {
            if (string.IsNullOrWhiteSpace(task))
            {
                continue;
            }

            // 任务不存在时忽略（本来就无此后台任务）
            var change = await ProcessRunner.RunAsync(
                "schtasks.exe", ["/Change", "/TN", task, "/DISABLE"], cancellationToken);
            if (!change.Success && !change.Output.Contains("没有找到", StringComparison.OrdinalIgnoreCase)
                                && !change.Output.Contains("not found", StringComparison.OrdinalIgnoreCase)
                                && !change.Output.Contains("0x80070002", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"禁用计划任务 {task} 失败：{change.Output.Trim()}");
            }

            // 结束正在运行的任务实例（失败忽略）
            await ProcessRunner.RunAsync(
                "schtasks.exe", ["/End", "/TN", task], cancellationToken);
        }

        return null;
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<TaskBackup> states)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var state in states)
        {
            if (!state.WasEnabled)
            {
                continue; // 原已禁用/不存在，保持现状
            }

            var change = await ProcessRunner.RunAsync(
                "schtasks.exe", ["/Change", "/TN", state.TaskPath, "/ENABLE"], cancellationToken);
            if (!change.Success)
            {
                throw new InvalidOperationException($"恢复计划任务 {state.TaskPath} 失败：{change.Output.Trim()}");
            }
        }
    }

    /// <summary>查询任务当前是否启用（schtasks /Query CSV 输出解析；任务不存在视为已禁用）。</summary>
    private static async Task<bool> IsEnabledAsync(string task, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "schtasks.exe", ["/Query", "/TN", task, "/FO", "CSV", "/NH"], cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return false; // 任务不存在/查询失败 → 视为已禁用
        }

        // CSV 末列状态：Disabled（英文）/ 已禁用（中文）；Running 也视为启用
        return !DisabledStateRegex().IsMatch(result.Output);
    }

    [GeneratedRegex(@"已禁用|Disabled", RegexOptions.IgnoreCase)]
    private static partial Regex DisabledStateRegex();
}

/// <summary>计划任务备份数据：任务路径与禁用前是否启用。</summary>
public sealed record TaskBackup(string TaskPath, bool WasEnabled);
