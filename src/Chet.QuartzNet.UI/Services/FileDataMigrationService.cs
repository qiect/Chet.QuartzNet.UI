using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Services;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chet.QuartzNet.UI.Services;

/// <summary>
/// 文件数据迁移服务，支持按需触发和进度追踪
/// 将 FileJobStorage 中的数据（作业、日志、设置、通知）迁移到数据库存储
/// </summary>
public class FileDataMigrationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileDataMigrationService> _logger;
    private readonly IOptions<QuartzUIOptions> _options;

    private readonly object _lock = new();
    private bool _isRunning;
    private DataMigrationStatusDto _status = new();

    public FileDataMigrationService(
        IServiceScopeFactory scopeFactory,
        ILogger<FileDataMigrationService> logger,
        IOptions<QuartzUIOptions> options
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        InitializeStatus();
    }

    private void InitializeStatus()
    {
        var options = _options.Value;
        _status = new DataMigrationStatusDto
        {
            FileStoragePath = options.FileStoragePath,
            FileStoragePathExists =
                !string.IsNullOrWhiteSpace(options.FileStoragePath)
                && Directory.Exists(options.FileStoragePath),
            StorageType = options.StorageType.ToString(),
            Steps = new List<MigrationStepInfo>
            {
                new()
                {
                    Name = "作业数据",
                    Key = "jobs",
                    Status = MigrationStepStatus.Pending,
                },
                new()
                {
                    Name = "作业日志",
                    Key = "logs",
                    Status = MigrationStepStatus.Pending,
                },
                new()
                {
                    Name = "系统设置",
                    Key = "settings",
                    Status = MigrationStepStatus.Pending,
                },
                new()
                {
                    Name = "通知消息",
                    Key = "notifications",
                    Status = MigrationStepStatus.Pending,
                },
            },
        };
    }

    /// <summary>
    /// 获取当前迁移状态
    /// </summary>
    public DataMigrationStatusDto GetStatus()
    {
        lock (_lock)
        {
            var options = _options.Value;
            _status.FileStoragePath = options.FileStoragePath;
            _status.FileStoragePathExists =
                !string.IsNullOrWhiteSpace(options.FileStoragePath)
                && Directory.Exists(options.FileStoragePath);
            _status.StorageType = options.StorageType.ToString();
            return _status;
        }
    }

    /// <summary>
    /// 触发迁移（异步执行，立即返回）
    /// </summary>
    /// <param name="force">是否强制重新迁移</param>
    /// <returns>是否成功触发</returns>
    public bool TriggerMigration(bool force = false)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                return false;
            }

            if (!force && _status.IsCompleted && _status.IsSuccess)
            {
                return false;
            }

            _isRunning = true;
            InitializeStatus();
            _status.IsRunning = true;
            _status.IsCompleted = false;
            _status.IsSuccess = false;
            _status.ErrorMessage = null;
            _status.StartTime = DateTime.Now;
            _status.EndTime = null;
            _status.DurationMs = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteMigrationAsync();
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _status.IsRunning = false;
                    _status.IsCompleted = true;
                    _status.IsSuccess = false;
                    _status.ErrorMessage = ex.Message;
                    _status.EndTime = DateTime.Now;
                    _status.DurationMs = (long)
                        (_status.EndTime.Value - _status.StartTime!).Value.TotalMilliseconds;
                    _isRunning = false;
                }

                _logger.LogFailure("文件数据迁移", ex);
            }
        });

        return true;
    }

    private async Task ExecuteMigrationAsync()
    {
        var options = _options.Value;

        if (
            string.IsNullOrWhiteSpace(options.FileStoragePath)
            || !Directory.Exists(options.FileStoragePath)
        )
        {
            lock (_lock)
            {
                _status.IsRunning = false;
                _status.IsCompleted = true;
                _status.IsSuccess = false;
                _status.ErrorMessage = "文件存储路径不存在";
                _status.EndTime = DateTime.Now;
                _status.DurationMs = (long)
                    (_status.EndTime.Value - _status.StartTime!).Value.TotalMilliseconds;
                _isRunning = false;
            }

            _logger.LogInfo(
                "文件数据迁移",
                "文件存储路径不存在，跳过迁移: {Path}",
                options.FileStoragePath
            );
            return;
        }

        if (options.StorageType != StorageType.Database)
        {
            lock (_lock)
            {
                _status.IsRunning = false;
                _status.IsCompleted = true;
                _status.IsSuccess = false;
                _status.ErrorMessage = "当前存储类型不是数据库，请将 StorageType 设置为 Database";
                _status.EndTime = DateTime.Now;
                _status.DurationMs = (long)
                    (_status.EndTime.Value - _status.StartTime!).Value.TotalMilliseconds;
                _isRunning = false;
            }

            _logger.LogWarn(
                "文件数据迁移",
                "当前存储类型不是数据库，跳过迁移。请将 StorageType 设置为 Database 后重试"
            );
            return;
        }

        _logger.LogInfoStructured("开始文件数据迁移到数据库");

        var fileStorageLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var fileStorageLogger = fileStorageLoggerFactory.CreateLogger<FileJobStorage>();
        var optionsWrapper = new OptionsWrapper<QuartzUIOptions>(options);
        var fileStorage = new FileJobStorage(optionsWrapper, fileStorageLogger);

        using var scope = _scopeFactory.CreateScope();
        var dbStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

        var initialized = await dbStorage.InitializeAsync();
        if (!initialized)
        {
            lock (_lock)
            {
                _status.IsRunning = false;
                _status.IsCompleted = true;
                _status.IsSuccess = false;
                _status.ErrorMessage = "数据库初始化失败";
                _status.EndTime = DateTime.Now;
                _status.DurationMs = (long)
                    (_status.EndTime.Value - _status.StartTime!).Value.TotalMilliseconds;
                _isRunning = false;
            }

            _logger.LogFailure("文件数据迁移", "数据库初始化失败，终止迁移");
            return;
        }

        await MigrateJobsAsync(fileStorage, dbStorage);
        await MigrateLogsAsync(fileStorage, dbStorage);
        await MigrateSettingsAsync(fileStorage, dbStorage);
        await MigrateNotificationsAsync(fileStorage, dbStorage);

        lock (_lock)
        {
            _status.IsRunning = false;
            _status.IsCompleted = true;
            _status.IsSuccess = true;
            _status.CurrentStep = "迁移完成";
            _status.EndTime = DateTime.Now;
            _status.DurationMs = (long)
                (_status.EndTime.Value - _status.StartTime!).Value.TotalMilliseconds;
            _isRunning = false;

            var completedSteps = _status.Steps.Count(s =>
                s.Status == MigrationStepStatus.Completed || s.Status == MigrationStepStatus.Skipped
            );
            _status.ProgressPercent = (int)
                Math.Round((double)completedSteps / _status.Steps.Count * 100);
        }

        _logger.LogSuccess("文件数据迁移", "文件数据迁移到数据库完成");
    }

    private async Task MigrateJobsAsync(FileJobStorage fileStorage, IJobStorage dbStorage)
    {
        var stepIndex = 0;
        lock (_lock)
        {
            _status.CurrentStep = "正在迁移作业数据...";
            _status.Steps[stepIndex].Status = MigrationStepStatus.Running;
            _status.Steps[stepIndex].StartTime = DateTime.Now;
            UpdateProgress();
        }

        try
        {
            var jobs = await fileStorage.GetAllJobsAsync();

            lock (_lock)
            {
                _status.Steps[stepIndex].TotalCount = jobs.Count;
            }

            if (jobs.Count == 0)
            {
                lock (_lock)
                {
                    _status.Steps[stepIndex].Status = MigrationStepStatus.Skipped;
                    _status.Steps[stepIndex].EndTime = DateTime.Now;
                    UpdateProgress();
                }

                _logger.LogInfo("文件数据迁移", "没有作业数据需要迁移");
                return;
            }

            _logger.LogInfo("文件数据迁移", "找到 {Count} 条作业数据", jobs.Count);
            var migratedCount = await dbStorage.AddJobsBatchAsync(jobs);

            lock (_lock)
            {
                _status.Steps[stepIndex].MigratedCount = migratedCount;
                _status.Steps[stepIndex].SkippedCount = jobs.Count - migratedCount;
                _status.Steps[stepIndex].Status = MigrationStepStatus.Completed;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogInfo(
                "文件数据迁移",
                "作业迁移完成: 成功 {Migrated}, 跳过 {Skipped}, 共 {Total}",
                migratedCount,
                jobs.Count - migratedCount,
                jobs.Count
            );
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _status.Steps[stepIndex].Status = MigrationStepStatus.Failed;
                _status.Steps[stepIndex].ErrorMessage = ex.Message;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogFailure("迁移作业数据", ex);
        }
    }

    private async Task MigrateLogsAsync(FileJobStorage fileStorage, IJobStorage dbStorage)
    {
        var stepIndex = 1;
        lock (_lock)
        {
            _status.CurrentStep = "正在迁移作业日志...";
            _status.Steps[stepIndex].Status = MigrationStepStatus.Running;
            _status.Steps[stepIndex].StartTime = DateTime.Now;
            UpdateProgress();
        }

        try
        {
            var allLogs = new List<QuartzJobLog>();
            var pageIndex = 1;
            const int pageSize = 500;

            while (true)
            {
                var queryDto = new QuartzJobLogQueryDto
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                };
                var pagedResult = await fileStorage.GetJobLogsAsync(queryDto);
                if (pagedResult.Items.Count == 0)
                    break;

                allLogs.AddRange(pagedResult.Items);

                if (pagedResult.Items.Count < pageSize)
                    break;

                pageIndex++;
            }

            lock (_lock)
            {
                _status.Steps[stepIndex].TotalCount = allLogs.Count;
            }

            if (allLogs.Count == 0)
            {
                lock (_lock)
                {
                    _status.Steps[stepIndex].Status = MigrationStepStatus.Skipped;
                    _status.Steps[stepIndex].EndTime = DateTime.Now;
                    UpdateProgress();
                }

                _logger.LogInfo("文件数据迁移", "没有作业日志需要迁移");
                return;
            }

            _logger.LogInfo("文件数据迁移", "找到 {Count} 条作业日志", allLogs.Count);
            var migratedCount = await dbStorage.AddJobLogsBatchAsync(allLogs);

            lock (_lock)
            {
                _status.Steps[stepIndex].MigratedCount = migratedCount;
                _status.Steps[stepIndex].SkippedCount = allLogs.Count - migratedCount;
                _status.Steps[stepIndex].Status = MigrationStepStatus.Completed;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogInfo(
                "文件数据迁移",
                "日志迁移完成: 成功 {Migrated}, 跳过 {Skipped}, 共 {Total}",
                migratedCount,
                allLogs.Count - migratedCount,
                allLogs.Count
            );
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _status.Steps[stepIndex].Status = MigrationStepStatus.Failed;
                _status.Steps[stepIndex].ErrorMessage = ex.Message;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogFailure("迁移作业日志", ex);
        }
    }

    private async Task MigrateSettingsAsync(FileJobStorage fileStorage, IJobStorage dbStorage)
    {
        var stepIndex = 2;
        lock (_lock)
        {
            _status.CurrentStep = "正在迁移系统设置...";
            _status.Steps[stepIndex].Status = MigrationStepStatus.Running;
            _status.Steps[stepIndex].StartTime = DateTime.Now;
            UpdateProgress();
        }

        try
        {
            var settings = await fileStorage.GetAllSettingsAsync();

            lock (_lock)
            {
                _status.Steps[stepIndex].TotalCount = settings.Count;
            }

            if (settings.Count == 0)
            {
                lock (_lock)
                {
                    _status.Steps[stepIndex].Status = MigrationStepStatus.Skipped;
                    _status.Steps[stepIndex].EndTime = DateTime.Now;
                    UpdateProgress();
                }

                _logger.LogInfo("文件数据迁移", "没有系统设置需要迁移");
                return;
            }

            _logger.LogInfo("文件数据迁移", "找到 {Count} 条系统设置", settings.Count);
            var migratedCount = await dbStorage.SaveSettingsBatchAsync(settings);

            lock (_lock)
            {
                _status.Steps[stepIndex].MigratedCount = migratedCount;
                _status.Steps[stepIndex].SkippedCount = settings.Count - migratedCount;
                _status.Steps[stepIndex].Status = MigrationStepStatus.Completed;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogInfo(
                "文件数据迁移",
                "设置迁移完成: 成功 {Migrated}, 共 {Total}",
                migratedCount,
                settings.Count
            );
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _status.Steps[stepIndex].Status = MigrationStepStatus.Failed;
                _status.Steps[stepIndex].ErrorMessage = ex.Message;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogFailure("迁移系统设置", ex);
        }
    }

    private async Task MigrateNotificationsAsync(FileJobStorage fileStorage, IJobStorage dbStorage)
    {
        var stepIndex = 3;
        lock (_lock)
        {
            _status.CurrentStep = "正在迁移通知消息...";
            _status.Steps[stepIndex].Status = MigrationStepStatus.Running;
            _status.Steps[stepIndex].StartTime = DateTime.Now;
            UpdateProgress();
        }

        try
        {
            var allNotifications = new List<QuartzNotification>();
            var pageIndex = 1;
            const int pageSize = 500;

            while (true)
            {
                var queryDto = new NotificationQueryDto
                {
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                };
                var pagedResult = await fileStorage.GetNotificationsAsync(queryDto);
                if (pagedResult.Items.Count == 0)
                    break;

                allNotifications.AddRange(pagedResult.Items);

                if (pagedResult.Items.Count < pageSize)
                    break;

                pageIndex++;
            }

            lock (_lock)
            {
                _status.Steps[stepIndex].TotalCount = allNotifications.Count;
            }

            if (allNotifications.Count == 0)
            {
                lock (_lock)
                {
                    _status.Steps[stepIndex].Status = MigrationStepStatus.Skipped;
                    _status.Steps[stepIndex].EndTime = DateTime.Now;
                    UpdateProgress();
                }

                _logger.LogInfo("文件数据迁移", "没有通知消息需要迁移");
                return;
            }

            _logger.LogInfo("文件数据迁移", "找到 {Count} 条通知消息", allNotifications.Count);
            var migratedCount = await dbStorage.AddNotificationsBatchAsync(allNotifications);

            lock (_lock)
            {
                _status.Steps[stepIndex].MigratedCount = migratedCount;
                _status.Steps[stepIndex].SkippedCount = allNotifications.Count - migratedCount;
                _status.Steps[stepIndex].Status = MigrationStepStatus.Completed;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogInfo(
                "文件数据迁移",
                "通知迁移完成: 成功 {Migrated}, 跳过 {Skipped}, 共 {Total}",
                migratedCount,
                allNotifications.Count - migratedCount,
                allNotifications.Count
            );
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _status.Steps[stepIndex].Status = MigrationStepStatus.Failed;
                _status.Steps[stepIndex].ErrorMessage = ex.Message;
                _status.Steps[stepIndex].EndTime = DateTime.Now;
                UpdateProgress();
            }

            _logger.LogFailure("迁移通知消息", ex);
        }
    }

    private void UpdateProgress()
    {
        var completedSteps = _status.Steps.Count(s =>
            s.Status == MigrationStepStatus.Completed
            || s.Status == MigrationStepStatus.Skipped
            || s.Status == MigrationStepStatus.Failed
        );
        _status.ProgressPercent = (int)
            Math.Round((double)completedSteps / _status.Steps.Count * 100);
    }
}
