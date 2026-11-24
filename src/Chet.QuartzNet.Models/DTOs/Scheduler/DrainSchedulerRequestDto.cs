using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 排水请求
/// </summary>
public class DrainSchedulerRequestDto
{
    /// <summary>
    /// 覆盖默认排水超时（秒）
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}
