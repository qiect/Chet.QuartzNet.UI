using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 停机模式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShutdownMode
{
    /// <summary>
    /// 立即停止，不等待执行中的任务
    /// </summary>
    Immediate = 0,

    /// <summary>
    /// 停止接收新触发，等待执行中任务收尾；超时后强制关闭并把未完成的执行标记为中断
    /// </summary>
    Drain = 1,

    /// <summary>
    /// 兼容别名：与 <see cref="Drain"/> 行为完全一致（超时也使用排水超时），
    /// 仅为兼容历史配置保留，新配置不应再使用。
    /// </summary>
    Wait = 2,
}
