using System.Collections.Concurrent;
using System.Reflection;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Migration;
using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.EFCore.MigrationSupport;

/// <summary>
/// 数据库 Provider 安装状态
/// </summary>
public sealed record MigrationProviderAvailability(
    MigrationStoreType Type,
    string PackageName,
    bool Installed,
    string? Error
);

/// <summary>
/// 迁移存储工厂
/// 根据存储描述创建文件或数据库适配器，数据库 Provider 通过反射按需加载，避免引入强依赖
/// </summary>
public sealed class MigrationStoreFactory
{
    private static readonly Dictionary<MigrationStoreType, ProviderInfo> KnownProviders = new()
    {
        [MigrationStoreType.MySql] = new ProviderInfo(
            "Chet.QuartzNet.EFCore.MySQL",
            "Chet.QuartzNet.EFCore.MySQL.MigrationSupport.MySqlMigrationProvider",
            "Chet.QuartzNet.EFCore.MySQL"
        ),
        [MigrationStoreType.PostgreSql] = new ProviderInfo(
            "Chet.QuartzNet.EFCore.PostgreSQL",
            "Chet.QuartzNet.EFCore.PostgreSQL.MigrationSupport.PostgreSqlMigrationProvider",
            "Chet.QuartzNet.EFCore.PostgreSQL"
        ),
        [MigrationStoreType.SQLite] = new ProviderInfo(
            "Chet.QuartzNet.EFCore.SQLite",
            "Chet.QuartzNet.EFCore.SQLite.MigrationSupport.SqliteMigrationProvider",
            "Chet.QuartzNet.EFCore.SQLite"
        ),
        [MigrationStoreType.SqlServer] = new ProviderInfo(
            "Chet.QuartzNet.EFCore.SqlServer",
            "Chet.QuartzNet.EFCore.SqlServer.MigrationSupport.SqlServerMigrationProvider",
            "Chet.QuartzNet.EFCore.SqlServer"
        ),
    };

    private static readonly ConcurrentDictionary<MigrationStoreType, ProviderResolution> Cache = new();

    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MigrationStoreFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 获取各数据库 Provider 的安装状态
    /// </summary>
    public IReadOnlyList<MigrationProviderAvailability> GetProviderAvailability()
    {
        var list = new List<MigrationProviderAvailability>();
        foreach (var pair in KnownProviders)
        {
            var resolution = Cache.GetOrAdd(pair.Key, Resolve);
            list.Add(
                new MigrationProviderAvailability(
                    pair.Key,
                    pair.Value.PackageName,
                    resolution.Provider != null,
                    resolution.Error
                )
            );
        }
        return list;
    }

    /// <summary>
    /// 创建迁移存储适配器
    /// </summary>
    /// <param name="descriptor">存储描述</param>
    /// <param name="isTarget">是否作为迁移目标</param>
    /// <param name="mode">写入模式</param>
    /// <param name="allowUpgradeSourceSchema">是否允许升级源数据库结构</param>
    public IMigrationStore CreateStore(
        MigrationStoreDescriptor descriptor,
        bool isTarget,
        MigrationWriteMode mode,
        bool allowUpgradeSourceSchema
    )
    {
        if (descriptor.Type == MigrationStoreType.File)
        {
            return new FileMigrationStore(
                descriptor,
                mode,
                _loggerFactory.CreateLogger<FileMigrationStore>()
            );
        }

        if (string.IsNullOrWhiteSpace(descriptor.ConnectionString))
        {
            throw new InvalidOperationException($"{descriptor.Type} 存储缺少连接字符串");
        }

        var resolution = Cache.GetOrAdd(descriptor.Type, Resolve);
        if (resolution.Provider == null)
        {
            throw new InvalidOperationException(
                resolution.Error
                    ?? $"未安装 {KnownProviders[descriptor.Type].PackageName} 包，无法使用 {descriptor.Type} 存储进行迁移"
            );
        }

        var options = resolution.Provider.CreateOptions(
            descriptor.ConnectionString,
            descriptor.ServerVersion
        );
        var context = new QuartzDbContext(options);

        return new EfCoreMigrationStore(
            context,
            descriptor,
            isTarget,
            mode,
            allowUpgradeSourceSchema,
            _loggerFactory.CreateLogger<EfCoreMigrationStore>()
        );
    }

    private static ProviderResolution Resolve(MigrationStoreType type)
    {
        var info = KnownProviders[type];
        try
        {
            // 先尝试按已知程序集名称加载
            var assembly = Assembly.Load(info.AssemblyName);
            var provider = CreateFromAssembly(assembly, type);
            if (provider != null)
            {
                return new ProviderResolution(provider, null);
            }
        }
        catch (Exception ex)
        {
            // 程序集未安装时继续扫描已加载程序集
            _ = ex;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var provider = CreateFromAssembly(assembly, type);
            if (provider != null)
            {
                return new ProviderResolution(provider, null);
            }
        }

        return new ProviderResolution(
            null,
            $"未安装 {info.PackageName} 包，无法使用 {type} 存储进行迁移"
        );
    }

    private static IQuartzMigrationProvider? CreateFromAssembly(
        Assembly assembly,
        MigrationStoreType type
    )
    {
        try
        {
            foreach (var assemblyType in assembly.GetTypes())
            {
                if (
                    !typeof(IQuartzMigrationProvider).IsAssignableFrom(assemblyType)
                    || assemblyType.IsInterface
                    || assemblyType.IsAbstract
                )
                {
                    continue;
                }

                if (
                    Activator.CreateInstance(assemblyType) is IQuartzMigrationProvider provider
                    && provider.StoreType == type
                )
                {
                    return provider;
                }
            }
        }
        catch (Exception)
        {
            // 忽略无法反射的程序集
        }

        return null;
    }

    private sealed record ProviderInfo(
        string AssemblyName,
        string TypeName,
        string PackageName
    );

    private sealed record ProviderResolution(
        IQuartzMigrationProvider? Provider,
        string? Error
    );
}
