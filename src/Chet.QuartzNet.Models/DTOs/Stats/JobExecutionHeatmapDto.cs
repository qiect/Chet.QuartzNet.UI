namespace Chet.QuartzNet.Models.DTOs;

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
