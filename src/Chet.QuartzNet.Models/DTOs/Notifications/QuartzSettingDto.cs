using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

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
