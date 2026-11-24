using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 本次启动的中断恢复汇总
/// </summary>
public class RecoverySummaryDto
{
    /// <summary>
    /// 进程启动标识
    /// </summary>
    public Guid BootId { get; set; }

    /// <summary>
    /// 实例标识
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 进程启动时间
    /// </summary>
    public DateTime BootTime { get; set; }

    /// <summary>
    /// 恢复流程开始时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 恢复流程完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 是否仍在恢复中
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 本次检测到的中断执行数
    /// </summary>
    public int InterruptedCount { get; set; }

    /// <summary>
    /// 已自动重跑数
    /// </summary>
    public int RerunCount { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 仍待处理数
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 明细是否被截断
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// 明细
    /// </summary>
    public List<RecoveryDetailDto> Details { get; set; } = new();
}
