using System.ComponentModel.DataAnnotations;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 系统设置实体
/// 用于存储系统的各种配置信息
/// </summary>
public class QuartzSetting
{
    /// <summary>
    /// 设置ID
    /// </summary>
    [Key]
    public Guid SettingId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 设置键
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 设置值
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 设置描述
    /// </summary>
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }
}
