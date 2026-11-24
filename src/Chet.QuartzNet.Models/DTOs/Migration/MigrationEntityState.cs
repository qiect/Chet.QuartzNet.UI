using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 单个实体的迁移状态
/// </summary>
public class MigrationEntityState
{
    /// <summary>
    /// 实体键（jobs/logs/settings/notifications）
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// 实体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 实体状态
    /// </summary>
    public MigrationStepStatus Status { get; set; } = MigrationStepStatus.Pending;

    /// <summary>
    /// 源端记录数
    /// </summary>
    public long SourceCount { get; set; }

    /// <summary>
    /// 已处理数
    /// </summary>
    public long Processed { get; set; }

    /// <summary>
    /// 插入数
    /// </summary>
    public long Inserted { get; set; }

    /// <summary>
    /// 更新数
    /// </summary>
    public long Updated { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public long Skipped { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
