using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 手动重新执行中断记录的请求
/// </summary>
public class RerunInterruptedLogRequestDto
{
    /// <summary>
    /// 中断日志ID
    /// </summary>
    public Guid LogId { get; set; }
}
