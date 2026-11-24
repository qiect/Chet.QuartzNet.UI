using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 手动中断运行中作业的请求
/// </summary>
public class InterruptRunningLogRequestDto
{
    /// <summary>
    /// 运行中日志ID
    /// </summary>
    public Guid LogId { get; set; }
}
