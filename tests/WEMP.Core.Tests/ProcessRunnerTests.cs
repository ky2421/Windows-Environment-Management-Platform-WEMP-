using WEMP.PackageManagement.Infrastructure;

namespace WEMP.Core.Tests;

/// <summary>
/// 外部进程执行器测试：输出捕获、环境变量注入、超时终止、启动失败与长输出截断。
/// 不依赖 winget，全部使用 cmd.exe 内建命令，可在无管理权限环境运行。
/// </summary>
public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_captures_exit_code_and_output()
    {
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c echo hello", CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
        Assert.Contains("hello", result.Output);
    }

    [Fact]
    public async Task RunAsync_reports_nonzero_exit_code()
    {
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c exit 3", CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RunAsync_injects_environment_overrides()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c echo %WEMP_TEST_VAR%",
            CancellationToken.None,
            environment: new Dictionary<string, string> { ["WEMP_TEST_VAR"] = "injected-value" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("injected-value", result.Output);
    }

    [Fact]
    public async Task RunAsync_inherits_process_environment_when_no_overrides()
    {
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c echo %SystemRoot%", CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Windows", result.Output);
    }

    [Fact]
    public async Task RunAsync_fails_gracefully_when_executable_missing()
    {
        var result = await ProcessRunner.RunAsync(
            Path.Combine(Path.GetTempPath(), "wemp-no-such-exe-xyz.exe"),
            string.Empty,
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Success);
        Assert.Contains("启动失败", result.Output);
    }

    [Fact]
    public async Task RunAsync_times_out_and_kills_process()
    {
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c ping -n 30 127.0.0.1",
            CancellationToken.None,
            timeoutSeconds: 1);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("执行超时", result.Output);
        Assert.Equal(1, result.DurationSeconds);
    }

    [Fact]
    public async Task RunAsync_truncates_long_output_keeping_head_and_tail()
    {
        // 200 行 × 30 字符 ≈ 6000 字符，超过 4000 截断阈值
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c for /L %i in (1,1,200) do @echo 123456789012345678901234567890",
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[输出已截断]", result.Output);
        Assert.True(result.Output.Length < 4500, $"输出未被截断：{result.Output.Length} 字符");
    }

    [Fact]
    public async Task RunAsync_with_short_output_is_not_truncated()
    {
        var result = await ProcessRunner.RunAsync("cmd.exe", "/c echo short", CancellationToken.None);

        Assert.DoesNotContain("[输出已截断]", result.Output);
    }

    [Fact]
    public async Task RunAsync_with_cancelled_token_returns_timeout_result()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            "/c ping -n 30 127.0.0.1",
            cts.Token,
            timeoutSeconds: 30);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("执行超时", result.Output);
    }
}
