namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 统计查询DTO
/// </summary>
public class StatsQueryDto
{
    /// <summary>
    /// 时间范围类型：today, yesterday, thisWeek, thisMonth, custom
    /// </summary>
    public string? TimeRangeType { get; set; } = "today";

    /// <summary>
    /// 自定义开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 自定义结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 作业名称
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 作业分组
    /// </summary>
    public string? JobGroup { get; set; }
}

/// <summary>
/// 作业统计数据DTO
/// </summary>
public class JobStatsDto
{
    /// <summary>
    /// 总作业数
    /// </summary>
    public int TotalJobs { get; set; }

    /// <summary>
    /// 启用的作业数
    /// </summary>
    public int EnabledJobs { get; set; }

    /// <summary>
    /// 禁用的作业数
    /// </summary>
    public int DisabledJobs { get; set; }

    /// <summary>
    /// 总执行数
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// 成功的执行数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败的执行数
    /// </summary>
    public int FailedCount { get; set; }
}

/// <summary>
/// 作业状态分布数据DTO
/// </summary>
public class JobStatusDistributionDto
{
    /// <summary>
    /// 作业状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 数量
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 百分比
    /// </summary>
    public double Percentage { get; set; }
}

/// <summary>
/// 作业执行趋势数据DTO
/// </summary>
public class JobExecutionTrendDto
{
    /// <summary>
    /// 时间点
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// 成功执行次数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败执行次数
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalCount { get; set; }
}

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
    /// 成功率（0-100）
    /// </summary>
    public double SuccessRate { get; set; }

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
    /// 最近执行时间
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    public string? CronExpression { get; set; }
}

/// <summary>
/// 作业执行热力图数据DTO
/// </summary>
public class JobExecutionHeatmapDto
{
    /// <summary>
    /// 星期几（1=周一...7=周日）
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// 小时（0-23）
    /// </summary>
    public int Hour { get; set; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public int FailedCount { get; set; }
}

/// <summary>
/// 耗时基线分析数据DTO（原Top慢作业排行）
/// </summary>
public class TopSlowJobDto
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
    /// 平均执行耗时（毫秒）
    /// </summary>
    public double AvgDuration { get; set; }

    /// <summary>
    /// 最大执行耗时（毫秒）
    /// </summary>
    public double MaxDuration { get; set; }

    /// <summary>
    /// 最小执行耗时（毫秒）
    /// </summary>
    public double MinDuration { get; set; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// 成功率（0-100）
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 最近执行时间
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }
}