using System.Reflection;

namespace WEMP.Core.Abstractions;

/// <summary>
/// 模块宿主：负责模块的发现、加载与生命周期管理。
/// WPF 应用、Windows 服务、CLI 三个宿主共用同一套模块管理逻辑。
/// </summary>
public interface IModuleHost
{
    /// <summary>当前已加载的模块。</summary>
    IReadOnlyList<IModule> Modules { get; }

    /// <summary>从程序集扫描并加载所有 <see cref="IModule"/> 实现。</summary>
    void LoadFromAssemblies(params Assembly[] assemblies);

    /// <summary>按依赖顺序初始化所有模块。</summary>
    Task InitializeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>激活所有模块。</summary>
    Task ActivateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>关闭所有模块（逆序）。</summary>
    Task ShutdownAllAsync(CancellationToken cancellationToken = default);
}
