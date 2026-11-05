using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Migration;
using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.EFCore.MigrationSupport;

/// <summary>
/// EF Core 数据库迁移适配器
/// 源端按页读取，目标端按批写入，写入过程不重建主键，保证重复迁移幂等
/// </summary>
public sealed class EfCoreMigrationStore : IMigrationStore
{
    private const int ClearChunkSize = 5000;

    private readonly QuartzDbContext _context;
    private readonly MigrationStoreDescriptor _descriptor;
    private readonly bool _isTarget;
    private readonly MigrationWriteMode _mode;
    private readonly bool _allowUpgradeSourceSchema;
    private readonly ILogger<EfCoreMigrationStore> _logger;
    private readonly Dictionary<MigrationEntity, long> _countCache = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public EfCoreMigrationStore(
        QuartzDbContext context,
        MigrationStoreDescriptor descriptor,
        bool isTarget,
        MigrationWriteMode mode,
        bool allowUpgradeSourceSchema,
        ILogger<EfCoreMigrationStore> logger
    )
    {
        _context = context;
        _descriptor = descriptor;
        _isTarget = isTarget;
        _mode = mode;
        _allowUpgradeSourceSchema = allowUpgradeSourceSchema;
        _logger = logger;
    }

    /// <inheritdoc />
    public MigrationStoreDescriptor Descriptor =>
        new()
        {
            Type = _descriptor.Type,
            ProfileName = _descriptor.ProfileName,
            ServerVersion = _descriptor.ServerVersion,
        };

    /// <inheritdoc />
    public async Task ProbeAsync(bool asTarget, CancellationToken cancellationToken = default)
    {
        if (!await _context.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"无法连接到 {_descriptor.Type} 数据库，请检查连接字符串"
            );
        }

        if (!asTarget)
        {
            var pending = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                if (_allowUpgradeSourceSchema)
                {
                    _logger.LogWarning("源数据库存在未应用的迁移，正在按配置执行结构升级");
                    await _context.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"源数据库结构版本较旧（存在 {pending.Count} 个未应用的迁移），请先升级源库结构，或勾选「允许升级源库结构」后重试"
                    );
                }
            }
        }
    }

    /// <inheritdoc />
    public Task BackupAsync(CancellationToken cancellationToken = default)
    {
        // 数据库不做自动备份，由使用方自行保障
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task EnsureTargetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在确保 {Provider} 目标数据库结构", _descriptor.Type);
        await _context.Database.MigrateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        if (_countCache.TryGetValue(entity, out var cached))
        {
            return cached;
        }

        var count = entity switch
        {
            MigrationEntity.Jobs => await _context.QuartzJobs.LongCountAsync(cancellationToken),
            MigrationEntity.Logs => await _context.QuartzJobLogs.LongCountAsync(cancellationToken),
            MigrationEntity.Settings => await _context.QuartzSettings.LongCountAsync(cancellationToken),
            MigrationEntity.Notifications => await _context
                .QuartzNotifications
                .LongCountAsync(cancellationToken),
            _ => 0L,
        };

        _countCache[entity] = count;
        return count;
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzJobInfo>> ReadJobsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context
            .QuartzJobs.AsNoTracking()
            .OrderBy(j => j.JobGroup)
            .ThenBy(j => j.JobName);
        var items = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
        return new MigrationPage<QuartzJobInfo>
        {
            Offset = offset,
            Total = (int)_countCache.GetValueOrDefault(MigrationEntity.Jobs),
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzJobLog>> ReadLogsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context
            .QuartzJobLogs.AsNoTracking()
            .OrderBy(l => l.CreateTime)
            .ThenBy(l => l.LogId);
        var items = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
        return new MigrationPage<QuartzJobLog>
        {
            Offset = offset,
            Total = (int)_countCache.GetValueOrDefault(MigrationEntity.Logs),
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzSetting>> ReadSettingsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.QuartzSettings.AsNoTracking().OrderBy(s => s.Key);
        var items = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
        return new MigrationPage<QuartzSetting>
        {
            Offset = offset,
            Total = (int)_countCache.GetValueOrDefault(MigrationEntity.Settings),
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzNotification>> ReadNotificationsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context
            .QuartzNotifications.AsNoTracking()
            .OrderBy(n => n.CreateTime)
            .ThenBy(n => n.NotificationId);
        var items = await query.Skip(offset).Take(pageSize).ToListAsync(cancellationToken);
        return new MigrationPage<QuartzNotification>
        {
            Offset = offset,
            Total = (int)_countCache.GetValueOrDefault(MigrationEntity.Notifications),
            Items = items,
        };
    }

    /// <inheritdoc />
    public Task BeginWriteAsync(
        MigrationEntity entity,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        _context.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<WriteOutcome> WriteJobsAsync(
        IReadOnlyList<QuartzJobInfo> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        var outcome = new WriteOutcome();
        var groups = items.Select(j => j.JobGroup).Distinct().ToList();
        var existing = await _context
            .QuartzJobs.AsNoTracking()
            .Where(j => groups.Contains(j.JobGroup))
            .ToListAsync(cancellationToken);
        var existingMap = existing.ToDictionary(MigrationKeys.Job, StringComparer.Ordinal);
        var added = new Dictionary<string, QuartzJobInfo>(StringComparer.Ordinal);
        var updates = new List<QuartzJobInfo>();

        foreach (var item in items)
        {
            var key = MigrationKeys.Job(item);
            if (added.ContainsKey(key))
            {
                outcome.Skipped++;
                continue;
            }

            if (existingMap.TryGetValue(key, out var current))
            {
                if (mode == MigrationWriteMode.Overwrite)
                {
                    CopyJobFields(item, current);
                    updates.Add(current);
                    outcome.Updated++;
                }
                else
                {
                    outcome.Skipped++;
                }
                continue;
            }

            added[key] = item;
            outcome.Inserted++;
        }

        if (added.Count > 0)
        {
            _context.QuartzJobs.AddRange(added.Values);
        }
        if (updates.Count > 0)
        {
            _context.QuartzJobs.UpdateRange(updates);
        }

        await SaveChangesAsync(cancellationToken);
        _countCache.Remove(MigrationEntity.Jobs);
        return outcome;
    }

    /// <inheritdoc />
    public async Task<WriteOutcome> WriteLogsAsync(
        IReadOnlyList<QuartzJobLog> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        var outcome = new WriteOutcome();
        var ids = items.Select(l => l.LogId).Distinct().ToList();
        var existingIds = await _context
            .QuartzJobLogs.AsNoTracking()
            .Where(l => ids.Contains(l.LogId))
            .Select(l => l.LogId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();
        var batchSet = new HashSet<Guid>();
        var inserts = new List<QuartzJobLog>();

        foreach (var item in items)
        {
            if (existingSet.Contains(item.LogId) || !batchSet.Add(item.LogId))
            {
                outcome.Skipped++;
                continue;
            }

            inserts.Add(item);
            outcome.Inserted++;
        }

        if (inserts.Count > 0)
        {
            _context.QuartzJobLogs.AddRange(inserts);
            await SaveChangesAsync(cancellationToken);
        }

        _countCache.Remove(MigrationEntity.Logs);
        return outcome;
    }

    /// <inheritdoc />
    public async Task<WriteOutcome> WriteSettingsAsync(
        IReadOnlyList<QuartzSetting> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        var outcome = new WriteOutcome();
        var keys = items.Select(s => s.Key).Distinct().ToList();
        var existing = await _context
            .QuartzSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToListAsync(cancellationToken);
        var existingMap = existing.ToDictionary(
            MigrationKeys.Setting,
            StringComparer.Ordinal
        );
        var added = new Dictionary<string, QuartzSetting>(StringComparer.Ordinal);
        var updates = new List<QuartzSetting>();

        foreach (var item in items)
        {
            var key = MigrationKeys.Setting(item);
            if (added.ContainsKey(key))
            {
                outcome.Skipped++;
                continue;
            }

            if (existingMap.TryGetValue(key, out var current))
            {
                if (mode == MigrationWriteMode.Merge)
                {
                    outcome.Skipped++;
                    continue;
                }

                current.Value = item.Value;
                current.Description = item.Description;
                current.Enabled = item.Enabled;
                current.UpdateTime = item.UpdateTime;
                updates.Add(current);
                outcome.Updated++;
                continue;
            }

            added[key] = item;
            outcome.Inserted++;
        }

        if (added.Count > 0)
        {
            _context.QuartzSettings.AddRange(added.Values);
        }
        if (updates.Count > 0)
        {
            _context.QuartzSettings.UpdateRange(updates);
        }

        await SaveChangesAsync(cancellationToken);
        _countCache.Remove(MigrationEntity.Settings);
        return outcome;
    }

    /// <inheritdoc />
    public async Task<WriteOutcome> WriteNotificationsAsync(
        IReadOnlyList<QuartzNotification> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        var outcome = new WriteOutcome();
        var ids = items.Select(n => n.NotificationId).Distinct().ToList();
        var existingIds = await _context
            .QuartzNotifications.AsNoTracking()
            .Where(n => ids.Contains(n.NotificationId))
            .Select(n => n.NotificationId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();
        var batchSet = new HashSet<Guid>();
        var inserts = new List<QuartzNotification>();

        foreach (var item in items)
        {
            if (existingSet.Contains(item.NotificationId) || !batchSet.Add(item.NotificationId))
            {
                outcome.Skipped++;
                continue;
            }

            inserts.Add(item);
            outcome.Inserted++;
        }

        if (inserts.Count > 0)
        {
            _context.QuartzNotifications.AddRange(inserts);
            await SaveChangesAsync(cancellationToken);
        }

        _countCache.Remove(MigrationEntity.Notifications);
        return outcome;
    }

    /// <inheritdoc />
    public async Task CompleteWriteAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        _context.ChangeTracker.Clear();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<long> ClearAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        long total = 0;
        switch (entity)
        {
            case MigrationEntity.Jobs:
                total = await _context.QuartzJobs.ExecuteDeleteAsync(cancellationToken);
                break;

            case MigrationEntity.Settings:
                total = await _context.QuartzSettings.ExecuteDeleteAsync(cancellationToken);
                break;

            case MigrationEntity.Logs:
            {
                while (true)
                {
                    var ids = await _context
                        .QuartzJobLogs.AsNoTracking()
                        .OrderBy(l => l.CreateTime)
                        .ThenBy(l => l.LogId)
                        .Select(l => l.LogId)
                        .Take(ClearChunkSize)
                        .ToListAsync(cancellationToken);
                    if (ids.Count == 0)
                    {
                        break;
                    }

                    total += await _context
                        .QuartzJobLogs.Where(l => ids.Contains(l.LogId))
                        .ExecuteDeleteAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                }
                break;
            }

            case MigrationEntity.Notifications:
            {
                while (true)
                {
                    var ids = await _context
                        .QuartzNotifications.AsNoTracking()
                        .OrderBy(n => n.CreateTime)
                        .ThenBy(n => n.NotificationId)
                        .Select(n => n.NotificationId)
                        .Take(ClearChunkSize)
                        .ToListAsync(cancellationToken);
                    if (ids.Count == 0)
                    {
                        break;
                    }

                    total += await _context
                        .QuartzNotifications.Where(n => ids.Contains(n.NotificationId))
                        .ExecuteDeleteAsync(cancellationToken);
                    _context.ChangeTracker.Clear();
                }
                break;
            }
        }

        _countCache.Remove(entity);
        _logger.LogInformation("已清空目标实体 {Entity}，共 {Count} 条", entity, total);
        return total;
    }

    /// <inheritdoc />
    public async Task<long> CountExistingAsync(
        MigrationEntity entity,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default
    )
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        switch (entity)
        {
            case MigrationEntity.Jobs:
            {
                var pairs = keys.Select(MigrationKeys.SplitJob).ToList();
                var groups = pairs.Select(p => p.JobGroup).Distinct().ToList();
                var existing = await _context
                    .QuartzJobs.AsNoTracking()
                    .Where(j => groups.Contains(j.JobGroup))
                    .Select(j => new { j.JobGroup, j.JobName })
                    .ToListAsync(cancellationToken);
                var existingKeys = existing
                    .Select(j => MigrationKeys.Job(j.JobGroup, j.JobName))
                    .ToHashSet(StringComparer.Ordinal);
                return keys.Count(existingKeys.Contains);
            }

            case MigrationEntity.Settings:
            {
                return await _context
                    .QuartzSettings.AsNoTracking()
                    .CountAsync(s => keys.Contains(s.Key), cancellationToken);
            }

            case MigrationEntity.Logs:
            {
                var ids = ParseGuids(keys);
                return await _context
                    .QuartzJobLogs.AsNoTracking()
                    .CountAsync(l => ids.Contains(l.LogId), cancellationToken);
            }

            case MigrationEntity.Notifications:
            {
                var ids = ParseGuids(keys);
                return await _context
                    .QuartzNotifications.AsNoTracking()
                    .CountAsync(n => ids.Contains(n.NotificationId), cancellationToken);
            }

            default:
                return 0;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _context.DisposeAsync();

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private static List<Guid> ParseGuids(IReadOnlyList<string> keys)
    {
        var ids = new List<Guid>(keys.Count);
        foreach (var key in keys)
        {
            if (Guid.TryParse(key, out var id))
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    private static void CopyJobFields(QuartzJobInfo source, QuartzJobInfo target)
    {
        // 不修改 CreateTime，保留目标首次创建时间
        target.TriggerName = source.TriggerName;
        target.TriggerGroup = source.TriggerGroup;
        target.CronExpression = source.CronExpression;
        target.Description = source.Description;
        target.JobType = source.JobType;
        target.JobClassOrApi = source.JobClassOrApi;
        target.JobData = source.JobData;
        target.ApiMethod = source.ApiMethod;
        target.ApiHeaders = source.ApiHeaders;
        target.ApiBody = source.ApiBody;
        target.ApiTimeout = source.ApiTimeout;
        target.RetryCount = source.RetryCount;
        target.RetryIntervalSeconds = source.RetryIntervalSeconds;
        target.SkipSslValidation = source.SkipSslValidation;
        target.DisallowConcurrentExecution = source.DisallowConcurrentExecution;
        target.Dependencies = source.Dependencies;
        target.StartTime = source.StartTime;
        target.EndTime = source.EndTime;
        target.Status = source.Status;
        target.IsEnabled = source.IsEnabled;
        target.NextRunTime = source.NextRunTime;
        target.PreviousRunTime = source.PreviousRunTime;
        target.Remark = source.Remark;
        target.UpdateTime = source.UpdateTime;
        target.CreateBy = source.CreateBy;
        target.UpdateBy = source.UpdateBy;
    }
}
