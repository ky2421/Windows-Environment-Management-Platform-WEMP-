using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;
using WEMP.Optimization.Seeding;
using WEMP.Optimization.UI;

namespace WEMP.Optimization;

/// <summary>系统优化模块：知识库驱动的一键优化 / 自定义优化，支持备份、回滚与日志。</summary>
public sealed class OptimizationModule : IModule
{
    public string Name => "WEMP.Optimization";

    public string DisplayName => "系统优化";

    public Version Version => new(0, 2, 0);

    public IReadOnlyList<PageRegistration> Pages { get; } =
    [
        new PageRegistration(
            "optimization",
            "系统优化",
            typeof(OptimizationPageViewModel),
            typeof(OptimizationPage),
            Order: 1),
    ];

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // 模块激活即同步优化知识库，确保 CLI / Service / UI 均可使用
        using var scope = services.CreateScope();
        var seed = scope.ServiceProvider.GetRequiredService<OptimizationSeedService>();
        await seed.EnsureSeedAsync(cancellationToken);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
