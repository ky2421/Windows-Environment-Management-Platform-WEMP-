using System.Text;
using WEMP.Optimization.Execution;

namespace WEMP.Core.Tests;

/// <summary>
/// 优化执行层纯逻辑测试：不触碰真实系统/注册表，
/// 覆盖 HAGS 标志更新、启动项关键字匹配与 GPU 偏好二进制布局。
/// </summary>
public class OptimizationActionLogicTests
{
    // ---------- HagAction.UpdateHagsFlag ----------

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", false)]
    public void UpdateHagsFlag_empty_current_creates_flag(string? current, bool enable)
    {
        var updated = HagAction.UpdateHagsFlag(current, enable);

        Assert.Equal($"SwapEffectUpgradeEnable={(enable ? "0x1" : "0x0")};", updated);
    }

    [Theory]
    [InlineData("SwapEffectUpgradeEnable=0x0;", true, "SwapEffectUpgradeEnable=0x1;")]
    [InlineData("SwapEffectUpgradeEnable=0x1;", false, "SwapEffectUpgradeEnable=0x0;")]
    [InlineData("Foo=Bar;SwapEffectUpgradeEnable=0x1;", true, "Foo=Bar;SwapEffectUpgradeEnable=0x1;")]
    public void UpdateHagsFlag_replaces_existing_flag_in_place(string current, bool enable, string expected)
    {
        Assert.Equal(expected, HagAction.UpdateHagsFlag(current, enable));
    }

    [Theory]
    [InlineData("Foo=Bar;", true, "Foo=Bar;SwapEffectUpgradeEnable=0x1;")]
    [InlineData("Foo=Bar", false, "Foo=Bar;SwapEffectUpgradeEnable=0x0;")]
    public void UpdateHagsFlag_appends_when_flag_missing(string current, bool enable, string expected)
    {
        Assert.Equal(expected, HagAction.UpdateHagsFlag(current, enable));
    }

    // ---------- StartupAction 关键字匹配 ----------

    [Fact]
    public void MatchesVendor_detects_oem_keywords_in_name_or_path()
    {
        var nvidia = new StartupEntry("HKCU", "Run", "NVIDIA GeForce", "C:\\nvidia\\app.exe", false);
        var lenovo = new StartupEntry("HKLM", "Run", "LenovoVantage", "C:\\lenovo\\vantage.exe", false);
        var clean = new StartupEntry("HKCU", "Run", "MyApp", "C:\\tools\\myapp.exe", false);

        Assert.True(StartupAction.MatchesVendor(nvidia));
        Assert.True(StartupAction.MatchesVendor(lenovo));
        Assert.False(StartupAction.MatchesVendor(clean));
    }

    [Fact]
    public void MatchesAny_matches_any_keyword_case_insensitively()
    {
        var entry = new StartupEntry("HKCU", "Run", "Realtek Audio", "C:\\realtek\\snd.exe", false);

        Assert.True(StartupAction.MatchesAny(entry, ["realtek", "声卡"]));
        Assert.True(StartupAction.MatchesAny(entry, ["AUDIO"]));
        Assert.False(StartupAction.MatchesAny(entry, ["intel", "nv"]));
    }

    // ---------- GpuAction 主程序过滤 ----------

    [Theory]
    [InlineData(@"C:\Games\Cyberpunk\Cyberpunk2077.exe", true)]
    [InlineData(@"C:\Games\Game\unins000.exe", false)]
    [InlineData(@"C:\Games\Game\Uninstall.exe", false)]
    [InlineData(@"C:\Games\Game\setup.exe", false)]
    [InlineData(@"C:\Games\Game\vc_redist.exe", false)]
    [InlineData(@"C:\Games\Game\CrashReporter.exe", false)]
    [InlineData(@"C:\Games\Game\GameUpdater.exe", false)]
    public void IsMainExecutable_filters_helper_programs(string path, bool expected)
    {
        Assert.Equal(expected, GpuAction.IsMainExecutable(path));
    }

    // ---------- GpuAction 偏好二进制布局 ----------

    [Fact]
    public void BuildPreferenceData_matches_windows_binary_format()
    {
        const string name = "game.exe";
        var data = GpuAction.BuildPreferenceData(name);

        // [DWORD 名称长度(UTF-16 字节数)][名称 UTF-16][DWORD 高性能=2]
        var nameBytes = Encoding.Unicode.GetBytes(name);
        var expectedLength = 4 + nameBytes.Length + 4;
        Assert.Equal(expectedLength, data.Length);

        Assert.Equal(nameBytes.Length, BitConverter.ToInt32(data, 0));
        Assert.Equal(name, Encoding.Unicode.GetString(data, 4, nameBytes.Length));
        Assert.Equal(2, BitConverter.ToInt32(data, 4 + nameBytes.Length));
    }

    [Fact]
    public void BuildPreferenceData_handles_non_ascii_names()
    {
        const string name = "游戏主程序.exe";
        var data = GpuAction.BuildPreferenceData(name);

        var nameBytes = Encoding.Unicode.GetBytes(name);
        Assert.Equal(nameBytes.Length, BitConverter.ToInt32(data, 0));
        Assert.Equal(name, Encoding.Unicode.GetString(data, 4, nameBytes.Length));
    }
}
