using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Chet.QuartzNet.EFCore.SqlServer.MigrationSupport;

/// <summary>
/// SQL Server 迁移 Provider
/// </summary>
public sealed class SqlServerMigrationProvider : IQuartzMigrationProvider
{
    /// <inheritdoc />
    public MigrationStoreType StoreType => MigrationStoreType.SqlServer;

    /// <inheritdoc />
    public string PackageName => "Chet.QuartzNet.EFCore.SqlServer";

    /// <inheritdoc />
    public DbContextOptions<QuartzDbContext> CreateOptions(
        string connectionString,
        string? serverVersion
    )
    {
        var builder = new DbContextOptionsBuilder<QuartzDbContext>();
        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly("Chet.QuartzNet.EFCore.SqlServer")
        );

        return builder.Options;
    }
}
