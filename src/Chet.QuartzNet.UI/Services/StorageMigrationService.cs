using System.Diagnostics;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Migration;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Chet.QuartzNet.UI.Services;

/// <summary>
/// 存储数据迁移编排服务
/// 支持文件、MySQL、PostgreSQL、SQLite、SQL Server 之间的互转，负责预检、进度、取消与迁移后校验
/// </summary>
public class StorageMigrationService
{
    private static readonly Dictionary<MigrationEntity, (string Key, string Name)> EntityInfo =
        new()
        {
            [MigrationEntity.Jobs] = ("jobs", "作业数据"),
            [MigrationEntity.Logs] = ("logs", "作业日志"),
            [MigrationEntity.Settings] = ("settings", "系统设置"),
            [MigrationEntity.Notifications] = ("notifications", "通知消息"),
        };

    /// <summary>
    /// 迁移执行顺序：设置 → 作业 → 通知 → 日志
    /// </summary>
    private static readonly MigrationEntity[] ExecutionOrder =
    {
        MigrationEntity.Settings,
        MigrationEntity.Jobs,
        MigrationEntity.Notifications,
        MigrationEntity.Logs,
    };

    private static readonly MigrationEntity[] DefaultEntities =
    {
        MigrationEntity.Jobs,
        MigrationEntity.Logs,
        MigrationEntity.Settings,
        MigrationEntity.Notifications,
    };

    /// <summary>
    /// 附加数据库在配置档列表中的保留名称
    /// </summary>
    private const string AdditionalStoreProfileName = "additional";

    private readonly IOptions<QuartzUIOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageMigrationService> _logger;
    private readonly MigrationStoreFactory _storeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly object _lock = new();

    private DataMigrationStatusDto _status = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    /// <summary>
    /// 构造函数
    /// </summary>
    public StorageMigrationService(
        IOptions<QuartzUIOptions> options,
        IConfiguration configuration,
        ILogger<StorageMigrationService> logger,
        MigrationStoreFactory storeFactory,
        ISchedulerFactory schedulerFactory
    )
    {
        _options = options;
        _configuration = configuration;
        _logger = logger;
        _storeFactory = storeFactory;
        _schedulerFactory = schedulerFactory;
        _status = CreateStatus();
    }

    /// <summary>
    /// 获取当前迁移状态快照
    /// </summary>
    public DataMigrationStatusDto GetStatus()
    {
        lock (_lock)
        {
            return Snapshot(_status);
        }
    }

    /// <summary>
    /// 获取迁移可选项（不包含任何连接字符串）
    /// </summary>
    public MigrationOptionsDto GetOptions()
    {
        var options = _options.Value;
        var migration = options.Migration;
        var filePath = ResolveFullPath(options.FileStoragePath);
        var result = new MigrationOptionsDto
        {
            CurrentStorageType = options.StorageType.ToString(),
            CurrentDatabaseProvider =
                options.StorageType == StorageType.Database
                    ? options.DatabaseProvider.ToString()
                    : null,
            FileStoragePath = filePath,
            FileStoragePathExists = Directory.Exists(filePath),
            AllowReplace = migration.AllowReplace,
            AllowAdHocConnectionString = migration.AllowAdHocConnectionString,
            BatchSize = migration.BatchSize,
            MaxErrorSamples = migration.MaxErrorSamples,
        };

        // 只有当前存储确实为文件存储时才标记“当前”，避免与当前数据库混淆
        var isFileStorageCurrent = options.StorageType == StorageType.File;
        result.FileSources.Add(
            new MigrationFileSourceOptionDto
            {
                Name = isFileStorageCurrent ? "当前文件存储" : "文件存储",
                Path = filePath,
                DisplayPath = GetDisplayPath(filePath),
            }
        );
        foreach (var source in migration.FileSources)
        {
            if (string.IsNullOrWhiteSpace(source.Path))
            {
                continue;
            }

            var path = ResolveFullPath(source.Path);
            if (result.FileSources.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.FileSources.Add(
                new MigrationFileSourceOptionDto
                {
                    Name = string.IsNullOrWhiteSpace(source.Name) ? path : source.Name,
                    Path = path,
                    DisplayPath = GetDisplayPath(path),
                }
            );
        }

        if (!string.IsNullOrWhiteSpace(migration.AdditionalStore?.Provider))
        {
            result.Profiles.Add(
                new MigrationProfileOptionDto
                {
                    Name = AdditionalStoreProfileName,
                    Provider = migration.AdditionalStore.Provider,
                    HasConnectionString = !string.IsNullOrWhiteSpace(
                        migration.AdditionalStore.ConnectionString
                    ),
                    IsAdditional = true,
                }
            );
        }

        foreach (var pair in migration.Profiles)
        {
            result.Profiles.Add(
                new MigrationProfileOptionDto
                {
                    Name = pair.Key,
                    Provider = pair.Value.Provider,
                    HasConnectionString = !string.IsNullOrWhiteSpace(
                        pair.Value.ConnectionString
                    ),
                }
            );
        }

        foreach (var availability in _storeFactory.GetProviderAvailability())
        {
            result.Providers.Add(
                new MigrationProviderOptionDto
                {
                    Type = availability.Type.ToString(),
                    PackageName = availability.PackageName,
                    Installed = availability.Installed,
                }
            );
        }

        return result;
    }

    /// <summary>
    /// 迁移预检，返回各实体的源/目标数量与预计写入量
    /// </summary>
    public async Task<MigrationPreviewDto> PreviewAsync(
        TriggerMigrationRequestDto? request,
        CancellationToken cancellationToken = default
    )
    {
        var preview = new MigrationPreviewDto();
        lock (_lock)
        {
            if (_isRunning)
            {
                preview.CanRun = false;
                preview.Message = "迁移任务正在运行中，请稍后再试";
                return preview;
            }
        }

        ResolvedMigration resolved;
        try
        {
            resolved = Resolve(request);
        }
        catch (Exception ex)
        {
            preview.CanRun = false;
            preview.Message = ex.Message;
            return preview;
        }

        preview.Source = resolved.Source.GetDisplayName();
        preview.Target = resolved.Target.GetDisplayName();
        preview.Mode = resolved.Mode.ToString();

        IMigrationStore? source = null;
        IMigrationStore? target = null;
        try
        {
            source = _storeFactory.CreateStore(
                resolved.Source,
                false,
                resolved.Mode,
                resolved.AllowUpgradeSourceSchema
            );
            target = _storeFactory.CreateStore(
                resolved.Target,
                true,
                resolved.Mode,
                resolved.AllowUpgradeSourceSchema
            );

            await source.ProbeAsync(false, cancellationToken);
            await target.ProbeAsync(true, cancellationToken);

            foreach (var entity in resolved.Entities.OrderBy(e => (int)e))
            {
                var item = new MigrationPreviewEntityDto
                {
                    Entity = EntityInfo[entity].Key,
                    Name = EntityInfo[entity].Name,
                };

                try
                {
                    item.SourceCount = await source.CountAsync(entity, cancellationToken);
                }
                catch (Exception ex)
                {
                    preview.Warnings.Add(
                        $"读取源端{EntityInfo[entity].Name}数量失败：{ex.Message}"
                    );
                }

                try
                {
                    item.TargetCount = await target.CountAsync(entity, cancellationToken);
                }
                catch (Exception ex)
                {
                    preview.Warnings.Add(
                        $"读取目标端{EntityInfo[entity].Name}数量失败（目标库可能尚未初始化）：{ex.Message}"
                    );
                }

                var overlap = 0L;
                try
                {
                    overlap = await CountOverlapAsync(
                        source,
                        target,
                        entity,
                        item.SourceCount,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    preview.Warnings.Add(
                        $"预估{EntityInfo[entity].Name}重叠数量失败，将按全部写入估算：{ex.Message}"
                    );
                }

                switch (resolved.Mode)
                {
                    case MigrationWriteMode.Merge:
                        item.WillInsert = Math.Max(0, item.SourceCount - overlap);
                        item.WillSkip = overlap;
                        break;

                    case MigrationWriteMode.Overwrite:
                        item.WillInsert = Math.Max(0, item.SourceCount - overlap);
                        if (
                            entity == MigrationEntity.Jobs
                            || entity == MigrationEntity.Settings
                        )
                        {
                            item.WillUpdate = overlap;
                        }
                        else
                        {
                            item.WillSkip = overlap;
                        }
                        break;

                    default:
                        item.WillInsert = item.SourceCount;
                        break;
                }

                preview.Entities.Add(item);
            }

            if (resolved.Mode == MigrationWriteMode.Replace)
            {
                preview.Warnings.Add("Replace 模式会先清空目标中所选实体的数据，请确认备份后再执行");
            }

            preview.CanRun = true;
        }
        catch (Exception ex)
        {
            preview.CanRun = false;
            preview.Message = ex.Message;
        }
        finally
        {
            if (source != null)
            {
                await source.DisposeAsync();
            }
            if (target != null)
            {
                await target.DisposeAsync();
            }
        }

        return preview;
    }

    /// <summary>
    /// 触发迁移（后台执行）
    /// </summary>
    /// <returns>是否成功启动，失败时返回原因</returns>
    public (bool Started, string? Error) Start(TriggerMigrationRequestDto? request)
    {
        ResolvedMigration resolved;
        try
        {
            resolved = Resolve(request);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        if (!resolved.Force && GetStatus() is { IsCompleted: true, IsSuccess: true })
        {
            return (false, "迁移已完成，如需重新迁移请点击「强制重新迁移」按钮");
        }

        if (!TryEnter())
        {
            return (false, "迁移任务正在运行中，请稍后再试");
        }

        _ = Task.Run(() => RunResolvedAsync(resolved, CancellationToken.None));
        return (true, null);
    }

    /// <summary>
    /// 同步执行迁移，供后台服务或测试调用
    /// </summary>
    public async Task<bool> RunAsync(
        TriggerMigrationRequestDto? request,
        CancellationToken cancellationToken = default
    )
    {
        ResolvedMigration resolved;
        try
        {
            resolved = Resolve(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据迁移参数校验失败");
            return false;
        }

        if (!TryEnter())
        {
            _logger.LogWarning("已有迁移任务正在运行，本次迁移已跳过");
            return false;
        }

        await RunResolvedAsync(resolved, cancellationToken);
        return GetStatus().IsSuccess;
    }

    /// <summary>
    /// 取消正在运行的迁移任务
    /// </summary>
    public bool Cancel()
    {
        lock (_lock)
        {
            if (!_isRunning)
            {
                return false;
            }
            _cts?.Cancel();
            return true;
        }
    }

    private async Task RunResolvedAsync(
        ResolvedMigration resolved,
        CancellationToken externalToken
    )
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var status = CreateStatus();
        status.MigrationId = Guid.NewGuid().ToString("N");
        status.SourceDisplay = resolved.Source.GetDisplayName();
        status.TargetDisplay = resolved.Target.GetDisplayName();
        status.Mode = resolved.Mode.ToString();
        status.Stage = "Validate";
        status.IsRunning = true;
        status.StartTime = DateTime.Now;
        status.Total = 0;
        status.Processed = 0;

        lock (_lock)
        {
            _status = status;
            _cts = cts;
        }

        var stopwatch = Stopwatch.StartNew();
        IMigrationStore? source = null;
        IMigrationStore? target = null;
        IScheduler? scheduler = null;
        var counts = new Dictionary<MigrationEntity, long>();

        try
        {
            source = _storeFactory.CreateStore(
                resolved.Source,
                false,
                resolved.Mode,
                resolved.AllowUpgradeSourceSchema
            );
            target = _storeFactory.CreateStore(
                resolved.Target,
                true,
                resolved.Mode,
                resolved.AllowUpgradeSourceSchema
            );

            UpdateStage("Probe");
            await source.ProbeAsync(false, cts.Token);
            await target.ProbeAsync(true, cts.Token);

            foreach (var entity in resolved.Entities)
            {
                counts[entity] = await source.CountAsync(entity, cts.Token);
            }

            status.Total = counts.Values.Sum();
            foreach (var entity in resolved.Entities)
            {
                var step = GetStep(status, entity);
                step.TotalCount = counts[entity];
            }

            if (resolved.PauseScheduler)
            {
                try
                {
                    scheduler = await _schedulerFactory.GetScheduler(cts.Token);
                    await scheduler.Standby(cts.Token);
                    _logger.LogInformation("迁移期间已暂停调度器");
                }
                catch (Exception ex)
                {
                    AddWarning(status, "暂停调度器失败：" + ex.Message);
                    _logger.LogWarning(ex, "迁移期间暂停调度器失败");
                }
            }

            UpdateStage("Backup");
            await source.BackupAsync(cts.Token);

            UpdateStage("EnsureSchema");
            await target.EnsureTargetSchemaAsync(cts.Token);

            UpdateStage("Migrate");
            foreach (var entity in ExecutionOrder)
            {
                if (!resolved.Entities.Contains(entity))
                {
                    var skippedStep = GetStep(status, entity);
                    skippedStep.Status = MigrationStepStatus.Skipped;
                    continue;
                }

                await MigrateEntityAsync(
                    source,
                    target,
                    entity,
                    counts[entity],
                    resolved,
                    status,
                    cts.Token
                );
            }

            UpdateStage("Verify");
            await VerifyAsync(source, target, counts, resolved, status, cts.Token);

            status.Stage = "Done";
            status.IsSuccess = true;
            status.CurrentEntity = null;
            status.SuggestedConfig = BuildSuggestedConfig(resolved.Target);
            _logger.LogInformation(
                "数据迁移完成：{Source} -> {Target}，模式 {Mode}，耗时 {Duration}ms",
                status.SourceDisplay,
                status.TargetDisplay,
                status.Mode,
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (OperationCanceledException)
        {
            status.IsCancelled = true;
            status.Stage = "Cancelled";
            status.ErrorMessage = "迁移已取消";
            MarkRunningEntitiesFailed(status, "迁移已取消");
            _logger.LogWarning("数据迁移已取消");
        }
        catch (Exception ex)
        {
            status.Stage = "Failed";
            status.ErrorMessage = ex.Message;
            MarkRunningEntitiesFailed(status, ex.Message);
            _logger.LogError(ex, "数据迁移失败");
        }
        finally
        {
            if (scheduler != null)
            {
                try
                {
                    await scheduler.Start(CancellationToken.None);
                    _logger.LogInformation("迁移结束，调度器已恢复");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "迁移结束恢复调度器失败");
                }
            }

            if (source != null)
            {
                try
                {
                    await source.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "释放迁移源适配器失败");
                }
            }
            if (target != null)
            {
                try
                {
                    await target.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "释放迁移目标适配器失败");
                }
            }

            lock (_lock)
            {
                status.IsRunning = false;
                status.IsCompleted = true;
                status.EndTime = DateTime.Now;
                status.DurationMs = stopwatch.ElapsedMilliseconds;
                status.ProgressPercent = status.IsSuccess
                    ? 100
                    : status.ProgressPercent;
                status.CurrentEntity = null;
                _isRunning = false;
                _cts = null;
            }

            cts.Dispose();
        }
    }

    private async Task MigrateEntityAsync(
        IMigrationStore source,
        IMigrationStore target,
        MigrationEntity entity,
        long sourceCount,
        ResolvedMigration resolved,
        DataMigrationStatusDto status,
        CancellationToken cancellationToken
    )
    {
        var entityState = GetEntityState(status, entity);
        var step = GetStep(status, entity);
        var key = EntityInfo[entity].Key;

        entityState.SourceCount = sourceCount;
        entityState.Status = MigrationStepStatus.Running;
        step.Status = MigrationStepStatus.Running;
        step.StartTime = DateTime.Now;
        status.CurrentEntity = key;

        _logger.LogInformation(
            "开始迁移 {Entity}，源端共 {Count} 条，模式 {Mode}",
            EntityInfo[entity].Name,
            sourceCount,
            resolved.Mode
        );

        try
        {
            if (sourceCount == 0 && resolved.Mode != MigrationWriteMode.Replace)
            {
                entityState.Status = MigrationStepStatus.Skipped;
                step.Status = MigrationStepStatus.Skipped;
                step.EndTime = DateTime.Now;
                step.ErrorMessage = "源端无数据";
                return;
            }

            if (resolved.Mode == MigrationWriteMode.Replace)
            {
                var cleared = await target.ClearAsync(entity, cancellationToken);
                _logger.LogInformation(
                    "Replace 模式已清空目标 {Entity} {Count} 条",
                    EntityInfo[entity].Name,
                    cleared
                );
                step.SkippedCount = cleared;
            }

            await target.BeginWriteAsync(entity, resolved.Mode, cancellationToken);

            var offset = 0;
            long processed = 0;
            while (offset < sourceCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await ReadPageAsync(
                    source,
                    entity,
                    offset,
                    resolved.BatchSize,
                    cancellationToken
                );
                if (page.Count == 0)
                {
                    break;
                }

                try
                {
                    var outcome = await WritePageAsync(
                        target,
                        entity,
                        page,
                        resolved.Mode,
                        cancellationToken
                    );

                    entityState.Inserted += outcome.Inserted;
                    entityState.Updated += outcome.Updated;
                    entityState.Skipped += outcome.Skipped;
                    entityState.Failed += outcome.Failed;
                }
                catch (Exception ex) when (resolved.ContinueOnError)
                {
                    entityState.Failed += page.Count;
                    AddErrorSample(status, entity, $"offset:{offset}", ex.Message);
                    _logger.LogError(
                        ex,
                        "迁移 {Entity} 批次失败，已按配置继续，offset={Offset}",
                        EntityInfo[entity].Name,
                        offset
                    );
                }

                offset += page.Count;
                processed += page.Count;

                entityState.Processed = processed;
                step.MigratedCount = processed;
                step.SkippedCount = entityState.Skipped;
                step.UpdatedCount = entityState.Updated;
                step.FailedCount = entityState.Failed;

                status.Processed += page.Count;
                status.ProgressPercent = CalculatePercent(status);
            }

            await target.CompleteWriteAsync(entity, cancellationToken);

            entityState.Status =
                entityState.Failed > 0
                    ? MigrationStepStatus.Failed
                    : MigrationStepStatus.Completed;
            step.Status = entityState.Status;
            step.EndTime = DateTime.Now;
            if (entityState.Failed > 0)
            {
                step.ErrorMessage = $"存在 {entityState.Failed} 条写入失败，详见错误样本";
            }

            _logger.LogInformation(
                "完成迁移 {Entity}：插入 {Inserted}，更新 {Updated}，跳过 {Skipped}，失败 {Failed}",
                EntityInfo[entity].Name,
                entityState.Inserted,
                entityState.Updated,
                entityState.Skipped,
                entityState.Failed
            );
        }
        catch (OperationCanceledException)
        {
            entityState.Status = MigrationStepStatus.Failed;
            entityState.ErrorMessage = "迁移已取消";
            step.Status = MigrationStepStatus.Failed;
            step.EndTime = DateTime.Now;
            step.ErrorMessage = "迁移已取消";
            throw;
        }
        catch (Exception ex)
        {
            entityState.Status = MigrationStepStatus.Failed;
            entityState.ErrorMessage = ex.Message;
            step.Status = MigrationStepStatus.Failed;
            step.EndTime = DateTime.Now;
            step.ErrorMessage = ex.Message;
            AddErrorSample(status, entity, "entity", ex.Message);
            throw;
        }
    }

    private async Task VerifyAsync(
        IMigrationStore source,
        IMigrationStore target,
        IReadOnlyDictionary<MigrationEntity, long> counts,
        ResolvedMigration resolved,
        DataMigrationStatusDto status,
        CancellationToken cancellationToken
    )
    {
        foreach (var entity in resolved.Entities)
        {
            var sourceCount = counts[entity];
            if (sourceCount == 0)
            {
                continue;
            }

            var targetCount = await target.CountAsync(entity, cancellationToken);
            if (resolved.Mode == MigrationWriteMode.Replace)
            {
                if (targetCount != sourceCount)
                {
                    AddWarning(
                        status,
                        $"{EntityInfo[entity].Name}数量校验不一致：源 {sourceCount}，目标 {targetCount}"
                    );
                }
            }
            else if (targetCount < sourceCount)
            {
                AddWarning(
                    status,
                    $"{EntityInfo[entity].Name}数量可能不足：源 {sourceCount}，目标 {targetCount}（源端可能存在重复键）"
                );
            }

            var offsets = new List<int> { 0 };
            if (sourceCount > 1)
            {
                offsets.Add((int)(sourceCount / 2));
                offsets.Add((int)Math.Max(0, sourceCount - 20));
            }

            foreach (var offset in offsets.Distinct())
            {
                var keys = await ReadKeysAsync(
                    source,
                    entity,
                    offset,
                    20,
                    cancellationToken
                );
                if (keys.Count == 0)
                {
                    continue;
                }

                var missing = keys.Count - await target.CountExistingAsync(
                    entity,
                    keys,
                    cancellationToken
                );
                if (missing > 0)
                {
                    AddWarning(
                        status,
                        $"{EntityInfo[entity].Name}抽样校验发现 {missing} 条记录未写入目标（offset={offset}）"
                    );
                }
            }
        }
    }

    private async Task<long> CountOverlapAsync(
        IMigrationStore source,
        IMigrationStore target,
        MigrationEntity entity,
        long sourceCount,
        CancellationToken cancellationToken
    )
    {
        long overlap = 0;
        var offset = 0;
        const int pageSize = 2000;

        while (offset < sourceCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var keys = await ReadKeysAsync(source, entity, offset, pageSize, cancellationToken);
            if (keys.Count == 0)
            {
                break;
            }

            overlap += await target.CountExistingAsync(entity, keys, cancellationToken);
            offset += keys.Count;
        }

        return overlap;
    }

    private async Task<List<string>> ReadKeysAsync(
        IMigrationStore store,
        MigrationEntity entity,
        int offset,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        switch (entity)
        {
            case MigrationEntity.Jobs:
                return (await store.ReadJobsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Select(MigrationKeys.Job)
                    .ToList();
            case MigrationEntity.Logs:
                return (await store.ReadLogsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Select(MigrationKeys.Log)
                    .ToList();
            case MigrationEntity.Settings:
                return (await store.ReadSettingsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Select(MigrationKeys.Setting)
                    .ToList();
            case MigrationEntity.Notifications:
                return (await store.ReadNotificationsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Select(MigrationKeys.Notification)
                    .ToList();
            default:
                return new List<string>();
        }
    }

    private async Task<List<object>> ReadPageAsync(
        IMigrationStore store,
        MigrationEntity entity,
        int offset,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        switch (entity)
        {
            case MigrationEntity.Jobs:
                return (await store.ReadJobsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Cast<object>()
                    .ToList();
            case MigrationEntity.Logs:
                return (await store.ReadLogsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Cast<object>()
                    .ToList();
            case MigrationEntity.Settings:
                return (await store.ReadSettingsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Cast<object>()
                    .ToList();
            case MigrationEntity.Notifications:
                return (await store.ReadNotificationsAsync(offset, pageSize, cancellationToken))
                    .Items
                    .Cast<object>()
                    .ToList();
            default:
                return new List<object>();
        }
    }

    private async Task<WriteOutcome> WritePageAsync(
        IMigrationStore store,
        MigrationEntity entity,
        List<object> page,
        MigrationWriteMode mode,
        CancellationToken cancellationToken
    )
    {
        switch (entity)
        {
            case MigrationEntity.Jobs:
                return await store.WriteJobsAsync(
                    page.Cast<QuartzJobInfo>().ToList(),
                    mode,
                    cancellationToken
                );
            case MigrationEntity.Logs:
                return await store.WriteLogsAsync(
                    page.Cast<QuartzJobLog>().ToList(),
                    mode,
                    cancellationToken
                );
            case MigrationEntity.Settings:
                return await store.WriteSettingsAsync(
                    page.Cast<QuartzSetting>().ToList(),
                    mode,
                    cancellationToken
                );
            case MigrationEntity.Notifications:
                return await store.WriteNotificationsAsync(
                    page.Cast<QuartzNotification>().ToList(),
                    mode,
                    cancellationToken
                );
            default:
                return new WriteOutcome();
        }
    }

    private ResolvedMigration Resolve(TriggerMigrationRequestDto? request)
    {
        request ??= new TriggerMigrationRequestDto();
        var options = _options.Value;
        var migration = options.Migration;

        var source = ResolveSource(request.Source);
        var target = ResolveTarget(request.Target);

        if (source.Type == target.Type)
        {
            throw new InvalidOperationException(
                "源与目标的存储类型相同，无需迁移，请选择不同的源和目标"
            );
        }

        var entities =
            request.Entities is { Count: > 0 }
                ? request.Entities.Distinct().ToList()
                : DefaultEntities.ToList();

        if (request.Mode == MigrationWriteMode.Replace)
        {
            if (!migration.AllowReplace)
            {
                throw new InvalidOperationException(
                    "Replace 模式未启用，请在 QuartzUI:Migration:AllowReplace 中显式开启后再使用"
                );
            }
            if (!request.ConfirmReplace)
            {
                throw new InvalidOperationException("Replace 模式需要二次确认");
            }
        }

        return new ResolvedMigration(
            source,
            target,
            entities,
            request.Mode,
            request.ContinueOnError ?? migration.ContinueOnError,
            request.PauseScheduler || migration.PauseSchedulerDuringMigration,
            request.AllowUpgradeSourceSchema,
            request.Force,
            Math.Clamp(migration.BatchSize, 1, 2000),
            Math.Clamp(migration.MaxErrorSamples, 1, 200)
        );
    }

    private MigrationStoreDescriptor ResolveSource(MigrationStoreDescriptor? descriptor)
    {
        var options = _options.Value;
        if (descriptor == null || descriptor.Type == MigrationStoreType.File)
        {
            var path = ResolveFilePath(descriptor?.FilePath, options);
            return new MigrationStoreDescriptor
            {
                Type = MigrationStoreType.File,
                FilePath = path,
            };
        }

        return ResolveDatabase(descriptor);
    }

    private MigrationStoreDescriptor ResolveTarget(MigrationStoreDescriptor? descriptor)
    {
        var options = _options.Value;
        if (descriptor == null)
        {
            if (options.StorageType != StorageType.Database)
            {
                throw new InvalidOperationException(
                    "当前应用使用文件存储，请在请求中显式指定目标数据库配置档"
                );
            }

            var connectionString = _configuration.GetConnectionString("QuartzUI");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("未找到 QuartzUI 数据库连接字符串配置");
            }

            return new MigrationStoreDescriptor
            {
                Type = MapProvider(options.DatabaseProvider),
                ConnectionString = connectionString,
                ProfileName = "current",
            };
        }

        if (descriptor.Type == MigrationStoreType.File)
        {
            return new MigrationStoreDescriptor
            {
                Type = MigrationStoreType.File,
                FilePath = ResolveFilePath(descriptor.FilePath, options),
            };
        }

        return ResolveDatabase(descriptor);
    }

    private MigrationStoreDescriptor ResolveDatabase(MigrationStoreDescriptor descriptor)
    {
        var options = _options.Value;
        var migration = options.Migration;

        if (descriptor.ProfileName == "current")
        {
            if (options.StorageType != StorageType.Database)
            {
                throw new InvalidOperationException(
                    "当前应用使用文件存储，不存在可用的当前数据库，请选择迁移配置档"
                );
            }

            var currentConnectionString = _configuration.GetConnectionString("QuartzUI");
            if (string.IsNullOrWhiteSpace(currentConnectionString))
            {
                throw new InvalidOperationException("未找到 QuartzUI 数据库连接字符串配置");
            }

            return new MigrationStoreDescriptor
            {
                Type = MapProvider(options.DatabaseProvider),
                ConnectionString = currentConnectionString,
                ServerVersion = descriptor.ServerVersion,
                ProfileName = "current",
            };
        }

        var availability = _storeFactory
            .GetProviderAvailability()
            .FirstOrDefault(p => p.Type == descriptor.Type);
        if (availability is { Installed: false })
        {
            throw new InvalidOperationException(
                $"未安装 {availability.PackageName} 包，无法使用 {descriptor.Type} 存储进行迁移"
            );
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ProfileName))
        {
            var isAdditionalStore = descriptor.ProfileName.Equals(
                AdditionalStoreProfileName,
                StringComparison.OrdinalIgnoreCase
            );
            var profile = isAdditionalStore
                ? migration.AdditionalStore
                : migration.Profiles.GetValueOrDefault(descriptor.ProfileName);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    isAdditionalStore
                        ? "未配置 QuartzUI:Migration:AdditionalStore"
                        : $"未找到迁移配置档：{descriptor.ProfileName}"
                );
            }
            if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            {
                throw new InvalidOperationException(
                    isAdditionalStore
                        ? "QuartzUI:Migration:AdditionalStore 未配置连接字符串"
                        : $"迁移配置档 {descriptor.ProfileName} 未配置连接字符串"
                );
            }

            var provider = string.IsNullOrWhiteSpace(profile.Provider)
                ? descriptor.Type
                : ParseStoreType(profile.Provider);
            if (provider != descriptor.Type && descriptor.Type != MigrationStoreType.File)
            {
                throw new InvalidOperationException(
                    isAdditionalStore
                        ? $"QuartzUI:Migration:AdditionalStore 的数据库类型为 {provider}，与请求中的 {descriptor.Type} 不一致"
                        : $"配置档 {descriptor.ProfileName} 的数据库类型为 {provider}，与请求中的 {descriptor.Type} 不一致"
                );
            }

            return new MigrationStoreDescriptor
            {
                Type = provider,
                ConnectionString = profile.ConnectionString,
                ServerVersion = profile.ServerVersion ?? descriptor.ServerVersion,
                ProfileName = isAdditionalStore ? "附加数据库" : descriptor.ProfileName,
            };
        }

        if (!string.IsNullOrWhiteSpace(descriptor.ConnectionString))
        {
            if (!migration.AllowAdHocConnectionString)
            {
                throw new InvalidOperationException(
                    "服务端未允许直接提交连接字符串，请配置 QuartzUI:Migration:AdditionalStore"
                );
            }

            return descriptor;
        }

        throw new InvalidOperationException(
            $"{descriptor.Type} 存储需要在 QuartzUI:Migration:AdditionalStore 中配置，或在允许的情况下直接提交连接字符串"
        );
    }

    private string ResolveFilePath(string? requestedPath, QuartzUIOptions options)
    {
        var defaultPath = ResolveFullPath(options.FileStoragePath);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return defaultPath;
        }

        var path = ResolveFullPath(requestedPath);
        var allowed = new List<string> { defaultPath };
        allowed.AddRange(
            options
                .Migration.FileSources.Where(f => !string.IsNullOrWhiteSpace(f.Path))
                .Select(f => ResolveFullPath(f.Path))
        );

        if (
            !allowed.Exists(p =>
                string.Equals(p, path, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            throw new InvalidOperationException(
                $"文件路径不在服务端允许列表中：{path}，请通过 QuartzUI:Migration:FileSources 配置"
            );
        }

        return path;
    }

    private static string ResolveFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>
    /// 获取展示用路径：在工作目录内时使用相对路径，否则回退为完整路径
    /// </summary>
    private static string GetDisplayPath(string fullPath)
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

    private static MigrationStoreType ParseStoreType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "file" => MigrationStoreType.File,
            "mysql" => MigrationStoreType.MySql,
            "postgresql" or "postgres" or "npgsql" => MigrationStoreType.PostgreSql,
            "sqlite" => MigrationStoreType.SQLite,
            "sqlserver" or "mssql" => MigrationStoreType.SqlServer,
            _ => throw new InvalidOperationException($"不支持的存储类型：{value}"),
        };
    }

    private static MigrationStoreType MapProvider(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.MySql => MigrationStoreType.MySql,
            DatabaseProvider.PostgreSql => MigrationStoreType.PostgreSql,
            DatabaseProvider.SQLite => MigrationStoreType.SQLite,
            DatabaseProvider.SqlServer => MigrationStoreType.SqlServer,
            _ => throw new InvalidOperationException($"不支持的数据库类型：{provider}"),
        };

    private bool TryEnter()
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                return false;
            }
            _isRunning = true;
            return true;
        }
    }

    private DataMigrationStatusDto CreateStatus()
    {
        var options = _options.Value;
        var status = new DataMigrationStatusDto
        {
            FileStoragePath = options.FileStoragePath,
            FileStoragePathExists =
                !string.IsNullOrWhiteSpace(options.FileStoragePath)
                && Directory.Exists(ResolveFullPath(options.FileStoragePath)),
            StorageType = options.StorageType.ToString(),
            Stage = "Idle",
            Steps = new List<MigrationStepInfo>
            {
                new() { Name = "作业数据", Key = "jobs" },
                new() { Name = "作业日志", Key = "logs" },
                new() { Name = "系统设置", Key = "settings" },
                new() { Name = "通知消息", Key = "notifications" },
            },
            Entities = DefaultEntities
                .Select(e => new MigrationEntityState
                {
                    Entity = EntityInfo[e].Key,
                    Name = EntityInfo[e].Name,
                })
                .ToList(),
        };

        return status;
    }

    private void UpdateStage(string stage)
    {
        lock (_lock)
        {
            _status.Stage = stage;
            _status.CurrentStep = stage switch
            {
                "Validate" => "参数校验",
                "Probe" => "连通性与结构预检",
                "Backup" => "备份源数据",
                "EnsureSchema" => "初始化目标结构",
                "Migrate" => "迁移数据",
                "Verify" => "迁移后校验",
                "Done" => "迁移完成",
                "Failed" => "迁移失败",
                "Cancelled" => "迁移已取消",
                _ => stage,
            };
        }
    }

    private static MigrationStepInfo GetStep(
        DataMigrationStatusDto status,
        MigrationEntity entity
    )
    {
        var key = EntityInfo[entity].Key;
        return status.Steps.First(s => s.Key == key);
    }

    private static MigrationEntityState GetEntityState(
        DataMigrationStatusDto status,
        MigrationEntity entity
    )
    {
        var key = EntityInfo[entity].Key;
        return status.Entities.First(e => e.Entity == key);
    }

    private void AddWarning(DataMigrationStatusDto status, string message)
    {
        lock (_lock)
        {
            status.Warnings.Add(message);
        }
    }

    private void AddErrorSample(
        DataMigrationStatusDto status,
        MigrationEntity entity,
        string key,
        string message
    )
    {
        var maxSamples = Math.Clamp(_options.Value.Migration.MaxErrorSamples, 1, 200);
        lock (_lock)
        {
            if (status.ErrorSamples.Count >= maxSamples)
            {
                return;
            }

            status.ErrorSamples.Add(
                new MigrationRowError
                {
                    Entity = EntityInfo[entity].Key,
                    Key = key,
                    Message = message,
                }
            );
        }
    }

    private static DataMigrationStatusDto Snapshot(DataMigrationStatusDto status)
    {
        return new DataMigrationStatusDto
        {
            IsRunning = status.IsRunning,
            ProgressPercent = status.ProgressPercent,
            CurrentStep = status.CurrentStep,
            StartTime = status.StartTime,
            EndTime = status.EndTime,
            DurationMs = status.DurationMs,
            IsCompleted = status.IsCompleted,
            IsSuccess = status.IsSuccess,
            ErrorMessage = status.ErrorMessage,
            FileStoragePath = status.FileStoragePath,
            FileStoragePathExists = status.FileStoragePathExists,
            StorageType = status.StorageType,
            MigrationId = status.MigrationId,
            SourceDisplay = status.SourceDisplay,
            TargetDisplay = status.TargetDisplay,
            Mode = status.Mode,
            Stage = status.Stage,
            CurrentEntity = status.CurrentEntity,
            Processed = status.Processed,
            Total = status.Total,
            IsCancelled = status.IsCancelled,
            SuggestedConfig = status.SuggestedConfig,
            Steps = status.Steps
                .Select(step => new MigrationStepInfo
                {
                    Name = step.Name,
                    Key = step.Key,
                    Status = step.Status,
                    TotalCount = step.TotalCount,
                    MigratedCount = step.MigratedCount,
                    SkippedCount = step.SkippedCount,
                    UpdatedCount = step.UpdatedCount,
                    FailedCount = step.FailedCount,
                    ErrorMessage = step.ErrorMessage,
                    StartTime = step.StartTime,
                    EndTime = step.EndTime,
                })
                .ToList(),
            Entities = status.Entities
                .Select(entity => new MigrationEntityState
                {
                    Entity = entity.Entity,
                    Name = entity.Name,
                    Status = entity.Status,
                    SourceCount = entity.SourceCount,
                    Processed = entity.Processed,
                    Inserted = entity.Inserted,
                    Updated = entity.Updated,
                    Skipped = entity.Skipped,
                    Failed = entity.Failed,
                    ErrorMessage = entity.ErrorMessage,
                })
                .ToList(),
            ErrorSamples = status.ErrorSamples
                .Select(error => new MigrationRowError
                {
                    Entity = error.Entity,
                    Key = error.Key,
                    Field = error.Field,
                    Message = error.Message,
                })
                .ToList(),
            Warnings = status.Warnings.ToList(),
        };
    }

    private static int CalculatePercent(DataMigrationStatusDto status)
    {
        if (status.Total <= 0)
        {
            return status.IsSuccess ? 100 : 0;
        }

        return (int)Math.Min(100, status.Processed * 100 / status.Total);
    }

    private static void MarkRunningEntitiesFailed(DataMigrationStatusDto status, string message)
    {
        foreach (var entity in status.Entities)
        {
            if (entity.Status == MigrationStepStatus.Running)
            {
                entity.Status = MigrationStepStatus.Failed;
                entity.ErrorMessage = message;
            }
        }

        foreach (var step in status.Steps)
        {
            if (step.Status == MigrationStepStatus.Running)
            {
                step.Status = MigrationStepStatus.Failed;
                step.EndTime = DateTime.Now;
                step.ErrorMessage = message;
            }
        }
    }

    private static string BuildSuggestedConfig(MigrationStoreDescriptor target)
    {
        if (target.Type == MigrationStoreType.File)
        {
            return "{\n  \"QuartzUI\": {\n    \"StorageType\": \"File\",\n    \"FileStoragePath\": \""
                + (target.FilePath ?? string.Empty).Replace("\\", "\\\\")
                + "\"\n  }\n}";
        }

        return "{\n  \"QuartzUI\": {\n    \"StorageType\": \"Database\",\n    \"DatabaseProvider\": \""
            + target.Type
            + "\"\n  },\n  \"ConnectionStrings\": {\n    \"QuartzUI\": \"<目标连接字符串>\"\n  }\n}";
    }

    private sealed record ResolvedMigration(
        MigrationStoreDescriptor Source,
        MigrationStoreDescriptor Target,
        IReadOnlyList<MigrationEntity> Entities,
        MigrationWriteMode Mode,
        bool ContinueOnError,
        bool PauseScheduler,
        bool AllowUpgradeSourceSchema,
        bool Force,
        int BatchSize,
        int MaxErrorSamples
    );
}
