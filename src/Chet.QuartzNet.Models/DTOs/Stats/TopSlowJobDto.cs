namespace Chet.QuartzNet.Models.DTOs;

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
