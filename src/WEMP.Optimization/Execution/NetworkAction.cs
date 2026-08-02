using System.Management;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 网络优化执行器：通过 WMI 读取/设置网卡 DNS（Win32_NetworkAdapterConfiguration）。
/// 修改 DNS 需要管理员权限。
/// </summary>
public sealed class NetworkAction : IOptimizationAction
{
    public string ItemType => "network";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adapters = QueryEnabledAdapters()
            .Select(row => new NetworkAdapterState(
                row["Description"]?.ToString() ?? "未知网卡",
                (row["DNSServerSearchOrder"] as string[])?.ToList() ?? []))
            .ToList();

        return Task.FromResult<object?>(adapters);
    }

    public Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dnsList = target.Dns ?? throw new ArgumentException("缺少 DNS 配置（target.dns）", nameof(target));
        if (dnsList.Count == 0)
        {
            throw new ArgumentException("DNS 列表为空", nameof(target));
        }

        foreach (var adapter in QueryEnabledAdapters())
        {
            var returnValue = adapter.InvokeMethod("SetDNSServerSearchOrder", [dnsList.ToArray()]);
            var code = Convert.ToInt32(returnValue);
            if (code != 0)
            {
                throw new InvalidOperationException(
                    $"设置 DNS 失败（代码 {code}，需管理员权限）：{adapter["Description"]}");
            }
        }

        return Task.FromResult<object?>(null);
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not List<NetworkAdapterState> states)
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        // 按备份顺序恢复：第一项为主 DNS，其余追加
        foreach (var state in states)
        {
            foreach (var adapter in QueryEnabledAdapters())
            {
                if (!string.Equals(
                    adapter["Description"]?.ToString(), state.Description,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var returnValue = adapter.InvokeMethod(
                    "SetDNSServerSearchOrder",
                    [state.Dns.Count == 0 ? null : state.Dns.ToArray()]);
                var code = Convert.ToInt32(returnValue);
                if (code != 0)
                {
                    throw new InvalidOperationException(
                        $"恢复 DNS 失败（代码 {code}）：{state.Description}");
                }
            }
        }

        return Task.CompletedTask;
    }

    private static List<ManagementObject> QueryEnabledAdapters()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\cimv2",
            "SELECT Description, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        return searcher.Get().Cast<ManagementObject>().ToList();
    }
}

/// <summary>网卡 DNS 备份数据。</summary>
public sealed record NetworkAdapterState(string Description, List<string> Dns);
