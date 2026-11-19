namespace Chet.QuartzNet.Models.DTOs;

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
