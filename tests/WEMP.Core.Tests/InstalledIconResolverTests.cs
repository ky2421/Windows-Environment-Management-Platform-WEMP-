using WEMP.PackageManagement.Infrastructure;

namespace WEMP.Core.Tests;

/// <summary>已安装软件图标解析测试：DisplayIcon 清理逻辑（注册表读取属环境依赖，不测）。</summary>
public class InstalledIconResolverTests
{
    [Theory]
    [InlineData("\"C:\\Apps\\App.exe\",0", "C:\\Apps\\App.exe")]
    [InlineData("\"C:\\Apps\\App.exe\",1", "C:\\Apps\\App.exe")]
    [InlineData("C:\\Apps\\App.ico", "C:\\Apps\\App.ico")]
    [InlineData("\"C:\\Apps\\My App.exe\"", "C:\\Apps\\My App.exe")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void CleanIcon_strips_quotes_and_index(string? input, string? expected)
    {
        Assert.Equal(expected, InstalledIconResolver.CleanIcon(input));
    }

    [Fact]
    public void Resolve_unknown_name_returns_null_without_throwing()
    {
        // 未命中注册表条目时安全返回 null（不依赖具体环境软件）
        Assert.Null(InstalledIconResolver.Resolve("__wemp_definitely_not_installed__"));
        Assert.Null(InstalledIconResolver.Resolve(""));
        Assert.Null(InstalledIconResolver.Resolve(null));
    }

    [Theory]
    [InlineData("Google Chrome", "googlechrome")]
    [InlineData("Google Chrome (x64)", "googlechrome")]
    [InlineData("Visual Studio Code", "visualstudiocode")]
    [InlineData("7-Zip 24.08 (x64)", "7-zip")]
    [InlineData("Notepad++ 7.6.6", "notepad++")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeName_ignores_case_whitespace_brackets_and_version(string? input, string expected)
    {
        Assert.Equal(expected, InstalledIconResolver.NormalizeName(input));
    }
}
