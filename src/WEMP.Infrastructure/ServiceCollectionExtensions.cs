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

        return services;
    }
}
