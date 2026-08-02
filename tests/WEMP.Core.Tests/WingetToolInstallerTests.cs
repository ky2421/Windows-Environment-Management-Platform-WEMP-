using WEMP.DevEnvironment.Services;
using WEMP.PackageManagement.Infrastructure;
using WEMP.PackageManagement.Models;
using WEMP.PackageManagement.Winget;

namespace WEMP.Core.Tests;

/// <summary>winget 工具安装器测试：包 id 映射与 optional 语义。</summary>
public class WingetToolInstallerTests
{
    private sealed class FakeProvider : IPackageProvider
    {
        public CommandResult Result { get; set; } = new(0, "安装成功", 5);
        public List<string> Calls { get; } = [];

        public Task<List<WingetPackage>> ListAsync(CancellationToken ct) => Task.FromResult<List<WingetPackage>>([]);

        public Task<List<WingetPackage>> GetUpgradableAsync(CancellationToken ct)
            => Task.FromResult<List<WingetPackage>>([]);

        public Task<CommandResult> InstallAsync(string packageId, CancellationToken ct)
        {
            Calls.Add(packageId);
            return Task.FromResult(Result);
        }

        public Task<CommandResult> UninstallAsync(string packageId, CancellationToken ct)
            => Task.FromResult(new CommandResult(0, "卸载成功", 3));

        public Task<CommandResult> UpgradeAllAsync(CancellationToken ct)
            => Task.FromResult(new CommandResult(0, "升级完成", 20));
    }

    [Theory]
    [InlineData("node", "OpenJS.NodeJS.LTS")]
    [InlineData("python", "Python.Python.3.12")]
    [InlineData("git", "Git.Git")]
    [InlineData("docker", "Docker.DockerDesktop")]
    [InlineData("vscode", "Microsoft.VisualStudioCode")]
    [InlineData("typescript", null)]
    [InlineData("ruff", null)]
    [InlineData("unknown-tool", null)]
    public void ResolvePackageId_maps_known_tools(string tool, string? expected)
    {
        var installer = new WingetToolInstaller(new FakeProvider());
        Assert.Equal(expected, installer.ResolvePackageId(tool));
    }

    [Fact]
    public async Task Install_success_returns_ok()
    {
        var provider = new FakeProvider();
        var installer = new WingetToolInstaller(provider);

        var result = await installer.InstallAsync("node", "20", optional: false);

        Assert.True(result.Success);
        Assert.Equal("installed", result.Status);
        Assert.Equal(["OpenJS.NodeJS.LTS"], provider.Calls);
    }

    [Fact]
    public async Task Install_required_without_mapping_fails()
    {
        var installer = new WingetToolInstaller(new FakeProvider());

        var result = await installer.InstallAsync("typescript", null, optional: false);

        Assert.False(result.Success);
        Assert.Equal("failed", result.Status);
        Assert.Contains("无 winget 包映射", result.Message);
    }

    [Fact]
    public async Task Install_optional_without_mapping_skips()
    {
        var installer = new WingetToolInstaller(new FakeProvider());

        var result = await installer.InstallAsync("typescript", null, optional: true);

        Assert.True(result.Success);
        Assert.Equal("skipped", result.Status);
    }

    [Fact]
    public async Task Install_winget_failure_with_optional_skips()
    {
        var provider = new FakeProvider { Result = new CommandResult(1, "失败", 3) };
        var installer = new WingetToolInstaller(provider);

        var result = await installer.InstallAsync("node", null, optional: true);

        Assert.True(result.Success);
        Assert.Equal("skipped", result.Status);
        Assert.Contains("安装失败", result.Message);
    }

    [Fact]
    public async Task Install_winget_failure_with_required_fails()
    {
        var provider = new FakeProvider { Result = new CommandResult(1, "失败", 3) };
        var installer = new WingetToolInstaller(provider);

        var result = await installer.InstallAsync("node", null, optional: false);

        Assert.False(result.Success);
        Assert.Equal("failed", result.Status);
    }
}
