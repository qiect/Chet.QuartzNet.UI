using System.ComponentModel.DataAnnotations;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 通知状态枚举
/// </summary>
public enum NotificationStatus
{
    /// <summary>
    /// 待发送
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 发送成功
    /// </summary>
    Sent = 1,

    /// <summary>
    /// 发送失败
    /// </summary>
    Failed = 2,
}
