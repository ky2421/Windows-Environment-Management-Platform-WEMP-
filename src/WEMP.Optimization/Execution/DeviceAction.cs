using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 系统设备执行器（设备管理器）：按友好名关键词禁用/启用设备
/// （UMBus Root Bus Enumerator、高精度事件计时器 HPET 等）。
/// 需管理员权限；禁用后重新枚举可能生成新的实例 ID，回滚按关键词重新启用。
/// </summary>
public sealed partial class DeviceAction : IOptimizationAction
{
    public string ItemType => "device";

    public bool SupportsBackup => true;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<object?>(new DeviceBackup(target.DeviceKeywords ?? []));
    }

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keywords = target.DeviceKeywords;
        if (keywords is not { Count: > 0 })
        {
            throw new ArgumentException("缺少设备关键词（target.deviceKeywords）", nameof(target));
        }

        var script = BuildScript(keywords, "Disable-PnpDevice");
        var result = await RunPowershellAsync(script, cancellationToken, TimeSpan.FromMinutes(2));
        if (!result.Success)
        {
            throw new InvalidOperationException($"禁用设备失败（需管理员权限）：{result.Output.Trim()}");
        }

        var match = DisabledRegex().Match(result.Output);
        var name = match.Success ? match.Groups[1].Value : "";
        return string.IsNullOrEmpty(name)
            ? $"未找到匹配的设备（关键词：{string.Join(" / ", keywords)}），跳过"
            : $"已禁用：{name}";
    }

    public async Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (backup is not DeviceBackup b || b.Keywords is not { Count: > 0 })
        {
            throw new ArgumentException("备份数据无效", nameof(backup));
        }

        // 重新枚举匹配设备并启用（禁用后实例 ID 可能变化，不能按旧 ID 恢复）
        var script = BuildScript(b.Keywords, "Enable-PnpDevice");
        var result = await RunPowershellAsync(script, cancellationToken, TimeSpan.FromMinutes(2));
        if (!result.Success)
        {
            throw new InvalidOperationException($"启用设备失败（需管理员权限）：{result.Output.Trim()}");
        }

        var match = DisabledRegex().Match(result.Output);
        if (!match.Success || string.IsNullOrEmpty(match.Groups[1].Value))
        {
            throw new InvalidOperationException("未找到可恢复的设备，请打开设备管理器手动启用");
        }
    }

    /// <summary>构造 PowerShell：按关键词枚举 OK 状态设备并执行禁用/启用。</summary>
    private static string BuildScript(List<string> keywords, string cmdlet)
    {
        var condition = string.Join(" -or ", keywords.Select(k =>
            $"(($_.FriendlyName -like '*{k}*') -or ($_.InstanceId -like '*{k}*'))"));
        return
            $"$dev = Get-PnpDevice | Where-Object {{ ({condition}) -and ($_.Status -eq 'OK') }} | Select-Object -First 1; " +
            $"if ($dev) {{ {cmdlet} -InstanceId $dev.InstanceId -Confirm:$false; Write-Output ('changed=' + $dev.FriendlyName) }} else {{ Write-Output 'changed=' }}";
    }

    private static async Task<(bool Success, string Output)> RunPowershellAsync(
        string script, CancellationToken cancellationToken, TimeSpan timeout)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return (false, "进程启动失败");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 进程已退出
            }

            return (false, "命令执行超时");
        }

        var outputs = await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        return (process.ExitCode == 0, string.Concat(outputs));
    }

    [GeneratedRegex(@"changed=(.+)")]
    private static partial Regex DisabledRegex();
}

/// <summary>系统设备备份数据。</summary>
public sealed record DeviceBackup(List<string> Keywords);
