using System.Runtime.InteropServices;

namespace WEMP.SystemInfo.Services;

/// <summary>系统还原点创建（SRSetRestorePoint）。需要管理员权限且系统保护已开启。</summary>
public static class RestorePointHelper
{
    private const int BEGIN_SYSTEM_CHANGE = 100;
    private const int END_SYSTEM_CHANGE = 101;
    private const int MODIFY_SETTINGS = 12;

    /// <summary>创建一个「设置变更」类型的系统还原点。</summary>
    public static bool Create(string description)
    {
        var info = new RestorePointInfo
        {
            EventType = BEGIN_SYSTEM_CHANGE,
            RestorePointType = MODIFY_SETTINGS,
            Description = description,
        };
        if (!SRSetRestorePointW(ref info, out var status))
        {
            return false;
        }

        // 0 表示已启动变更记录；后续结束标记失败不影响还原点创建
        return status.Status == 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestorePointInfo
    {
        public int EventType;
        public int RestorePointType;
        public long SequenceNumber;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StatMgrStatus
    {
        public int Status;
        public long SequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode)]
    private static extern bool SRSetRestorePointW(ref RestorePointInfo restorePointSpec, out StatMgrStatus status);
}
