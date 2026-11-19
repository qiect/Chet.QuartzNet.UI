namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 作业性能查询DTO（扩展StatsQueryDto，增加TopCount参数）
/// </summary>
public class JobPerformanceQueryDto : StatsQueryDto
{
    /// <summary>
    /// 耗时排行取前N条，默认10
    /// </summary>
    public int TopCount { get; set; } = 10;
}
