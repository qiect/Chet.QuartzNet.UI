using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 数据迁移步骤状态
/// </summary>
public enum MigrationStepStatus
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 进行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 跳过（无数据或未选择）
    /// </summary>
    Skipped = 4,
}
