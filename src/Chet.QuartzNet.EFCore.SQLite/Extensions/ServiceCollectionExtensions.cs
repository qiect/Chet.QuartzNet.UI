using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.EFCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chet.QuartzNet.EFCore.SQLite.Extensions;

/// <summary>
/// SQLite 服务扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加EFCore数据库存储支持（SQLite）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzUISQLite(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<QuartzDbContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly("Chet.QuartzNet.EFCore.SQLite");
            });
        });

        services.Replace(ServiceDescriptor.Scoped<IJobStorage, EFCoreJobStorage>());
        return services;
    }

    /// <summary>
    /// 添加EFCore数据库存储支持（SQLite）- 使用配置
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzUISQLite(this IServiceCollection services, IConfiguration configuration)
    {
        var quartzUIOptions = configuration.GetSection("QuartzUI").Get<QuartzUIOptions>();
        
        if (quartzUIOptions?.StorageType == StorageType.Database)
        {
            var connectionString = configuration.GetConnectionString("QuartzUI");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("未找到QuartzUI数据库连接字符串配置");
            }

            return services.AddQuartzUISQLite(connectionString);
        }

        return services;
    }
}