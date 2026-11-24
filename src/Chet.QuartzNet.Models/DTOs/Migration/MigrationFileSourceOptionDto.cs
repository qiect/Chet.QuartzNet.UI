using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移文件源选项
/// </summary>
public class MigrationFileSourceOptionDto
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 文件完整路径（用于实际迁移，避免展示过长）
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 用于界面展示的路径：在工作目录内时为相对路径，否则回退为完整路径
    /// </summary>
    public string DisplayPath { get; set; } = string.Empty;
}
