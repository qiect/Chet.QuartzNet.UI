using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 预检时单个实体的统计
/// </summary>
public class MigrationPreviewEntityDto
{
    /// <summary>
    /// 实体键
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// 实体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 源端数量
    /// </summary>
    public long SourceCount { get; set; }

    /// <summary>
    /// 目标端已有数量
    /// </summary>
    public long TargetCount { get; set; }

    /// <summary>
    /// 预计插入数量
    /// </summary>
    public long WillInsert { get; set; }

    /// <summary>
    /// 预计更新数量
    /// </summary>
    public long WillUpdate { get; set; }

    /// <summary>
    /// 预计跳过数量
    /// </summary>
    public long WillSkip { get; set; }
}
