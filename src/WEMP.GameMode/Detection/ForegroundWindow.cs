using System.Runtime.InteropServices;

namespace WEMP.GameMode.Detection;

/// <summary>前台窗口检测：获取当前前台窗口所属进程 ID。</summary>
public static class ForegroundWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>返回前台窗口进程 ID；无前台窗口时返回 null。</summary>
    public static int? GetForegroundProcessId()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        return GetWindowThreadProcessId(handle, out var processId) != 0 ? (int)processId : null;
    }
}
