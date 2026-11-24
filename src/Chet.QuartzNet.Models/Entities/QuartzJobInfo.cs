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
    /// 失败重试次数（0=不重试，默认关闭）
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 失败重试间隔（秒）
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 跳过SSL验证
    /// </summary>
    public bool SkipSslValidation { get; set; } = false;

    /// <summary>
    /// 禁止并发执行（同一作业上一次执行未完成时，新的触发会等待）
    /// </summary>
    public bool DisallowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// 中断后是否自动重跑。
    /// null = 跟随全局配置；true = 开启；false = 关闭。
    /// </summary>
    public bool? RerunOnRecovery { get; set; }

    /// <summary>
    /// 前置依赖作业（JSON数组，元素为 {"jobGroup":"...","jobName":"..."}）
    /// 依赖作业最近一次执行成功后才执行本作业，否则本次跳过
    /// </summary>
    public string? Dependencies { get; set; }

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
