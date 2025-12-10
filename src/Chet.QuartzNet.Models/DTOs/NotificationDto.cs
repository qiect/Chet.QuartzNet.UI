using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// PushPlus配置DTO
/// </summary>
public class PushPlusConfigDto
{
    /// <summary>
    /// PushPlus Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 推送渠道
    /// 可选值：wechat, cp, webhook, mail, sms
    /// </summary>
    public string Channel { get; set; } = "wechat";

    /// <summary>
    /// 消息模板
    /// 可选值：html, text, markdown
    /// </summary>
    public string Template { get; set; } = "html";

    /// <summary>
    /// 主题
    /// 用于订阅推送
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 通知策略
    /// </summary>
    public NotificationStrategyDto Strategy { get; set; } = new();
}

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

/// <summary>
/// 通知查询DTO
/// </summary>
public class NotificationQueryDto
{
    /// <summary>
    /// 状态
    /// </summary>
    public NotificationStatus? Status { get; set; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 排序字段
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 排序方向（asc或desc）
    /// </summary>
    public string? SortOrder { get; set; }
}

/// <summary>
/// 通知设置DTO
/// </summary>
public class QuartzSettingDto
{
    /// <summary>
    /// 设置ID
    /// </summary>
    public Guid SettingId { get; set; }

    /// <summary>
    /// 设置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 设置值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 设置描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }
}