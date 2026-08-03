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

    /// <summary>注册表类多值写入（同一键下多个值），与单值二选一。</summary>
    public List<RegistryValueSpec>? Values { get; init; }

    /// <summary>注册表类多键写入（多个键，每个含多值），与单键/单值二选一。</summary>
    public List<RegistryKeySpec>? Keys { get; init; }

    /// <summary>布尔开关目标（hags 等开/关二选一优化项）。</summary>
    public bool? Enable { get; init; }

    /// <summary>设备管理器类目标：按友好名关键词匹配系统设备。</summary>
    public List<string>? DeviceKeywords { get; init; }

    // service
    public string? ServiceName { get; init; }

    public List<string>? Services { get; init; }

    /// <summary>服务目标启动类型：disabled（默认）/ manual（手动）/ auto；manual 用于 Edge 更新等不宜禁用的服务。</summary>
    public string? StartMode { get; init; }

    /// <summary>计划任务完整路径列表（schtasks /TN 格式，含反斜杠前缀）。</summary>
    public List<string>? Tasks { get; init; }

    /// <summary>Windows 可选功能名列表（DISM /featurename 格式）。</summary>
    public List<string>? FeatureNames { get; init; }

    // network
    public List<string>? Dns { get; init; }

    // disk / power
    public string? Command { get; init; }

    public string? Scheme { get; init; }

    // startup
    public string? StartupKeywords { get; init; }

    // startup 清理模式（disable-all = 黑名单，保留 retainKeywords，其余全禁）
    public string? Mode { get; init; }

    public List<string>? RetainKeywords { get; init; }

    /// <summary>要操作的服务名列表（单值转数组）。</summary>
    public List<string> GetServices() =>
        Services is { Count: > 0 } ? Services : [ServiceName ?? ""];

    /// <summary>计划任务路径列表；未配置返回空数组。</summary>
    public List<string> GetTasks() =>
        Tasks is { Count: > 0 } ? Tasks : [];

    /// <summary>可选功能名列表；未配置返回空数组。</summary>
    public List<string> GetFeatureNames() =>
        FeatureNames is { Count: > 0 } ? FeatureNames : [];

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

/// <summary>注册表多值条目（同一键下的一个值）。</summary>
public sealed class RegistryValueSpec
{
    public string? Name { get; init; }

    public JsonElement? Data { get; init; }
}

/// <summary>注册表多键条目（一个键及其多个值）。</summary>
public sealed class RegistryKeySpec
{
    public string? Key { get; init; }

    public List<RegistryValueSpec>? Values { get; init; }
}
