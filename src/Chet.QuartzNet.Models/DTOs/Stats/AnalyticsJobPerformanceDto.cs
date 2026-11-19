namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 作业性能分析聚合DTO（合并GetJobHealthOverview + GetTopSlowJobs）
/// </summary>
public class AnalyticsJobPerformanceDto
{
    /// <summary>
    /// 作业健康概览数据
    /// </summary>
    public List<JobHealthDto> JobHealthOverview { get; set; } = [];

    /// <summary>
    /// 耗时基线分析数据
    /// </summary>
    public List<TopSlowJobDto> TopSlowJobs { get; set; } = [];
}
