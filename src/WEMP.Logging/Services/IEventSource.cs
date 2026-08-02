using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// Windows 事件日志读取抽象（测试中以内存实现替换）。
/// </summary>
public interface IEventSource
{
    /// <summary>读取指定通道（Application/System）最近窗口内的事件。</summary>
    Task<IReadOnlyList<SystemEvent>> ReadRecentAsync(string channel, TimeSpan window, CancellationToken cancellationToken = default);
}
