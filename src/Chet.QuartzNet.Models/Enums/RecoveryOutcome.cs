using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 单条中断记录的处理结果
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecoveryOutcome
{
    /// <summary>
    /// 等待处理
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已触发补偿
    /// </summary>
    Rerun = 1,

    /// <summary>
    /// 按策略跳过
    /// </summary>
    Skipped = 2,

    /// <summary>
    /// 补偿触发失败
    /// </summary>
    Failed = 3,
}
