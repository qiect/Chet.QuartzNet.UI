using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// Quartz作业信息实体
/// </summary>
public class QuartzJobInfo
{
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
    /// 触发器名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>
    /// 触发器分组
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TriggerGroup { get; set; } = string.Empty;

    /// <summary>
    /// Cron表达式
    /// </summary>
    [Required]
    [StringLength(200)]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 作业描述
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 作业类型
    /// </summary>
    public JobTypeEnum JobType { get; set; } = JobTypeEnum.DLL;

    /// <summary>
    /// 作业类名或API URL
    /// </summary>
    [Required]
    [StringLength(500)]
    public string JobClassOrApi { get; set; } = string.Empty;

    /// <summary>
    /// 作业数据（JSON格式）
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// API请求方法（GET/POST等）
    /// </summary>
    [StringLength(10)]
    public string? ApiMethod { get; set; } = "GET";

    /// <summary>
    /// API请求头（JSON格式）
    /// </summary>
    public string? ApiHeaders { get; set; }

    /// <summary>
    /// API请求体（JSON格式）
    /// </summary>
    public string? ApiBody { get; set; }

    /// <summary>
    /// API超时时间（秒）
    /// </summary>
    public int ApiTimeout { get; set; } = 60; // 默认60秒

    /// <summary>
    /// 跳过SSL验证
    /// </summary>
    public bool SkipSslValidation { get; set; } = false;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 作业状态
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Normal;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTime? NextRunTime { get; set; }

    /// <summary>
    /// 上次执行时间
    /// </summary>
    public DateTime? PreviousRunTime { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [StringLength(500)]
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [StringLength(100)]
    public string? CreateBy { get; set; }

    /// <summary>
    /// 更新人
    /// </summary>
    [StringLength(100)]
    public string? UpdateBy { get; set; }

    /// <summary>
    /// 获取作业键
    /// </summary>
    public string GetJobKey() => $"{JobGroup}.{JobName}";

    /// <summary>
    /// 获取触发器键
    /// </summary>
    public string GetTriggerKey() => $"{TriggerGroup}.{TriggerName}";
}

/// <summary>
/// 作业类型枚举
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobTypeEnum
{
    /// <summary>
    /// DLL作业（默认）
    /// </summary>
    DLL = 0,

    /// <summary>
    /// API作业
    /// </summary>
    API = 1
}

/// <summary>
/// 作业状态枚举
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 暂停
    /// </summary>
    Paused = 1,

    /// <summary>
    /// 完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 3,

    /// <summary>
    /// 阻塞
    /// </summary>
    Blocked = 4
}

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
}

/// <summary>
/// 日志状态枚举
/// </summary>
public enum LogStatus
{
    /// <summary>
    /// 运行中
    /// </summary>
    Running = 0,

    /// <summary>
    /// 成功
    /// </summary>
    Success = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 2
}