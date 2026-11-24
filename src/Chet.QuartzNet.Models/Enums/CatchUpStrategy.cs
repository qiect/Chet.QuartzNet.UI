using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 中断补偿策略
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatchUpStrategy
{
    /// <summary>
    /// 只标记中断，不自动补偿
    /// </summary>
    None = 0,

    /// <summary>
    /// 每个作业只补偿最近一次中断
    /// </summary>
    Latest = 1,

    /// <summary>
    /// 补偿全部中断记录
    /// </summary>
    All = 2,
}
