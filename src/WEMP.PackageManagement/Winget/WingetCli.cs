using System.IO;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;

namespace WEMP.PackageManagement.Winget;

/// <summary>
/// winget CLI 适配层：探测 winget 可执行文件并封装常用命令
/// （list / install / uninstall / upgrade / search）。
/// </summary>
public sealed class WingetCli : IPackageProvider
{
    private const string FindArgs = "--accept-source-agreements --disable-interactivity";

    private readonly string _exePath;

    public WingetCli(string? exePath = null)
    {
        _exePath = exePath ?? FindWinget()
            ?? throw new InvalidOperationException("未找到 winget（可在应用商店安装 Windows 程序包管理器）");
    }

    public string ExePath => _exePath;

    /// <summary>探测 winget 可执行文件：PATH 优先，其次 App Execution Alias。</summary>
    public static string? FindWinget()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), "winget.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // 无效路径条目跳过
            }
        }

        // App Execution Alias（WindowsApps 目录，通常不在 PATH 中）
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(alias) ? alias : null;
    }

    /// <summary>列出已安装软件。</summary>
    public Task<List<WingetPackage>> ListAsync(CancellationToken cancellationToken)
        => RunTableAsync($"list {FindArgs}", cancellationToken);

    /// <summary>列出可升级软件。</summary>
    public Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken cancellationToken)
        => RunTableAsync($"upgrade {FindArgs}", cancellationToken);

    /// <summary>安装指定包。</summary>
    public Task<CommandResult> InstallAsync(string packageId, CancellationToken cancellationToken)
        => RunAsync($"install --id \"{packageId}\" --silent --accept-package-agreements --disable-interactivity", cancellationToken);

    /// <summary>卸载指定包。</summary>
    public Task<CommandResult> UninstallAsync(string packageId, CancellationToken cancellationToken)
        => RunAsync($"uninstall --id \"{packageId}\" --silent --disable-interactivity", cancellationToken);

    /// <summary>升级全部可升级软件。</summary>
    public Task<CommandResult> UpgradeAllAsync(CancellationToken cancellationToken)
        => RunAsync($"upgrade --all --silent --accept-package-agreements --disable-interactivity", cancellationToken);

    private async Task<List<WingetPackage>> RunTableAsync(string arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(arguments, cancellationToken);
        return WingetListParser.Parse(result.Output);
    }

    private async Task<CommandResult> RunAsync(string arguments, CancellationToken cancellationToken)
        => await ProcessRunner.RunAsync(_exePath, arguments, cancellationToken);
}
