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
    /// 跳过（无数据）
    /// </summary>
    Skipped = 4,
}

/// <summary>
/// 数据迁移步骤信息
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
    public int TotalCount { get; set; }

    /// <summary>
    /// 已迁移数
    /// </summary>
    public int MigratedCount { get; set; }

    /// <summary>
    /// 跳过数
    /// </summary>
    public int SkippedCount { get; set; }

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
    /// 各步骤详情
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
}

/// <summary>
/// 触发迁移请求DTO
/// </summary>
public class TriggerMigrationRequestDto
{
    /// <summary>
    /// 是否强制重新迁移（即使已迁移过也重新执行）
    /// </summary>
    public bool Force { get; set; }
}