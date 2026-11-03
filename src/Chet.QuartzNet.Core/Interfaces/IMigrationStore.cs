using Chet.QuartzNet.Core.Migration;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Core.Interfaces;

/// <summary>
/// 迁移存储适配器接口，文件与数据库均实现该接口
/// </summary>
public interface IMigrationStore : IAsyncDisposable
{
    /// <summary>
    /// 存储描述
    /// </summary>
    MigrationStoreDescriptor Descriptor { get; }

    /// <summary>
    /// 连通性、版本与结构预检，失败时抛出异常
    /// </summary>
    /// <param name="asTarget">是否作为迁移目标进行预检</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProbeAsync(bool asTarget, CancellationToken cancellationToken = default);

    /// <summary>
    /// 迁移前备份（文件存储复制目录，数据库不执行自动备份）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task BackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保目标结构存在（文件创建目录，数据库应用迁移）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnsureTargetSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计指定实体的记录数
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> CountAsync(MigrationEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取作业分页
    /// </summary>
    Task<MigrationPage<QuartzJobInfo>> ReadJobsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 读取日志分页
    /// </summary>
    Task<MigrationPage<QuartzJobLog>> ReadLogsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 读取设置分页
    /// </summary>
    Task<MigrationPage<QuartzSetting>> ReadSettingsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 读取通知分页
    /// </summary>
    Task<MigrationPage<QuartzNotification>> ReadNotificationsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 开始写入某个实体（文件存储在此打开临时文件）
    /// </summary>
    Task BeginWriteAsync(
        MigrationEntity entity,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 写入作业
    /// </summary>
    Task<WriteOutcome> WriteJobsAsync(
        IReadOnlyList<QuartzJobInfo> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 写入日志
    /// </summary>
    Task<WriteOutcome> WriteLogsAsync(
        IReadOnlyList<QuartzJobLog> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 写入设置
    /// </summary>
    Task<WriteOutcome> WriteSettingsAsync(
        IReadOnlyList<QuartzSetting> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 写入通知
    /// </summary>
    Task<WriteOutcome> WriteNotificationsAsync(
        IReadOnlyList<QuartzNotification> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 完成某个实体的写入（文件存储在此提交临时文件）
    /// </summary>
    Task CompleteWriteAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清空指定实体的数据
    /// </summary>
    /// <returns>清空的记录数</returns>
    Task<long> ClearAsync(MigrationEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计给定业务键中已存在于当前存储的数量（用于预检与迁移后校验）
    /// </summary>
    Task<long> CountExistingAsync(
        MigrationEntity entity,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default
    );
}
