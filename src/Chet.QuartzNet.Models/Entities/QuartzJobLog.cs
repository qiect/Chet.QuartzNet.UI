using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 作业执行记录
/// </summary>
public class QuartzJobLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public Guid LogId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 作业名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    [Required]
    [StringLength(100)]
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
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误堆栈
    /// </summary>
    public string? ErrorStackTrace { get; set; }

    /// <summary>
    /// 执行结果
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 触发器名称
    /// </summary>
    [StringLength(100)]
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发器分组
    /// </summary>
    [StringLength(100)]
    public string? TriggerGroup { get; set; }

    /// <summary>
    /// 执行参数
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 执行该记录的实例标识（机器名 + 进程号 + 启动短标识）
    /// </summary>
    [StringLength(64)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 进程启动标识，用于区分同一实例的上一次运行
    /// </summary>
    public Guid? BootId { get; set; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public TriggerType TriggerType { get; set; } = TriggerType.Cron;

    /// <summary>
    /// 手动触发/补偿触发的操作人
    /// </summary>
    [StringLength(100)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// 补偿记录指向被补偿的中断记录
    /// </summary>
    public Guid? RecoverySourceLogId { get; set; }

    /// <summary>
    /// 第几次尝试（补偿记录 = 源记录 + 1）
    /// </summary>
    public int RecoveryAttempt { get; set; } = 1;

    /// <summary>
    /// 最后一次心跳时间
    /// </summary>
    public DateTime? HeartbeatTime { get; set; }

    /// <summary>
    /// 判定为中断的时刻
    /// </summary>
    public DateTime? InterruptedAt { get; set; }

    /// <summary>
    /// 中断记录的补偿状态
    /// </summary>
    public CompensationStatus CompensationStatus { get; set; } = CompensationStatus.None;

    /// <summary>
    /// 本次补偿产生的日志ID
    /// </summary>
    public Guid? CompensationLogId { get; set; }

    /// <summary>
    /// 跳过补偿的原因
    /// </summary>
    [StringLength(500)]
    public string? SkipReason { get; set; }
}
