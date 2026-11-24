namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 统计查询DTO
/// </summary>
public class StatsQueryDto
{
    /// <summary>
    /// 时间范围类型：today, yesterday, thisWeek, thisMonth, custom
    /// </summary>
    public string? TimeRangeType { get; set; }

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
