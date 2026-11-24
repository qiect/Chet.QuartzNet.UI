namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 统计分析概览聚合DTO（合并GetJobStats + GetJobStatusDistribution + GetJobExecutionTrend + GetJobExecutionHeatmap）
/// </summary>
public class AnalyticsOverviewDto
{
    /// <summary>
    /// 作业统计概览
    /// </summary>
    public JobStatsDto Stats { get; set; } = new();

    /// <summary>
    /// 作业状态分布
    /// </summary>
    public List<JobStatusDistributionDto> StatusDistribution { get; set; } = [];

    /// <summary>
    /// 执行趋势数据
    /// </summary>
    public List<JobExecutionTrendDto> ExecutionTrend { get; set; } = [];

    /// <summary>
    /// 执行热力图数据
    /// </summary>
    public List<JobExecutionHeatmapDto> ExecutionHeatmap { get; set; } = [];
}
