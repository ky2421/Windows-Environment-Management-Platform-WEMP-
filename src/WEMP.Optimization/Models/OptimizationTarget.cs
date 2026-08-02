using System.Text.Json;

namespace WEMP.Optimization.Models;

/// <summary>
/// 优化目标参数（由知识库条目的 targetJson 反序列化）。
/// 不同类别使用不同字段，均为可选。
/// </summary>
public sealed class OptimizationTarget
{
    // registry / game（注册表类）
    public string? Key { get; init; }

    public string? ValueName { get; init; }

    public JsonElement? ValueData { get; init; }

    // service
    public string? ServiceName { get; init; }

    public List<string>? Services { get; init; }

    // network
    public List<string>? Dns { get; init; }

    // disk / power
    public string? Command { get; init; }

    public string? Scheme { get; init; }

    // startup
    public string? StartupKeywords { get; init; }

    /// <summary>要操作的服务名列表（单值转数组）。</summary>
    public List<string> GetServices() =>
        Services is { Count: > 0 } ? Services : [ServiceName ?? ""];

    /// <summary>解析目标参数；targetJson 为空返回 null。</summary>
    public static OptimizationTarget? Parse(string? targetJson)
    {
        if (string.IsNullOrWhiteSpace(targetJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<OptimizationTarget>(targetJson, Options);
    }

    // 知识库 JSON 使用 camelCase（key/valueName/valueData），Web 默认大小写不敏感
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
