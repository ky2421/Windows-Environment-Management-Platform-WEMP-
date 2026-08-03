using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// Windows 可选功能优化执行器：通过 dism 卸载/启用冷门组件
/// （IE11、PowerShell 2.0、TIFF 过滤器、XPS 查看器、传真和扫描等）。
/// 功能不存在时视为已卸载（跳过），保证知识库条目在精简系统上仍可执行。
/// </summary>
public sealed partial class WindowsFeatureAction : IOptimizationAction
{
    /// <summary>DISM 组件操作较慢，超时放宽到 2 分钟。</summary>
    private static readonly TimeSpan FeatureTimeout = TimeSpan.FromSeconds(120);

    public string ItemType => "windows-feature";

    public bool SupportsBackup => true;

    public async Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var states = new List<FeatureBackup>();
        foreach (var feature in target.GetFeatureNames())
        {
            if (string.IsNullOrWhiteSpace(feature))
            {
                continue;
            }

            states.Add(new FeatureBackup(feature, await IsEnabledAsync(feature, cancellationToken)));
        }

        return states;
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var feature in target.GetFeatureNames())
        {
            if (string.IsNullOrWhiteSpace(feature))
            {
                continue;
            }

            var result = await ProcessRunner.RunAsync(
                "dism.exe", ["/online", "/disable-feature", $"/featurename:{feature}", "/norestart"],
                cancellationToken, FeatureTimeout);
            if (!result.Success && !result.Output.Contains("0x800f080c", StringComparison.OrdinalIgnoreCase))
            {
                // 0x800f080c = 功能不存在，忽略
                throw new InvalidOperationException($"卸载可选功能 {feature} 失败：{result.Output.Trim()}");
            }
        }

        return null;
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<FeatureBackup> states)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        foreach (var state in states)
        {
            if (!state.WasEnabled)
            {
                continue; // 原已卸载，保持现状
            }

            var result = await ProcessRunner.RunAsync(
                "dism.exe", ["/online", "/enable-feature", $"/featurename:{state.FeatureName}", "/norestart"],
                cancellationToken, FeatureTimeout);
            if (!result.Success)
            {
                throw new InvalidOperationException($"恢复可选功能 {state.FeatureName} 失败：{result.Output.Trim()}");
            }
        }
    }

    /// <summary>查询功能是否启用（dism get-featureinfo 输出解析；功能不存在视为已卸载）。</summary>
    private static async Task<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            "dism.exe", ["/online", "/get-featureinfo", $"/featurename:{feature}"], cancellationToken, FeatureTimeout);
        if (!result.Success)
        {
            return false;
        }

        // 状态行：英文 "State : Enabled" / 中文 "状态 : 已启用"
        return EnabledStateRegex().IsMatch(result.Output);
    }

    [GeneratedRegex(@"State\s*:\s*Enabled|状态\s*:\s*已启用", RegexOptions.IgnoreCase)]
    private static partial Regex EnabledStateRegex();
}

/// <summary>可选功能备份数据：功能名与卸载前是否启用。</summary>
public sealed record FeatureBackup(string FeatureName, bool WasEnabled);
