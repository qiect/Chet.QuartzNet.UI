using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Chet.QuartzNet.EFCore.MigrationSupport;

/// <summary>
/// 数据库迁移 Provider 接口，由各数据库 Provider 包实现，用于在迁移时动态构建 DbContext
/// </summary>
public interface IQuartzMigrationProvider
{
    /// <summary>
    /// 对应的存储类型
    /// </summary>
    MigrationStoreType StoreType { get; }

    /// <summary>
    /// 对应的 NuGet 包名
    /// </summary>
    string PackageName { get; }

    /// <summary>
    /// 创建 QuartzDbContext 选项
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="serverVersion">数据库服务器版本（可选）</param>
    DbContextOptions<QuartzDbContext> CreateOptions(
        string connectionString,
        string? serverVersion
    );
}
