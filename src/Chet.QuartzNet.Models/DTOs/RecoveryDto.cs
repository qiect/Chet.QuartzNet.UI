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
