namespace WEMP.Core.Models;

/// <summary>模块生命周期状态。</summary>
public enum ModuleState
{
    /// <summary>已加载，尚未初始化。</summary>
    Loaded,

    /// <summary>初始化完成。</summary>
    Initialized,

    /// <summary>已激活（用户已进入模块）。</summary>
    Active,

    /// <summary>初始化或运行期间失败。</summary>
    Failed,
}
