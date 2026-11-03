using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Chet.QuartzNet.EFCore.MySQL.MigrationSupport;

/// <summary>
/// MySQL 迁移 Provider
/// </summary>
public sealed class MySqlMigrationProvider : IQuartzMigrationProvider
{
    /// <inheritdoc />
    public MigrationStoreType StoreType => MigrationStoreType.MySql;

    /// <inheritdoc />
    public string PackageName => "Chet.QuartzNet.EFCore.MySQL";

    /// <inheritdoc />
    public DbContextOptions<QuartzDbContext> CreateOptions(
        string connectionString,
        string? serverVersion
    )
    {
        var builder = new DbContextOptionsBuilder<QuartzDbContext>();
        var version = string.IsNullOrWhiteSpace(serverVersion)
            ? MySqlServerVersion.AutoDetect(connectionString)
            : MySqlServerVersion.Parse(serverVersion);

        builder.UseMySql(
            connectionString,
            version,
            options => options.MigrationsAssembly("Chet.QuartzNet.EFCore.MySQL")
        );

        return builder.Options;
    }
}
