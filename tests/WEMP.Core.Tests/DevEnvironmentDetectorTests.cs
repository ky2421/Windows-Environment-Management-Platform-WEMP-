using WEMP.SystemInfo.Detection;

namespace WEMP.Core.Tests;

/// <summary>开发环境检测器的版本解析逻辑（纯函数，不依赖真实进程）。</summary>
public class DevEnvironmentDetectorTests
{
    [Theory]
    [InlineData("8.0.423\r\n", @"^\s*(\S+)\s*$", "8.0.423")]
    [InlineData("v20.11.0\r\n", @"^v?(\d+\.\d+\.\d+[^\s]*)", "20.11.0")]
    [InlineData("Python 3.12.1\r\n", @"Python (\d+\.\d+\.\d+)", "3.12.1")]
    [InlineData("git version 2.43.0.windows.1\r\n", @"git version (\S+)", "2.43.0.windows.1")]
    [InlineData("go version go1.22.4 windows/amd64\r\n", @"go version go(\S+)", "1.22.4")]
    [InlineData("openjdk version \"17.0.9\" 2023-10-17\r\n", @"version ""?([^""\s]+)", "17.0.9")]
    [InlineData("Apache Maven 3.9.6 (bc0240f3c74424f...)\r\n", @"Apache Maven (\S+)", "3.9.6")]
    [InlineData("Docker version 26.1.1, build 4cf5afa\r\n", @"Docker version ([\d.]+)", "26.1.1")]
    [InlineData("this tool is not installed", @"Python (\d+\.\d+\.\d+)", null)]
    [InlineData("", @"^\s*(\S+)\s*$", null)]
    public void ParseVersion_extracts_expected_version(string output, string pattern, string? expected)
    {
        var actual = DevEnvironmentDetector.ParseVersion(output, pattern);
        Assert.Equal(expected, actual);
    }
}
