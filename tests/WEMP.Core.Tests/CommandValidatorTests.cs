using WEMP.DevEnvironment.Services;

namespace WEMP.Core.Tests;

/// <summary>验证命令执行测试：PATH 注入与输出匹配。</summary>
public class CommandValidatorTests
{
    [Fact]
    public void BuildLatestPath_merges_machine_and_user_paths()
    {
        var path = CommandValidator.BuildLatestPath();

        Assert.False(string.IsNullOrWhiteSpace(path));
        // 系统目录（机器 PATH）必须存在
        Assert.Contains("System32", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_passes_when_output_matches_expected()
    {
        var validator = new CommandValidator();

        var result = await validator.ValidateAsync("echo hello", "hello");

        Assert.True(result.Passed);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task ValidateAsync_fails_when_output_not_match()
    {
        var validator = new CommandValidator();

        var result = await validator.ValidateAsync("echo hello", "world");

        Assert.False(result.Passed);
        Assert.Contains("未匹配", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_resolves_command_from_registry_path()
    {
        // 验证 PATH 注入生效：仅凭注册表 PATH 也应能解析系统命令
        var validator = new CommandValidator();

        var result = await validator.ValidateAsync("where cmd", "cmd.exe");

        Assert.True(result.Passed);
    }
}
