using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chet.QuartzNet.EFCore.SQLite.MigrationSupport;

/// <summary>
/// SQLite 迁移 Provider
/// </summary>
public sealed class SqliteMigrationProvider : IQuartzMigrationProvider
{
    /// <inheritdoc />
    public MigrationStoreType StoreType => MigrationStoreType.SQLite;

    /// <inheritdoc />
    public string PackageName => "Chet.QuartzNet.EFCore.SQLite";

    /// <inheritdoc />
    public DbContextOptions<QuartzDbContext> CreateOptions(
        string connectionString,
        string? serverVersion
    )
    {
        EnsureDatabaseDirectory(connectionString);

        var builder = new DbContextOptionsBuilder<QuartzDbContext>();
        builder.UseSqlite(
            connectionString,
            options => options.MigrationsAssembly("Chet.QuartzNet.EFCore.SQLite")
        );

        return builder.Options;
    }

    private static void EnsureDatabaseDirectory(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
