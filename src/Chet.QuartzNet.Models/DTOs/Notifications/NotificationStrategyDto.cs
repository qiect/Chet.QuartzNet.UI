using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 通知策略DTO
/// </summary>
public class NotificationStrategyDto
{
    /// <summary>
    /// 作业成功时发送通知
    /// </summary>
    public bool NotifyOnJobSuccess { get; set; } = false;

    /// <summary>
    /// 作业失败时发送通知
    /// </summary>
    public bool NotifyOnJobFailure { get; set; } = true;

    /// <summary>
    /// 调度器异常时发送通知
    /// </summary>
    public bool NotifyOnSchedulerError { get; set; } = true;
}
