using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 数据库 Provider 安装状态
/// </summary>
public class MigrationProviderOptionDto
{
    /// <summary>
    /// 存储类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 对应 NuGet 包名
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// 是否已安装
    /// </summary>
    public bool Installed { get; set; }
}
