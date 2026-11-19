using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 迁移存储描述
/// </summary>
public class MigrationStoreDescriptor
{
    /// <summary>
    /// 存储类型
    /// </summary>
    public MigrationStoreType Type { get; set; } = MigrationStoreType.File;

    /// <summary>
    /// 文件存储路径（File 类型使用）
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 数据库连接字符串（仅服务端解析，不下发前端）
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 数据库服务器版本（MySQL 可选）
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// 服务端配置档名称
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// 获取用于展示的存储描述（不包含敏感连接串）
    /// </summary>
    public string GetDisplayName()
    {
        if (Type == MigrationStoreType.File)
        {
            return $"File({GetDisplayPath(FilePath)})";
        }

        return string.IsNullOrWhiteSpace(ProfileName)
            ? Type.ToString()
            : $"{Type}({ProfileName})";
    }

    /// <summary>
    /// 获取展示用路径：在工作目录内时使用相对路径，否则回退为完整路径
    /// </summary>
    private static string GetDisplayPath(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        try
        {
            var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, fullPath);
            var isOutsideWorkingDirectory =
                relativePath.Equals("..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
            if (!isOutsideWorkingDirectory)
            {
                return relativePath;
            }
        }
        catch (Exception)
        {
            // 路径解析失败时保持完整路径
        }

        return fullPath;
    }
}
