using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

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
