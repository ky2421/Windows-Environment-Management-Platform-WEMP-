using System.Diagnostics.Eventing.Reader;
using Serilog;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// 基于 EventLogReader 的 Windows 事件日志读取实现。
/// 读取 Application 与 System 通道；权限不足时返回空集合并记录警告。
/// </summary>
public sealed class WindowsEventLogSource : IEventSource
{
    public Task<IReadOnlyList<SystemEvent>> ReadRecentAsync(string channel, TimeSpan window, CancellationToken cancellationToken = default)
    {
        // timediff(@SystemTime) 返回距事件时间的毫秒数，避免绝对时间格式兼容问题
        var ms = (long)window.TotalMilliseconds;
        return Task.Run<IReadOnlyList<SystemEvent>>(() =>
        {
            try
            {
                var query = new EventLogQuery(
                    channel,
                    PathType.LogName,
                    $"*[System[TimeCreated[timediff(@SystemTime) <= {ms}]]]");

                var results = new List<SystemEvent>();
                using var reader = new EventLogReader(query);
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    using var record = reader.ReadEvent();
                    if (record is null)
                    {
                        break;
                    }

                    results.Add(new SystemEvent
                    {
                        EventTime = record.TimeCreated ?? DateTime.Now.Subtract(window),
                        Provider = record.ProviderName,
                        EventId = record.Id > 0 ? (int?)record.Id : null,
                        Level = record.Level.HasValue ? (int)record.Level.Value : 4,
                        Computer = record.MachineName,
                        Message = record.FormatDescription()?.Trim(),
                    });
                }

                Log.Information("事件日志读取完成：{Channel} 通道 {Count} 条", channel, results.Count);
                return results;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning(ex, "读取事件日志 {Channel} 无权限", channel);
                return [];
            }
            catch (EventLogNotFoundException ex)
            {
                Log.Warning(ex, "事件日志通道 {Channel} 不存在", channel);
                return [];
            }
            catch (Exception ex)
            {
                Log.Error(ex, "读取事件日志 {Channel} 失败", channel);
                return [];
            }
        }, cancellationToken);
    }
}
