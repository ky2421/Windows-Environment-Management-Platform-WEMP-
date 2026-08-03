using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WEMP.Infrastructure.Data;

namespace WEMP.Infrastructure;

/// <summary>WEMP.Infrastructure 的依赖注入注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册数据库上下文。默认使用 <see cref="WempDatabase.DefaultDatabasePath"/>，
    /// 可通过 <paramref name="connectionString"/> 覆盖。
    /// </summary>
    public static IServiceCollection AddWempInfrastructure(
        this IServiceCollection services,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var connection = connectionString ?? WempDatabase.CreateConnectionString();
        services.AddDbContext<WempDbContext>(options => options.UseSqlite(connection));
        // 运行时多线程场景（页面、后台监测、定时任务）使用短生命周期上下文工厂，
        // 避免 Singleton 服务共享同一 DbContext 导致的并发冲突
        services.AddPooledDbContextFactory<WempDbContext>(options => options.UseSqlite(connection));

        return services;
    }
}
