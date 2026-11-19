using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移数据库配置档选项（不包含连接字符串）
/// </summary>
public class MigrationProfileOptionDto
{
    /// <summary>
    /// 配置档名称（附加数据库固定为 additional）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 是否已配置连接字符串
    /// </summary>
    public bool HasConnectionString { get; set; }

    /// <summary>
    /// 是否为简化配置的附加数据库
    /// </summary>
    public bool IsAdditional { get; set; }
}
