using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// Quartz作业响应DTO
/// </summary>
public class QuartzJobResponseDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// 触发器名称
    /// </summary>
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>
    /// 触发器分组
    /// </summary>
    public string TriggerGroup { get; set; } = string.Empty;

    /// <summary>
    /// Cron表达式
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 作业描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 作业类型
    /// </summary>
    public JobTypeEnum JobType { get; set; }

    /// <summary>
    /// 作业类名或API URL
    /// </summary>
    public string JobClassOrApi { get; set; } = string.Empty;

    /// <summary>
    /// 作业数据（JSON格式）
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// API请求方法
    /// </summary>
    public string? ApiMethod { get; set; }

    /// <summary>
    /// API请求头
    /// </summary>
    public string? ApiHeaders { get; set; }

    /// <summary>
    /// API请求体
    /// </summary>
    public string? ApiBody { get; set; }

    /// <summary>
    /// API请求超时时间（秒）
    /// </summary>
    public int ApiTimeout { get; set; }

    /// <summary>
    /// 失败重试次数（0=不重试）
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 失败重试间隔（秒）
    /// </summary>
    public int RetryIntervalSeconds { get; set; }

    /// <summary>
    /// 是否跳过SSL验证
    /// </summary>
    public bool SkipSslValidation { get; set; }

    /// <summary>
    /// 禁止并发执行
    /// </summary>
    public bool DisallowConcurrentExecution { get; set; }

    /// <summary>
    /// 中断后是否自动重跑（原值，null = 跟随全局）
    /// </summary>
    public bool? RerunOnRecovery { get; set; }

    /// <summary>
    /// 中断后是否自动重跑（按全局配置解析后的最终值）
    /// </summary>
    public bool EffectiveRerunOnRecovery { get; set; }

    /// <summary>
    /// 前置依赖作业列表
    /// </summary>
    public List<JobDependencyDto> Dependencies { get; set; } = new();

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
    public JobStatus Status { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreateBy { get; set; }

    /// <summary>
    /// 更新人
    /// </summary>
    public string? UpdateBy { get; set; }

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTime? NextRunTime { get; set; }

    /// <summary>
    /// 上次执行时间
    /// </summary>
    public DateTime? PreviousRunTime { get; set; }
}
