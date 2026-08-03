namespace WEMP.GameMode.Detection;

/// <summary>
/// 游戏进程库：内置常见游戏进程名（不含 .exe 后缀），大小写不敏感。
/// 用于游戏会话检测时识别前台进程是否为游戏。
/// </summary>
public static class GameLibrary
{
    private static readonly HashSet<string> GameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Valve / Steam
        "csgo", "cs2", "dota2", "tf2", "hl2", "gmod", "rust", "ark",
        "palworld", "left4dead2", "l4d2", "payday2", "warframe",
        // Riot
        "leagueclient", "valorant", "valorant-win64-shipping",
        // Epic / Unreal
        "fortnite", "fortniteclient-win64-shipping", "rocketleague", "gta5", "rdr2",
        // Blizzard
        "overwatch", "overwatch2", "wow", "diabloiv", "diablo4",
        // Ubisoft
        "rainbowsix", "rainbow6", "siege", "thedivision2",
        // EA
        "r5apex", "apex", "thesims4", "bf2042",
        // Microsoft / Xbox
        "minecraft", "minecraftwindows", "forzahorizon5", "forza", "halo", "haloinfinite",
        // 国产
        "genshinimpact", "yuanshen", "starrail", "wutheringwaves", "crossfire", "dnf", "wegame",
        // 其他热门
        "eldenring", "cyberpunk2077", "witcher3", "skyrim", "fallout4",
        "monsterhunterworld", "stardewvalley", "terraria", "factorio", "rimworld",
        "civilizationvi", "valheim", "satisfactory", "subnautica", "destiny2",
        "pathofexile", "lostark", "throneandliberty",
    };

    /// <summary>内置游戏进程名集合（不含 .exe 后缀），供检测器构建合并缓存。</summary>
    public static IReadOnlyCollection<string> AllProcessNames => GameProcesses;

    /// <summary>进程名（可含 .exe 后缀）是否为已知游戏。</summary>
    public static bool IsGameProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return GameProcesses.Contains(name);
    }

    /// <summary>按进程 ID 判断是否为已知游戏。</summary>
    public static bool IsGameProcessById(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return IsGameProcess(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // 进程已退出
            return false;
        }
    }
}
