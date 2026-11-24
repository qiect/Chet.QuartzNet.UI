using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 通知消息DTO
/// </summary>
public class QuartzNotificationDto
{
    /// <summary>
    /// 通知ID
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// 通知标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 发送状态
    /// </summary>
    public NotificationStatus Status { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime? SendTime { get; set; }

    /// <summary>
    /// 发送耗时（毫秒）
    /// </summary>
    public long? Duration { get; set; }
}
