using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移步骤信息
/// </summary>
public class MigrationStepInfo
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 步骤键（jobs/logs/settings/notifications）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 步骤状态
    /// </summary>
    public MigrationStepStatus Status { get; set; } = MigrationStepStatus.Pending;

    /// <summary>
    /// 总记录数
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// 已迁移数
    /// </summary>
    public long MigratedCount { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public long SkippedCount { get; set; }

    /// <summary>
    /// 更新数
    /// </summary>
    public long UpdatedCount { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public long FailedCount { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 步骤开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 步骤结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
}
