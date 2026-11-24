namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 分页响应DTO
/// </summary>
/// <typeparam name="T"></typeparam>
public class PagedResponseDto<T>
{
    /// <summary>
    /// 数据列表
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 总条数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
