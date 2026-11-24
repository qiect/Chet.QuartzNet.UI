using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移实体类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationEntity
{
    /// <summary>
    /// 作业
    /// </summary>
    Jobs = 0,

    /// <summary>
    /// 作业日志
    /// </summary>
    Logs = 1,

    /// <summary>
    /// 系统设置
    /// </summary>
    Settings = 2,

    /// <summary>
    /// 通知消息
    /// </summary>
    Notifications = 3,
}
