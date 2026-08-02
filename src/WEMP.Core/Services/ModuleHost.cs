using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;

namespace WEMP.Core.Services;

/// <summary>
/// 模块宿主默认实现：从程序集扫描 <see cref="IModule"/> 实现，
/// 通过 DI 容器构造（支持构造函数注入），并按加载顺序管理生命周期。
/// </summary>
public sealed class ModuleHost : IModuleHost
{
    private readonly IServiceProvider _services;
    private readonly List<IModule> _modules = [];

    public ModuleHost(IServiceProvider services)
    {
        _services = services;
    }

    public IReadOnlyList<IModule> Modules => _modules;

    public void LoadFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(IsModuleType))
            {
                var module = (IModule)ActivatorUtilities.CreateInstance(_services, type);
                _modules.Add(module);
            }
        }

        _modules.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    public async Task InitializeAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in _modules)
        {
            await module.InitializeAsync(_services, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ActivateAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var module in _modules)
        {
            await module.ActivateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ShutdownAllAsync(CancellationToken cancellationToken = default)
    {
        // 逆序关闭，后加载的先释放
        foreach (var module in _modules.AsEnumerable().Reverse())
        {
            await module.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsModuleType(Type type)
    {
        return !type.IsAbstract
            && !type.IsInterface
            && typeof(IModule).IsAssignableFrom(type);
    }
}
