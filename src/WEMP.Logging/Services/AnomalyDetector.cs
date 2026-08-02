using System.Text.Json;
using System.Text.Json.Serialization;
using WEMP.Infrastructure.Data.Entities;

namespace WEMP.Logging.Services;

/// <summary>
/// 异常检测规则引擎（内置四条规则）：
/// 崩溃事件（1000/1001）、意外关机（41/6008）、单源错误风暴（≥5 条 Error）、审计失败率过高（&gt;30% 且 ≥3 条）。
/// </summary>
public sealed class AnomalyDetector : IAnomalyDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyList<LogAnomaly> Detect(IReadOnlyList<SystemEvent> events, IReadOnlyList<AuditLog> audits)
    {
        var anomalies = new List<LogAnomaly>();
        anomalies.AddRange(DetectCrashes(events));
        anomalies.AddRange(DetectUnexpectedShutdowns(events));
        anomalies.AddRange(DetectErrorStorms(events));
        anomalies.AddRange(DetectAuditFailures(audits));
        return anomalies;
    }

    private static IEnumerable<LogAnomaly> DetectCrashes(IReadOnlyList<SystemEvent> events)
    {
        foreach (var evt in events.Where(e => e.EventId is 1000 or 1001))
        {
            yield return new LogAnomaly
            {
                DetectedAt = DateTime.Now,
                RuleCode = "EVENT_CRASH",
                Severity = "high",
                Title = $"应用崩溃：{evt.Provider ?? "未知来源"}",
                Description = $"事件 {evt.EventId}（应用错误/Windows 错误报告）于 {evt.EventTime:yyyy-MM-dd HH:mm:ss} 触发",
                EvidenceJson = Serialize(new { evt.EventTime, evt.Provider, evt.EventId, evt.Message }),
            };
        }
    }

    private static IEnumerable<LogAnomaly> DetectUnexpectedShutdowns(IReadOnlyList<SystemEvent> events)
    {
        foreach (var evt in events.Where(e => e.EventId is 41 or 6008))
        {
            yield return new LogAnomaly
            {
                DetectedAt = DateTime.Now,
                RuleCode = "EVENT_UNEXPECTED_SHUTDOWN",
                Severity = "high",
                Title = $"异常关机/重启：{evt.Provider ?? "未知来源"}",
                Description = $"事件 {evt.EventId}（意外关机或系统异常重启）于 {evt.EventTime:yyyy-MM-dd HH:mm:ss} 触发",
                EvidenceJson = Serialize(new { evt.EventTime, evt.Provider, evt.EventId, evt.Message }),
            };
        }
    }

    private static IEnumerable<LogAnomaly> DetectErrorStorms(IReadOnlyList<SystemEvent> events)
    {
        // Level：1=严重 2=错误 3=警告 4=信息；按 Provider 统计错误级事件
        foreach (var group in events
                     .Where(e => e.Level <= 2 && !string.IsNullOrWhiteSpace(e.Provider))
                     .GroupBy(e => e.Provider, StringComparer.OrdinalIgnoreCase))
        {
            var list = group.ToList();
            if (list.Count < 5)
            {
                continue;
            }

            yield return new LogAnomaly
            {
                DetectedAt = DateTime.Now,
                RuleCode = "EVENT_ERROR_STORM",
                Severity = "medium",
                Title = $"错误风暴：{group.Key}",
                Description = $"来源 {group.Key} 在检测窗口内产生 {list.Count} 条错误级事件",
                EvidenceJson = Serialize(new
                {
                    Provider = group.Key,
                    Count = list.Count,
                    Window = new { From = list.Min(e => e.EventTime), To = list.Max(e => e.EventTime) },
                }),
            };
        }
    }

    private static IEnumerable<LogAnomaly> DetectAuditFailures(IReadOnlyList<AuditLog> audits)
    {
        if (audits.Count == 0)
        {
            yield break;
        }

        var failed = audits.Count(a => a.Result == "failed");
        if (failed < 3 || (double)failed / audits.Count <= 0.3)
        {
            yield break;
        }

        var failedByModule = audits
            .Where(a => a.Result == "failed")
            .GroupBy(a => a.Module)
            .Select(g => new { Module = g.Key, Count = g.Count() })
            .OrderByDescending(m => m.Count)
            .ToList();

        yield return new LogAnomaly
        {
            DetectedAt = DateTime.Now,
            RuleCode = "AUDIT_FAILURE_RATE",
            Severity = "medium",
            Title = $"审计失败率过高：{failed}/{audits.Count}",
            Description = $"窗口内审计日志失败占比 {(double)failed / audits.Count:P0}，可能表明操作或组件持续故障",
            EvidenceJson = Serialize(new { Failed = failed, Total = audits.Count, ByModule = failedByModule }),
        };
    }

    private static string? Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);
}
