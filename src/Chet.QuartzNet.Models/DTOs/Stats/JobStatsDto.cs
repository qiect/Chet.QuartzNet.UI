namespace Chet.QuartzNet.Models.DTOs;

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
