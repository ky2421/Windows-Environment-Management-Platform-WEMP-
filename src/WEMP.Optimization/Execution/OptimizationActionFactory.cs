namespace WEMP.Optimization.Execution;

/// <summary>按知识库类别解析对应的执行器。</summary>
public sealed class OptimizationActionFactory(IEnumerable<IOptimizationAction> actions)
{
    private readonly Dictionary<string, IOptimizationAction> _actions =
        actions.ToDictionary(action => action.ItemType, StringComparer.OrdinalIgnoreCase);

    /// <summary>类别 → 执行器（game 类别复用注册表执行器）。</summary>
    public IOptimizationAction Get(string category)
    {
        var itemType = category.ToLowerInvariant() switch
        {
            "game" => "registry",
            _ => category,
        };

        return _actions.TryGetValue(itemType, out var action)
            ? action
            : throw new NotSupportedException($"不支持的优化类别：{category}");
    }
}
