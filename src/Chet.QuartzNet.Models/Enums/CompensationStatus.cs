using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 中断记录的补偿状态
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompensationStatus
{
    /// <summary>
    /// 无（非中断记录，或尚未进入补偿流程）
    /// </summary>
    None = 0,

    /// <summary>
    /// 已判定中断，等待补偿
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 已触发补偿
    /// </summary>
    Triggered = 2,

    /// <summary>
    /// 按策略跳过
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// 补偿触发失败
    /// </summary>
    Failed = 4,
}
