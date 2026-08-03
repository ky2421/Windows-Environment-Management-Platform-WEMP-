namespace WEMP.GameMode.Detection;

/// <summary>游戏进程检测抽象（便于测试注入替身）。</summary>
public interface IGameDetector
{
    /// <summary>进程名（可含 .exe 后缀）是否为游戏。</summary>
    bool IsGameProcess(string processName);

    /// <summary>按进程 ID 判断是否为游戏。</summary>
    bool IsGameProcessById(int processId);
}

/// <summary>
/// 基于内置游戏库 + 用户自定义库的检测器。
/// 维护内置/自定义合并的进程名缓存，订阅 <see cref="ICustomGameLibrary.LibraryChanged"/>
/// 在库内容变化时重建，避免每次前台检测查询数据库。
/// </summary>
public sealed class GameLibraryDetector : IGameDetector
{
    private readonly ICustomGameLibrary _customLibrary;
    private readonly object _sync = new();
    private HashSet<string> _merged;

    public GameLibraryDetector(ICustomGameLibrary customLibrary)
    {
        _customLibrary = customLibrary;
        _merged = BuildMerged();
        _customLibrary.LibraryChanged += (_, _) => Reload();
    }

    public bool IsGameProcess(string processName)
    {
        var name = Normalize(processName);
        if (name is null)
        {
            return false;
        }

        lock (_sync)
        {
            return _merged.Contains(name);
        }
    }

    public bool IsGameProcessById(int processId)
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

    private HashSet<string> BuildMerged()
    {
        var merged = new HashSet<string>(GameLibrary.AllProcessNames, StringComparer.OrdinalIgnoreCase);
        foreach (var game in _customLibrary.GetAll())
        {
            merged.Add(game.ProcessName);
        }

        return merged;
    }

    private void Reload()
    {
        lock (_sync)
        {
            _merged = BuildMerged();
        }
    }

    private static string? Normalize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name.Length == 0 ? null : name;
    }
}
