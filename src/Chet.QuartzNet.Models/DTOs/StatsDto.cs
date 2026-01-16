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
/// 作业类型分布数据DTO
/// </summary>
public class JobTypeDistributionDto
{
    /// <summary>
    /// 作业类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

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
/// 作业执行耗时数据DTO
/// </summary>
public class JobExecutionTimeDto
{
    /// <summary>
    /// 耗时区间
    /// </summary>
    public string TimeRange { get; set; } = string.Empty;

    /// <summary>
    /// 作业数量
    /// </summary>
    public int Count { get; set; }
}