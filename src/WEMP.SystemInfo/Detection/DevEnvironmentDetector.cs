using System.Diagnostics;
using System.Text.RegularExpressions;
using WEMP.SystemInfo.Models;

namespace WEMP.SystemInfo.Detection;

/// <summary>
/// 开发环境检测器：在 PATH 中探测常用开发工具的安装情况并解析版本。
/// 每个工具独立探测、并行执行，单个工具超时 3 秒。
/// </summary>
public static partial class DevEnvironmentDetector
{
    /// <summary>单个工具探测超时。</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>统一测试入口：从任意探测输出中提取版本号（供单元测试复用）。</summary>
    public static string? ParseVersion(string output, string pattern)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = Regex.Match(output, pattern, RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>并行探测所有预置工具。</summary>
    public static async Task<List<DevToolInfo>> DetectAsync(CancellationToken cancellationToken = default)
    {
        var tasks = ToolProbes.Select(probe => ProbeAsync(probe, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results
            .Where(tool => tool.Installed)
            .OrderBy(tool => tool.DisplayName)
            .ToList();
    }

    private static async Task<DevToolInfo> ProbeAsync(ToolProbe probe, CancellationToken cancellationToken)
    {
        var info = new DevToolInfo
        {
            Name = probe.Name,
            DisplayName = probe.DisplayName,
            Executable = probe.Executable,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var output = await RunAsync(probe.Executable, probe.VersionArgs, cts.Token).ConfigureAwait(false);
            info.Version = ParseVersion(output, probe.VersionPattern);
        }
        catch (OperationCanceledException)
        {
            // 超时视为未探测成功
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 可执行文件不在 PATH 中，视为未安装
        }

        return info;
    }

    /// <summary>执行命令并返回 stdout+stderr 合并输出。</summary>
    private static async Task<string> RunAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var part in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            startInfo.ArgumentList.Add(part);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return "";
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var all = await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        return string.Concat(all);
    }

    private sealed record ToolProbe(
        string Name,
        string DisplayName,
        string Executable,
        string VersionArgs,
        string VersionPattern);

    private static readonly ToolProbe[] ToolProbes =
    [
        new("dotnet", ".NET SDK", "dotnet", "--version", @"^\s*(\S+)\s*$"),
        new("node", "Node.js", "node", "--version", @"^v?(\d+\.\d+\.\d+[^\s]*)"),
        new("npm", "npm", "npm", "--version", @"^(\d+\.\d+\.\d+)"),
        new("pnpm", "pnpm", "pnpm", "--version", @"^(\d+\.\d+\.\d+)"),
        new("yarn", "Yarn", "yarn", "--version", @"^(\d+\.\d+\.\d+)"),
        new("python", "Python", "python", "--version", @"Python (\d+\.\d+\.\d+)"),
        new("java", "Java", "java", "-version", @"version ""?([^""\s]+)"),
        new("git", "Git", "git", "--version", @"git version (\S+)"),
        new("go", "Go", "go", "version", @"go version go(\S+)"),
        new("rustc", "Rust", "rustc", "--version", @"rustc (\d+\.\d+\.\d+)"),
        new("docker", "Docker", "docker", "--version", @"Docker version ([\d.]+)"),
        new("code", "VS Code", "code", "--version", @"^(\d+\.\d+\.\d+)"),
        new("mvn", "Maven", "mvn", "--version", @"Apache Maven (\S+)"),
        new("gradle", "Gradle", "gradle", "--version", @"Gradle (\d+\.\d+(?:\.\d+)?)"),
        new("cmake", "CMake", "cmake", "--version", @"cmake version (\S+)"),
    ];
}
