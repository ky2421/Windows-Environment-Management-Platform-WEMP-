using System.Runtime.InteropServices;
using System.Security.Principal;
using Serilog;

namespace WEMP.Optimization.Services;

/// <summary>系统还原点服务：优化前调用 Windows System Restore API 创建还原点。</summary>
public interface ISystemRestorePointService
{
    /// <summary>
    /// 创建系统还原点，返回还原点序列号。
    /// 非管理员权限或系统还原未启用时返回 null（不抛出，优化流程继续）。
    /// </summary>
    Task<long?> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 SRSetRestorePoint 的实现：BEGIN_SYSTEM_CHANGE 创建 + END_SYSTEM_CHANGE 结束，
/// 还原点序列号返回给调用方持久化（OptimizationRecord.RestorePointId）。
/// </summary>
public sealed class SystemRestorePointService : ISystemRestorePointService
{
    private const int BeginSystemChange = 100;
    private const int EndSystemChange = 101;
    private const int ApplicationInstall = 0;

    public Task<long?> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateRestorePointCore(description));
    }

    private static long? CreateRestorePointCore(string description)
    {
        if (!IsAdministrator())
        {
            Log.Warning("非管理员权限，跳过系统还原点创建");
            return null;
        }

        var info = new RestorePointInfo
        {
            EventType = BeginSystemChange,
            RestorePtType = ApplicationInstall,
            SequenceNumber = 0,
            Description = Truncate(description),
        };

        var result = NativeMethods.SRSetRestorePoint(ref info, out var status);
        if (result != NativeMethods.ErrorSuccess)
        {
            Log.Warning("系统还原点创建失败：返回值 {Result}（状态 {Status}，系统还原可能未启用）", result, status.Status);
            return null;
        }

        var sequence = status.SequenceNumber;

        // 结束系统变更：告知系统快照固化完成
        var endInfo = new RestorePointInfo
        {
            EventType = EndSystemChange,
            RestorePtType = ApplicationInstall,
            SequenceNumber = sequence,
            Description = info.Description,
        };
        NativeMethods.SRSetRestorePoint(ref endInfo, out _);

        Log.Information("系统还原点已创建：序列号 {Sequence}（{Description}）", sequence, description);
        return sequence;
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "管理员权限检测失败，按非管理员处理");
            return false;
        }
    }

    private static string Truncate(string value)
    {
        // szDescription 固定 256 字符（含结尾 null）
        return value.Length <= 255 ? value : value[..255];
    }

    /// <summary>RESTOREPOINTINFO（dwEventType/dwRestorePtType/llSequenceNumber/szDescription）。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestorePointInfo
    {
        public int EventType;
        public int RestorePtType;
        public long SequenceNumber;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;
    }

    /// <summary>STATEMGRSTATUS（nStatus/llSequenceNumber）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct StateMgrStatus
    {
        public uint Status;
        public long SequenceNumber;
    }

    private static class NativeMethods
    {
        public const int ErrorSuccess = 0;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SRSetRestorePoint(ref RestorePointInfo pRestorePtSpec, out StateMgrStatus pSmgStatus);
    }
}
