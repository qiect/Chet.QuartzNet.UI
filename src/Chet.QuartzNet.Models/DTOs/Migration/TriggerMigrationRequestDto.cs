using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

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
