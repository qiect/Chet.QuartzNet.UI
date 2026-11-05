using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 数据迁移步骤状态
/// </summary>
public enum MigrationStepStatus
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 进行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 跳过（无数据或未选择）
    /// </summary>
    Skipped = 4,
}

/// <summary>
/// 迁移存储类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationStoreType
{
    /// <summary>
    /// 文件存储
    /// </summary>
    File = 0,

    /// <summary>
    /// MySQL
    /// </summary>
    MySql = 1,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSql = 2,

    /// <summary>
    /// SQLite
    /// </summary>
    SQLite = 3,

    /// <summary>
    /// SQL Server
    /// </summary>
    SqlServer = 4,
}

/// <summary>
/// 迁移实体类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationEntity
{
    /// <summary>
    /// 作业
    /// </summary>
    Jobs = 0,

    /// <summary>
    /// 作业日志
    /// </summary>
    Logs = 1,

    /// <summary>
    /// 系统设置
    /// </summary>
    Settings = 2,

    /// <summary>
    /// 通知消息
    /// </summary>
    Notifications = 3,
}

/// <summary>
/// 迁移写入模式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationWriteMode
{
    /// <summary>
    /// 不存在则插入，存在则跳过（设置为按键 Upsert），幂等
    /// </summary>
    Merge = 0,

    /// <summary>
    /// 不存在则插入，存在则更新
    /// </summary>
    Overwrite = 1,

    /// <summary>
    /// 先清空所选实体的目标数据，再写入
    /// </summary>
    Replace = 2,
}

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

/// <summary>
/// 迁移步骤信息
/// </summary>
public class MigrationStepInfo
{
    /// <summary>
    /// 步骤名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 步骤键（jobs/logs/settings/notifications）
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 步骤状态
    /// </summary>
    public MigrationStepStatus Status { get; set; } = MigrationStepStatus.Pending;

    /// <summary>
    /// 总记录数
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// 已迁移数
    /// </summary>
    public long MigratedCount { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public long SkippedCount { get; set; }

    /// <summary>
    /// 更新数
    /// </summary>
    public long UpdatedCount { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public long FailedCount { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 步骤开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 步骤结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 单个实体的迁移状态
/// </summary>
public class MigrationEntityState
{
    /// <summary>
    /// 实体键（jobs/logs/settings/notifications）
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// 实体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 实体状态
    /// </summary>
    public MigrationStepStatus Status { get; set; } = MigrationStepStatus.Pending;

    /// <summary>
    /// 源端记录数
    /// </summary>
    public long SourceCount { get; set; }

    /// <summary>
    /// 已处理数
    /// </summary>
    public long Processed { get; set; }

    /// <summary>
    /// 插入数
    /// </summary>
    public long Inserted { get; set; }

    /// <summary>
    /// 更新数
    /// </summary>
    public long Updated { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public long Skipped { get; set; }

    /// <summary>
    /// 失败数
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 单行迁移错误样本
/// </summary>
public class MigrationRowError
{
    /// <summary>
    /// 实体键（jobs/logs/settings/notifications）
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// 记录键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 相关字段
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 数据迁移状态DTO
/// </summary>
public class DataMigrationStatusDto
{
    /// <summary>
    /// 迁移是否正在运行
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 整体进度百分比（0-100）
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// 当前步骤描述
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// 各步骤详情（保留旧字段，兼容旧前端）
    /// </summary>
    public List<MigrationStepInfo> Steps { get; set; } = new();

    /// <summary>
    /// 迁移开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 迁移结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 总耗时（毫秒）
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 是否已完成（成功或失败）
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 文件存储路径
    /// </summary>
    public string FileStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储路径是否存在
    /// </summary>
    public bool FileStoragePathExists { get; set; }

    /// <summary>
    /// 当前存储类型
    /// </summary>
    public string StorageType { get; set; } = string.Empty;

    /// <summary>
    /// 迁移ID
    /// </summary>
    public string MigrationId { get; set; } = string.Empty;

    /// <summary>
    /// 源存储展示名称
    /// </summary>
    public string SourceDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 目标存储展示名称
    /// </summary>
    public string TargetDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 写入模式
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// 当前阶段（Idle/Validate/Probe/Backup/EnsureSchema/Migrate/Verify/Done）
    /// </summary>
    public string Stage { get; set; } = "Idle";

    /// <summary>
    /// 当前处理的实体
    /// </summary>
    public string? CurrentEntity { get; set; }

    /// <summary>
    /// 已处理记录数
    /// </summary>
    public long Processed { get; set; }

    /// <summary>
    /// 待处理记录总数
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 是否已取消
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// 逐实体迁移状态
    /// </summary>
    public List<MigrationEntityState> Entities { get; set; } = new();

    /// <summary>
    /// 错误样本（最多保留配置数量）
    /// </summary>
    public List<MigrationRowError> ErrorSamples { get; set; } = new();

    /// <summary>
    /// 告警与校验提示
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 迁移完成后建议的配置片段
    /// </summary>
    public string? SuggestedConfig { get; set; }
}

/// <summary>
/// 触发迁移请求DTO
/// </summary>
public class TriggerMigrationRequestDto
{
    /// <summary>
    /// 是否强制重新迁移（即使已成功迁移过也重新执行）
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// 迁移源（不传时默认当前文件存储路径）
    /// </summary>
    public MigrationStoreDescriptor? Source { get; set; }

    /// <summary>
    /// 迁移目标（不传时默认当前数据库）
    /// </summary>
    public MigrationStoreDescriptor? Target { get; set; }

    /// <summary>
    /// 需要迁移的实体，不传时默认全部
    /// </summary>
    public List<MigrationEntity>? Entities { get; set; }

    /// <summary>
    /// 写入模式，默认 Merge
    /// </summary>
    public MigrationWriteMode Mode { get; set; } = MigrationWriteMode.Merge;

    /// <summary>
    /// 出错时是否继续后续批次（默认取服务端配置）
    /// </summary>
    public bool? ContinueOnError { get; set; }

    /// <summary>
    /// Replace 模式的二次确认标记
    /// </summary>
    public bool ConfirmReplace { get; set; }

    /// <summary>
    /// 迁移期间是否暂停调度器（仅暂停触发，完成后恢复）
    /// </summary>
    public bool PauseScheduler { get; set; }

    /// <summary>
    /// 是否允许对源数据库执行结构升级（默认否）
    /// </summary>
    public bool AllowUpgradeSourceSchema { get; set; }
}

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
