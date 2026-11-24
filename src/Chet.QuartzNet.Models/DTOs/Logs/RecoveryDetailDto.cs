using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 单条中断恢复明细
/// </summary>
public class RecoveryDetailDto
{
    /// <summary>
    /// 中断日志ID
    /// </summary>
    public Guid LogId { get; set; }

    /// <summary>
    /// 作业名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// 第几次尝试
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// 处理结果
    /// </summary>
    public RecoveryOutcome Outcome { get; set; }

    /// <summary>
    /// 原因
    /// </summary>
    public string? Reason { get; set; }
}
