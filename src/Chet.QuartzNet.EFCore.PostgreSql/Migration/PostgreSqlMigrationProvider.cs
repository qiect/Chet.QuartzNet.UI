using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Chet.QuartzNet.EFCore.PostgreSQL.MigrationSupport;

/// <summary>
/// PostgreSQL 迁移 Provider
/// </summary>
public sealed class PostgreSqlMigrationProvider : IQuartzMigrationProvider
{
    /// <inheritdoc />
    public MigrationStoreType StoreType => MigrationStoreType.PostgreSql;

    /// <inheritdoc />
    public string PackageName => "Chet.QuartzNet.EFCore.PostgreSQL";

    /// <inheritdoc />
    public DbContextOptions<QuartzDbContext> CreateOptions(
        string connectionString,
        string? serverVersion
    )
    {
        var builder = new DbContextOptionsBuilder<QuartzDbContext>();
        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly("Chet.QuartzNet.EFCore.PostgreSQL")
        );

        return builder.Options;
    }
}
