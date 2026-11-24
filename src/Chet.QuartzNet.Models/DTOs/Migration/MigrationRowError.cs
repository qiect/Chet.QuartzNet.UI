using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 单行迁移错误样本
/// </summary>
public class MigrationRowError
{
    /// <summary>
    /// 实体键（jobs/logs/settings/notifications）
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// 记录键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 相关字段
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
