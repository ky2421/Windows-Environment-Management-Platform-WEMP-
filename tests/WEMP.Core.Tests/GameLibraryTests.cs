using WEMP.GameMode.Detection;

namespace WEMP.Core.Tests;

/// <summary>游戏库匹配测试。</summary>
public class GameLibraryTests
{
    [Theory]
    [InlineData("csgo", true)]
    [InlineData("CSGO", true)]                // 大小写不敏感
    [InlineData("dota2.exe", true)]           // 带 .exe 后缀
    [InlineData("r5apex", true)]
    [InlineData("genshinimpact", true)]
    [InlineData("notepad", false)]
    [InlineData("explorer", false)]
    [InlineData("", false)]
    [InlineData("csgo.old", false)]           // 仅去掉 .exe
    public void IsGameProcess_matches_library(string processName, bool expected)
    {
        Assert.Equal(expected, GameLibrary.IsGameProcess(processName));
    }

    [Fact]
    public void IsGameProcessById_returns_false_for_missing_process()
    {
        Assert.False(GameLibrary.IsGameProcessById(int.MaxValue));
    }
}
