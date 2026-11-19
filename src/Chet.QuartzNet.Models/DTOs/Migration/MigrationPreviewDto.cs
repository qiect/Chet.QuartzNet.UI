using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移预检结果
/// </summary>
public class MigrationPreviewDto
{
    /// <summary>
    /// 是否允许执行迁移
    /// </summary>
    public bool CanRun { get; set; }

    /// <summary>
    /// 校验失败原因
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 源存储展示名称
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 目标存储展示名称
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 写入模式
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 逐实体预检结果
    /// </summary>
    public List<MigrationPreviewEntityDto> Entities { get; set; } = new();

    /// <summary>
    /// 告警信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
