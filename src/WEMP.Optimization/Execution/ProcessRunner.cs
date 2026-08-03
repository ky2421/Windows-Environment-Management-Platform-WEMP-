using System.Diagnostics;

namespace WEMP.Optimization.Execution;

/// <summary>外部命令执行工具：静默运行并捕获输出。</summary>
public static class ProcessRunner
{
    /// <summary>执行外部命令（参数按空格拆分），返回退出码与合并输出。超时视为失败。</summary>
    public static Task<CommandResult> RunAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
        => RunAsync(executable,
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            cancellationToken, timeout);

    /// <summary>执行外部命令（参数数组原样传入，支持含空格的路径/参数），返回退出码与合并输出。超时视为失败。</summary>
    public static async Task<CommandResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new CommandResult(-1, "进程启动失败");
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

            return new CommandResult(-1, "命令执行超时");
        }

        var outputs = await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        return new CommandResult(process.ExitCode, string.Concat(outputs));
    }

    /// <summary>命令执行结果。</summary>
    public sealed record CommandResult(int ExitCode, string Output)
    {
        public bool Success => ExitCode == 0;
    }
}
