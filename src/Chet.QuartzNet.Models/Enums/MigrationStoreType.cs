using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移存储类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationStoreType
{
    /// <summary>
    /// 文件存储
    /// </summary>
    File = 0,

    /// <summary>
    /// MySQL
    /// </summary>
    MySql = 1,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSql = 2,

    /// <summary>
    /// SQLite
    /// </summary>
    SQLite = 3,

    /// <summary>
    /// SQL Server
    /// </summary>
    SqlServer = 4,
}
