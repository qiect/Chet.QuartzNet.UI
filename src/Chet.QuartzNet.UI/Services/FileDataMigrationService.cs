using Chet.QuartzNet.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Services;

/// <summary>
/// 文件数据迁移兼容服务
/// 保留原有 API，内部委托给 <see cref="StorageMigrationService"/>，默认执行文件存储到当前数据库的合并迁移
/// </summary>
public class FileDataMigrationService
{
    private readonly StorageMigrationService _migrationService;
    private readonly ILogger<FileDataMigrationService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileDataMigrationService(
        StorageMigrationService migrationService,
        ILogger<FileDataMigrationService> logger
    )
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前迁移状态
    /// </summary>
    public DataMigrationStatusDto GetStatus() => _migrationService.GetStatus();

    /// <summary>
    /// 触发迁移（异步执行，立即返回）
    /// </summary>
    /// <param name="force">是否强制重新迁移</param>
    /// <returns>是否成功触发</returns>
    public bool TriggerMigration(bool force = false)
    {
        var result = _migrationService.Start(
            new TriggerMigrationRequestDto { Force = force }
        );

        if (!result.Started && result.Error != null)
        {
            _logger.LogWarning("触发文件数据迁移失败：{Reason}", result.Error);
        }

        return result.Started;
    }
}
