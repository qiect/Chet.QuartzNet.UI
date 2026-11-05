using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chet.QuartzNet.EFCore.SQLite.Interceptors;

/// <summary>
/// SQLite 连接拦截器
/// 通过 WAL 日志模式将读写分离：作业执行写入日志时，页面查询仍可读取快照数据，不会被写锁阻塞
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string PragmaSql =
        "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragma(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        await ApplyPragmaAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyPragma(DbConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = PragmaSql;
            command.ExecuteNonQuery();
        }
        catch
        {
            // 内存数据库或只读连接不支持 WAL，忽略即可
        }
    }

    private static async Task ApplyPragmaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = PragmaSql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 内存数据库或只读连接不支持 WAL，忽略即可
        }
    }
}
