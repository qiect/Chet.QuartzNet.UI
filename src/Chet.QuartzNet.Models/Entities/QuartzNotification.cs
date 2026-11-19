using System.ComponentModel.DataAnnotations;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 通知消息实体
/// 用于存储系统发送的各种通知消息
/// </summary>
public class QuartzNotification
{
    /// <summary>
    /// 通知ID
    /// </summary>
    [Key]
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 通知标题
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 发送状态
    /// </summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 触发来源
    /// 例如：作业名称、调度器等
    /// </summary>
    [StringLength(100)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 发送时间
    /// </summary>
    public DateTime? SendTime { get; set; }

    /// <summary>
    /// 发送耗时（毫秒）
    /// </summary>
    public long? Duration { get; set; }
}
