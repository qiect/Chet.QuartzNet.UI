namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 作业健康概览数据DTO（用于散点气泡图）
/// </summary>
public class JobHealthDto
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
    /// 作业状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 成功率（0-100，成功数 / 总执行数）
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 失败执行次数（不含中断/运行中）
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 平均执行耗时（毫秒）
    /// </summary>
    public double AvgDuration { get; set; }

    /// <summary>
    /// 最大执行耗时（毫秒）
    /// </summary>
    public double MaxDuration { get; set; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// 创建时间（用于识别新建作业，避免沉默误报）
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 最近执行时间
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    public string? CronExpression { get; set; }
}
