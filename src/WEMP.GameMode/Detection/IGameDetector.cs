namespace WEMP.GameMode.Detection;

/// <summary>游戏进程检测抽象（便于测试注入替身）。</summary>
public interface IGameDetector
{
    /// <summary>进程名（可含 .exe 后缀）是否为游戏。</summary>
    bool IsGameProcess(string processName);

    /// <summary>按进程 ID 判断是否为游戏。</summary>
    bool IsGameProcessById(int processId);
}

/// <summary>基于内置游戏库的检测器。</summary>
public sealed class GameLibraryDetector : IGameDetector
{
    public bool IsGameProcess(string processName) => GameLibrary.IsGameProcess(processName);

    public bool IsGameProcessById(int processId) => GameLibrary.IsGameProcessById(processId);
}
