using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// 日志异常检测抽象：对系统事件与审计日志窗口数据执行规则匹配。
/// </summary>
public interface IAnomalyDetector
{
    /// <summary>检测窗口数据中的异常，返回待入库的异常项（不含历史去重）。</summary>
    IReadOnlyList<LogAnomaly> Detect(IReadOnlyList<SystemEvent> events, IReadOnlyList<AuditLog> audits);
}
