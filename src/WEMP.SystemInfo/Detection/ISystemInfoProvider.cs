using WEMP.SystemInfo.Models;

namespace WEMP.SystemInfo.Detection;

/// <summary>系统检测服务：采集操作系统、硬件与开发环境信息。</summary>
public interface ISystemInfoProvider
{
    /// <summary>执行一次完整检测。</summary>
    Task<SystemInfoSnapshot> DetectAsync(CancellationToken cancellationToken = default);
}
