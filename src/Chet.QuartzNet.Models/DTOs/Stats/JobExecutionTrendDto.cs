namespace Chet.QuartzNet.Models.DTOs;

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
    /// 中断执行次数（进程退出/停机超时等未正常收尾，不含运行中、跳过）
    /// </summary>
    public int InterruptedCount { get; set; }

    /// <summary>
    /// 总执行次数
    /// </summary>
    public int TotalCount { get; set; }
}
