using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// Quartz作业日志查询DTO
/// </summary>
public class QuartzJobLogQueryDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 作业分组
    /// </summary>
    public string? JobGroup { get; set; }

    /// <summary>
    /// 执行状态（单状态筛选）
    /// </summary>
    public LogStatus? Status { get; set; }

    /// <summary>
    /// 执行状态集合（多状态筛选，如中断 + 失败）
    /// </summary>
    public List<LogStatus>? Statuses { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 开始时间范围查询-下限（针对 StartTime 字段）
    /// </summary>
    public DateTime? StartStartTime { get; set; }

    /// <summary>
    /// 开始时间范围查询-上限（针对 StartTime 字段）
    /// </summary>
    public DateTime? EndStartTime { get; set; }

    /// <summary>
    /// 结束时间范围查询-下限（针对 EndTime 字段）
    /// </summary>
    public DateTime? StartEndTime { get; set; }

    /// <summary>
    /// 结束时间范围查询-上限（针对 EndTime 字段）
    /// </summary>
    public DateTime? EndEndTime { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// 排序字段
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 排序方向（asc或desc）
    /// </summary>
    public string? SortOrder { get; set; }
}
