using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// Quartz作业DTO
/// </summary>
public class QuartzJobDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    [Required(ErrorMessage = "作业名称不能为空")]
    [StringLength(100, ErrorMessage = "作业名称长度不能超过100个字符")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    [Required(ErrorMessage = "作业分组不能为空")]
    [StringLength(100, ErrorMessage = "作业分组长度不能超过100个字符")]
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>
    /// 触发器名称（可选，若为空则自动根据作业名称生成）
    /// </summary>
    [StringLength(100, ErrorMessage = "触发器名称长度不能超过100个字符")]
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发器分组（可选，默认与作业分组相同）
    /// </summary>
    [StringLength(100, ErrorMessage = "触发器分组长度不能超过100个字符")]
    public string? TriggerGroup { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    [Required(ErrorMessage = "Cron表达式不能为空")]
    [StringLength(200, ErrorMessage = "Cron表达式长度不能超过200个字符")]
    public string CronExpression { get; set; } = "0 0/1 * * * ?";

    /// <summary>
    /// 作业描述
    /// </summary>
    [StringLength(500, ErrorMessage = "作业描述长度不能超过500个字符")]
    public string? Description { get; set; }

    /// <summary>
    /// 作业类型
    /// </summary>
    public JobTypeEnum JobType { get; set; } = JobTypeEnum.DLL;

    /// <summary>
    /// 作业类名或API URL
    /// </summary>
    [Required(ErrorMessage = "作业类名或API URL不能为空")]
    [StringLength(500, ErrorMessage = "作业类名或API URL长度不能超过500个字符")]
    public string JobClassOrApi { get; set; } = string.Empty;

    /// <summary>
    /// 作业数据（JSON格式）
    /// </summary>
    [JsonValidation(ErrorMessage = "作业数据必须是有效的JSON格式")]
    public string? JobData { get; set; }

    /// <summary>
    /// API请求方法（GET/POST等）
    /// </summary>
    [StringLength(10, ErrorMessage = "API请求方法长度不能超过10个字符")]
    public string? ApiMethod { get; set; } = "GET";

    /// <summary>
    /// API请求头（JSON格式）
    /// </summary>
    [JsonValidation(ErrorMessage = "API请求头必须是有效的JSON格式")]
    public string? ApiHeaders { get; set; }

    /// <summary>
    /// API请求体（JSON格式）
    /// </summary>
    [JsonValidation(ErrorMessage = "API请求体必须是有效的JSON格式")]
    public string? ApiBody { get; set; }

    /// <summary>
    /// API超时时间（秒）
    /// </summary>
    [ConditionalRange(
        1,
        99999,
        nameof(JobType),
        JobTypeEnum.API,
        ErrorMessage = "API超时时间必须在1秒到99999秒之间"
    )]
    public int ApiTimeout { get; set; } = 60; // 默认60秒

    /// <summary>
    /// 失败重试次数（0=不重试）
    /// </summary>
    [Range(0, 10, ErrorMessage = "重试次数必须在0到10之间")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 失败重试间隔（秒）
    /// </summary>
    [Range(1, 3600, ErrorMessage = "重试间隔必须在1秒到3600秒之间")]
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
    /// 中断后是否自动重跑。null = 跟随全局；true = 开启；false = 关闭
    /// </summary>
    public bool? RerunOnRecovery { get; set; }

    /// <summary>
    /// 前置依赖作业列表，依赖作业最近一次执行成功后才执行本作业
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
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
