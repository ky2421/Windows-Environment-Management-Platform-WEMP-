namespace WEMP.Core.Abstractions;

/// <summary>
/// WEMP 模块契约。所有功能模块实现此接口，由宿主（Shell / Service / CLI）发现并管理生命周期。
/// </summary>
public interface IModule
{
    /// <summary>模块唯一标识，如 <c>WEMP.Optimization</c>。</summary>
    string Name { get; }

    /// <summary>用户可见名称，如「系统优化」。</summary>
    string DisplayName { get; }

    /// <summary>模块版本。</summary>
    Version Version { get; }

    /// <summary>模块初始化：注册自身服务、订阅消息。宿主启动时对所有模块调用一次。</summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);

    /// <summary>模块激活：用户首次进入模块页面时调用（可延迟的准备工作）。</summary>
    Task ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>模块关闭：释放资源、退订消息。应用退出时调用。</summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>模块注册到主导航的页面列表。</summary>
    IReadOnlyList<PageRegistration> Pages { get; }
}
