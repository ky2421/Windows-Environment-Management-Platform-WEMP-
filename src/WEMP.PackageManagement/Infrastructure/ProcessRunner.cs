using System.Diagnostics;

namespace WEMP.PackageManagement.Infrastructure;

/// <summary>外部进程执行器：静默运行并捕获标准输出（UTF-8）。</summary>
public static class ProcessRunner
{
    /// <summary>执行命令并等待退出，返回退出码与输出（尾部截断）。<paramref name="environment"/> 非空时覆盖对应环境变量（其余继承当前进程）。</summary>
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        int timeoutSeconds = 180,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new CommandResult(1, $"无法启动 {fileName}", 0);
            }
        }
        catch (Exception ex)
        {
            return new CommandResult(1, $"启动失败：{ex.Message}", 0);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitTask = WaitForExitAsync(process);
        var completed = await Task.WhenAny(
                waitTask,
                Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken))
            .ConfigureAwait(false);

        if (completed != waitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 进程已退出
            }

            return new CommandResult(-1, "执行超时", timeoutSeconds);
        }

        var outputText = output.ToString();
        if (outputText.Length > 4000)
        {
            // 保留头部（含表格表头）与尾部，中间截断
            outputText = outputText[..3000] + "\n...[输出已截断]...\n" + outputText[^1000..];
        }

        return new CommandResult(process.ExitCode, outputText, (int)Math.Round(process.ExitTime.Subtract(process.StartTime).TotalSeconds));
    }

    private static Task WaitForExitAsync(Process process)
        => Task.Run(() => process.WaitForExit());
}

/// <summary>命令执行结果。</summary>
public sealed record CommandResult(int ExitCode, string Output, int DurationSeconds)
{
    public bool Success => ExitCode == 0;
}
