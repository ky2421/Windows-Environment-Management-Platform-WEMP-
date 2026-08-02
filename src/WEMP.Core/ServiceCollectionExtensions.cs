using Microsoft.Extensions.DependencyInjection;
using WEMP.Core.Abstractions;
using WEMP.Core.Services;

namespace WEMP.Core;

/// <summary>WEMP.Core 的依赖注入注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册 Core 层基础服务：消息总线与模块宿主。</summary>
    public static IServiceCollection AddWempCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<IModuleHost, ModuleHost>();
        return services;
    }
}
