using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// Quartz作业日志DTO
/// </summary>
public class QuartzJobLogDto
{
    /// <summary>
    /// 日志ID
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
    /// 执行状态
    /// </summary>
    public LogStatus Status { get; set; }

    /// <summary>
    /// 执行开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 执行结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? Duration { get; set; }

    /// <summary>
    /// 执行结果消息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// 执行参数
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// 补偿来源日志ID
    /// </summary>
    public Guid? RecoverySourceLogId { get; set; }

    /// <summary>
    /// 第几次尝试
    /// </summary>
    public int RecoveryAttempt { get; set; }

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTime? HeartbeatTime { get; set; }

    /// <summary>
    /// 判定中断的时间
    /// </summary>
    public DateTime? InterruptedAt { get; set; }

    /// <summary>
    /// 补偿状态
    /// </summary>
    public CompensationStatus CompensationStatus { get; set; }

    /// <summary>
    /// 跳过补偿的原因
    /// </summary>
    public string? SkipReason { get; set; }
}

/// <summary>
/// Quartz作业日志查询DTO
/// </summary>
public class QuartzJobLogQueryDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 作业分组
    /// </summary>
    public string? JobGroup { get; set; }

    /// <summary>
    /// 执行状态（单状态筛选）
    /// </summary>
    public LogStatus? Status { get; set; }

    /// <summary>
    /// 执行状态集合（多状态筛选，如中断 + 失败）
    /// </summary>
    public List<LogStatus>? Statuses { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 开始时间范围查询-下限（针对 StartTime 字段）
    /// </summary>
    public DateTime? StartStartTime { get; set; }

    /// <summary>
    /// 开始时间范围查询-上限（针对 StartTime 字段）
    /// </summary>
    public DateTime? EndStartTime { get; set; }

    /// <summary>
    /// 结束时间范围查询-下限（针对 EndTime 字段）
    /// </summary>
    public DateTime? StartEndTime { get; set; }

    /// <summary>
    /// 结束时间范围查询-上限（针对 EndTime 字段）
    /// </summary>
    public DateTime? EndEndTime { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// 排序字段
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 排序方向（asc或desc）
    /// </summary>
    public string? SortOrder { get; set; }
}
