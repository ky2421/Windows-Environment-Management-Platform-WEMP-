using System.Diagnostics;
using System.Text.RegularExpressions;
using WEMP.Optimization.Models;

namespace WEMP.Optimization.Execution;

/// <summary>
/// 预装应用卸载执行器：卸载天气、地图、Xbox 套件、Candy Crush 等系统预装应用。
/// 不可回滚（卸载后需通过商店重新安装）。
/// </summary>
public sealed partial class AppxAction : IOptimizationAction
{
    public string ItemType => "appx";

    public bool SupportsBackup => false;

    public Task<object?> BackupAsync(OptimizationTarget target, CancellationToken cancellationToken)
        => Task.FromResult<object?>(null);

    public async Task<object?> ApplyAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string script =
            "$pkgs = Get-AppxPackage | Where-Object { $_.Name -match '^Microsoft\\.(Weather|WindowsMaps|Xbox)' -or $_.Name -match '^King\\.com\\.(CandyCrush|FarmHeroes)' }; " +
            "$pkgs | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue }; " +
            "Write-Output ('removed=' + $pkgs.Count)";

        var result = await RunPowershellAsync(script, cancellationToken, TimeSpan.FromMinutes(5));
        if (!result.Success)
        {
            throw new InvalidOperationException($"卸载预装应用失败：{result.Output.Trim()}");
        }

        var removed = 0;
        var match = RemovedRegex().Match(result.Output);
        if (match.Success)
        {
            _ = int.TryParse(match.Groups[1].Value, out removed);
        }

        return removed == 0
            ? "未发现可卸载的预装应用（天气/地图/Xbox/Candy Crush）"
            : $"已卸载 {removed} 个预装应用";
    }

    public Task RestoreAsync(OptimizationTarget target, object? backup, CancellationToken cancellationToken)
        => throw new NotSupportedException("卸载预装应用不可回滚（请通过 Microsoft Store 重新安装）");

    private static async Task<(bool Success, string Output)> RunPowershellAsync(
        string script, CancellationToken cancellationToken, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
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

        using var process = new Process { StartInfo = startInfo };
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

    [GeneratedRegex(@"removed=(\d+)")]
    private static partial Regex RemovedRegex();
}
