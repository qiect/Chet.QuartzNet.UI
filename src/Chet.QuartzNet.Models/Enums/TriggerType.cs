using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 日志触发来源
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    /// <summary>
    /// Cron 定时触发
    /// </summary>
    Cron = 0,

    /// <summary>
    /// 手动触发
    /// </summary>
    Manual = 1,

    /// <summary>
    /// 中断补偿重跑
    /// </summary>
    Recovery = 2,
}
