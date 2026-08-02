using WEMP.Core.Abstractions;
using WEMP.DevEnvironment.UI;

namespace WEMP.DevEnvironment;

/// <summary>
/// 开发环境模块：YAML 模板驱动的工具链一键部署
/// （工具安装 → 环境变量 → 配置文件 → 验证 → 快照），部署流水线持久化并支持回滚。
/// </summary>
public sealed class DevEnvironmentModule : IModule
{
    public string Name => "WEMP.DevEnvironment";

    public string DisplayName => "开发环境";

    public Version Version => new(0, 1, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "devenv",
            "开发环境",
            typeof(DevEnvironmentPageViewModel),
            typeof(DevEnvironmentPage),
            Order: 4),
    ];

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
