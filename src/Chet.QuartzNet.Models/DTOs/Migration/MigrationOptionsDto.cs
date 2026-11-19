using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移可选项
/// </summary>
public class MigrationOptionsDto
{
    /// <summary>
    /// 当前存储类型
    /// </summary>
    public string CurrentStorageType { get; set; } = string.Empty;

    /// <summary>
    /// 当前数据库 Provider
    /// </summary>
    public string? CurrentDatabaseProvider { get; set; }

    /// <summary>
    /// 当前文件存储路径
    /// </summary>
    public string FileStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 当前文件存储路径是否存在
    /// </summary>
    public bool FileStoragePathExists { get; set; }

    /// <summary>
    /// 可选的文件源
    /// </summary>
    public List<MigrationFileSourceOptionDto> FileSources { get; set; } = new();

    /// <summary>
    /// 可选的数据库配置档
    /// </summary>
    public List<MigrationProfileOptionDto> Profiles { get; set; } = new();

    /// <summary>
    /// 数据库 Provider 安装状态
    /// </summary>
    public List<MigrationProviderOptionDto> Providers { get; set; } = new();

    /// <summary>
    /// 是否允许 Replace 模式
    /// </summary>
    public bool AllowReplace { get; set; }

    /// <summary>
    /// 是否允许前端提交连接字符串
    /// </summary>
    public bool AllowAdHocConnectionString { get; set; }

    /// <summary>
    /// 批次大小
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// 错误样本上限
    /// </summary>
    public int MaxErrorSamples { get; set; }
}
