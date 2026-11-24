using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

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
