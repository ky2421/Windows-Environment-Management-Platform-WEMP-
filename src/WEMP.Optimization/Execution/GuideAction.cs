using System.Management;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 显卡控制面板设置指引执行器：检测独立显卡厂商（NVIDIA/AMD），
/// 返回对应控制面板的推荐设置清单。驱动面板设置无公开 API，仅提供指引。
/// </summary>
public sealed class GuideAction : IOptimizationAction
{
    public string ItemType => "guide";

    public bool SupportsBackup => false;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gpu = DetectGpu();
        var message = (gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase), gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase)) switch
        {
            (true, _) => $"检测到 NVIDIA 显卡（{gpu}）：\n" +
                         "NVIDIA 控制面板 → 管理 3D 设置 → 全局设置：\n" +
                         "1. 电源管理模式：最高性能优先\n" +
                         "2. 垂直同步：关\n" +
                         "3. 低延迟模式：超高\n" +
                         "4. 纹理过滤 - 质量：高性能\n" +
                         "5. 最大预渲染帧数：1\n" +
                         "配置 Surround、PhysX：PhysX 处理器选择独立显卡\n" +
                         "提示：每个游戏可在【程序设置】单独配置，不与全局冲突",
            (_, true) => $"检测到 AMD 显卡（{gpu}）：\n" +
                         "AMD Adrenalin → 游戏 → 图形：\n" +
                         "1. Radeon Anti-Lag：开启\n" +
                         "2. Radeon Boost：开启\n" +
                         "3. 纹理过滤质量：性能\n" +
                         "4. 垂直同步：关闭",
            _ => $"未识别到 NVIDIA/AMD 独立显卡（{gpu}）。\n" +
                 "请打开显卡驱动控制面板（NVIDIA 控制面板 / AMD Adrenalin），\n" +
                 "参照电源管理模式=最高性能、垂直同步=关闭、纹理过滤=高性能 的方向手动设置。",
        };

        return Task.FromResult<object?>(message);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        => throw new NotSupportedException("显卡控制面板设置需在驱动面板中手动还原（恢复默认设置）");

    private static string DetectGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return "未检测到显卡";
        }
        catch (Exception)
        {
            return "显卡信息读取失败";
        }
    }
}

/// <summary>显卡指引备份数据（不可回滚，未使用）。</summary>
public sealed record GuideBackup(string? Message);
