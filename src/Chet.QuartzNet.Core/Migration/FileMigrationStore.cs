using System.Text.Json;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.Core.Migration;

/// <summary>
/// 文件存储迁移适配器
/// 作为源时支持日志/通知的流式分页读取，作为目标时按实体合并、覆盖或替换写入
/// </summary>
public sealed class FileMigrationStore : IMigrationStore
{
    private static readonly string[] DataFiles =
    {
        "jobs.json",
        "logs.json",
        "setting.json",
        "notification.json",
    };

    private readonly string _basePath;
    private readonly MigrationWriteMode _mode;
    private readonly ILogger<FileMigrationStore> _logger;
    private readonly FileMigrationWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<MigrationEntity, long> _countCache = new();
    private readonly object _sync = new();
    private JsonArrayCursor<QuartzJobLog>? _logCursor;
    private JsonArrayCursor<QuartzNotification>? _notificationCursor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="descriptor">文件存储描述</param>
    /// <param name="mode">写入模式</param>
    /// <param name="logger">日志记录器</param>
    public FileMigrationStore(
        MigrationStoreDescriptor descriptor,
        MigrationWriteMode mode,
        ILogger<FileMigrationStore> logger
    )
    {
        if (string.IsNullOrWhiteSpace(descriptor.FilePath))
        {
            throw new ArgumentException("文件存储路径不能为空", nameof(descriptor));
        }

        _basePath = Path.GetFullPath(descriptor.FilePath);
        _mode = mode;
        _logger = logger;
        _jsonOptions = JsonSerializationConfig.JsonOptions();
        _writer = new FileMigrationWriter(_basePath, mode, logger, _jsonOptions);
    }

    /// <inheritdoc />
    public MigrationStoreDescriptor Descriptor =>
        new() { Type = MigrationStoreType.File, FilePath = _basePath };

    /// <inheritdoc />
    public Task ProbeAsync(bool asTarget, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_basePath))
        {
            if (asTarget)
            {
                return Task.CompletedTask;
            }

            throw new InvalidOperationException($"文件存储目录不存在：{_basePath}");
        }

        foreach (var file in DataFiles)
        {
            var path = Path.Combine(_basePath, file);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"文件存储文件不可读：{path}，{ex.Message}", ex);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task BackupAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_basePath))
        {
            var backupPath = Path.Combine(
                _basePath,
                "Backup",
                $"migration_{DateTime.Now:yyyyMMdd_HHmmss}"
            );
            FileMigrationWriter.BackupDirectory(_basePath, backupPath);
            _logger.LogInformation("已备份文件存储数据到：{Path}", backupPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureTargetSchemaAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basePath);

        if (_mode == MigrationWriteMode.Replace)
        {
            var backupPath = Path.Combine(
                _basePath,
                "Backup",
                $"migration_{DateTime.Now:yyyyMMdd_HHmmss}"
            );
            FileMigrationWriter.BackupDirectory(_basePath, backupPath);
            _logger.LogInformation("Replace 模式已备份目标文件存储到：{Path}", backupPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        lock (_sync)
        {
            if (_countCache.TryGetValue(entity, out var cached))
            {
                return cached;
            }
        }

        long count;
        switch (entity)
        {
            case MigrationEntity.Jobs:
                count = (await ReadListAsync<QuartzJobInfo>(GetFilePath(entity), cancellationToken))
                    .Count;
                break;
            case MigrationEntity.Settings:
                count = (
                    await ReadListAsync<QuartzSetting>(GetFilePath(entity), cancellationToken)
                ).Count;
                break;
            case MigrationEntity.Logs:
                count = await CountJsonArrayAsync<QuartzJobLog>(
                    GetFilePath(entity),
                    cancellationToken
                );
                break;
            case MigrationEntity.Notifications:
                count = await CountJsonArrayAsync<QuartzNotification>(
                    GetFilePath(entity),
                    cancellationToken
                );
                break;
            default:
                count = 0;
                break;
        }

        lock (_sync)
        {
            _countCache[entity] = count;
        }
        return count;
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzJobInfo>> ReadJobsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var jobs = await ReadListAsync<QuartzJobInfo>(GetFilePath(MigrationEntity.Jobs), cancellationToken);
        var ordered = jobs
            .OrderBy(j => j.JobGroup, StringComparer.Ordinal)
            .ThenBy(j => j.JobName, StringComparer.Ordinal)
            .ToList();
        return new MigrationPage<QuartzJobInfo>
        {
            Offset = offset,
            Total = ordered.Count,
            Items = ordered.Skip(offset).Take(pageSize).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzJobLog>> ReadLogsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var total = (int)await CountAsync(MigrationEntity.Logs, cancellationToken);
        var cursor = GetLogCursor();
        var items = await cursor.ReadAsync(offset, pageSize, cancellationToken);
        return new MigrationPage<QuartzJobLog>
        {
            Offset = offset,
            Total = total,
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
        var settings = await ReadListAsync<QuartzSetting>(
            GetFilePath(MigrationEntity.Settings),
            cancellationToken
        );
        var ordered = settings.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        return new MigrationPage<QuartzSetting>
        {
            Offset = offset,
            Total = ordered.Count,
            Items = ordered.Skip(offset).Take(pageSize).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<MigrationPage<QuartzNotification>> ReadNotificationsAsync(
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var total = (int)await CountAsync(MigrationEntity.Notifications, cancellationToken);
        var cursor = GetNotificationCursor();
        var items = await cursor.ReadAsync(offset, pageSize, cancellationToken);
        return new MigrationPage<QuartzNotification>
        {
            Offset = offset,
            Total = total,
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task BeginWriteAsync(
        MigrationEntity entity,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    )
    {
        await _writer.PrepareAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WriteOutcome> WriteJobsAsync(
        IReadOnlyList<QuartzJobInfo> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    ) => _writer.WriteJobsAsync(items, mode);

    /// <inheritdoc />
    public Task<WriteOutcome> WriteLogsAsync(
        IReadOnlyList<QuartzJobLog> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    ) => _writer.WriteLogsAsync(items, mode);

    /// <inheritdoc />
    public Task<WriteOutcome> WriteSettingsAsync(
        IReadOnlyList<QuartzSetting> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    ) => _writer.WriteSettingsAsync(items, mode);

    /// <inheritdoc />
    public Task<WriteOutcome> WriteNotificationsAsync(
        IReadOnlyList<QuartzNotification> items,
        MigrationWriteMode mode,
        CancellationToken cancellationToken = default
    ) => _writer.WriteNotificationsAsync(items, mode);

    /// <inheritdoc />
    public async Task CompleteWriteAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        await _writer.CompleteAsync(entity, cancellationToken);
        Invalidate(entity);
    }

    /// <inheritdoc />
    public async Task<long> ClearAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        var count = await _writer.ClearAsync(entity, cancellationToken);
        await ResetCursorAsync(entity);
        lock (_sync)
        {
            _countCache[entity] = 0;
        }
        return count;
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
                var jobs = await ReadListAsync<QuartzJobInfo>(
                    GetFilePath(entity),
                    cancellationToken
                );
                var set = new HashSet<string>(jobs.Select(MigrationKeys.Job), StringComparer.Ordinal);
                return keys.Count(set.Contains);
            }
            case MigrationEntity.Settings:
            {
                var settings = await ReadListAsync<QuartzSetting>(
                    GetFilePath(entity),
                    cancellationToken
                );
                var set = new HashSet<string>(
                    settings.Select(MigrationKeys.Setting),
                    StringComparer.Ordinal
                );
                return keys.Count(set.Contains);
            }
            case MigrationEntity.Logs:
            {
                var logKeys = keys.Select(k => Guid.TryParse(k, out var id) ? id : Guid.Empty).ToHashSet();
                var logs = await ReadListAsync<QuartzJobLog>(GetFilePath(entity), cancellationToken);
                return logs.Count(l => logKeys.Contains(l.LogId));
            }
            case MigrationEntity.Notifications:
            {
                var notificationKeys = keys
                    .Select(k => Guid.TryParse(k, out var id) ? id : Guid.Empty)
                    .ToHashSet();
                var notifications = await ReadListAsync<QuartzNotification>(
                    GetFilePath(entity),
                    cancellationToken
                );
                return notifications.Count(n => notificationKeys.Contains(n.NotificationId));
            }
            default:
                return 0;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _writer.AbortAsync();
        if (_logCursor != null)
        {
            await _logCursor.DisposeAsync();
        }
        if (_notificationCursor != null)
        {
            await _notificationCursor.DisposeAsync();
        }
    }

    private JsonArrayCursor<QuartzJobLog> GetLogCursor()
    {
        lock (_sync)
        {
            return _logCursor ??= new JsonArrayCursor<QuartzJobLog>(
                GetFilePath(MigrationEntity.Logs),
                _jsonOptions
            );
        }
    }

    private JsonArrayCursor<QuartzNotification> GetNotificationCursor()
    {
        lock (_sync)
        {
            return _notificationCursor ??= new JsonArrayCursor<QuartzNotification>(
                GetFilePath(MigrationEntity.Notifications),
                _jsonOptions
            );
        }
    }

    private async Task ResetCursorAsync(MigrationEntity entity)
    {
        if (entity == MigrationEntity.Logs && _logCursor != null)
        {
            await _logCursor.DisposeAsync();
            _logCursor = null;
        }
        if (entity == MigrationEntity.Notifications && _notificationCursor != null)
        {
            await _notificationCursor.DisposeAsync();
            _notificationCursor = null;
        }
    }

    private void Invalidate(MigrationEntity entity)
    {
        lock (_sync)
        {
            _countCache.Remove(entity);
        }
    }

    private string GetFilePath(MigrationEntity entity) =>
        Path.Combine(
            _basePath,
            entity switch
            {
                MigrationEntity.Jobs => "jobs.json",
                MigrationEntity.Logs => "logs.json",
                MigrationEntity.Settings => "setting.json",
                MigrationEntity.Notifications => "notification.json",
                _ => throw new ArgumentOutOfRangeException(nameof(entity)),
            }
        );

    private async Task<List<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new List<T>();
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );
            var items = await JsonSerializer.DeserializeAsync<List<T>>(
                stream,
                _jsonOptions,
                cancellationToken
            );
            return items ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"文件存储数据格式无效：{path}，{ex.Message}", ex);
        }
    }

    private async Task<long> CountJsonArrayAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        long count = 0;
        await foreach (var _ in ReadJsonArrayAsync<T>(path, cancellationToken))
        {
            count++;
        }
        return count;
    }

    private async IAsyncEnumerable<T> ReadJsonArrayAsync<T>(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        await foreach (
            var item in JsonSerializer.DeserializeAsyncEnumerable<T>(
                stream,
                _jsonOptions,
                cancellationToken
            )
        )
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// 顺序读取 JSON 数组的游标，保证分页读取时不会把全量数据加载到内存
    /// </summary>
    private sealed class JsonArrayCursor<T> : IAsyncDisposable
    {
        private readonly string _path;
        private readonly JsonSerializerOptions _options;
        private FileStream? _stream;
        private IAsyncEnumerator<T>? _enumerator;
        private int _position;

        public JsonArrayCursor(string path, JsonSerializerOptions options)
        {
            _path = path;
            _options = options;
        }

        public async Task<IReadOnlyList<T>> ReadAsync(
            int offset,
            int pageSize,
            CancellationToken cancellationToken
        )
        {
            if (!File.Exists(_path))
            {
                return Array.Empty<T>();
            }

            if (_enumerator == null || _position > offset)
            {
                await ResetAsync(cancellationToken);
            }

            while (_position < offset)
            {
                if (!await MoveNextAsync())
                {
                    return Array.Empty<T>();
                }
                _position++;
            }

            var items = new List<T>(pageSize);
            while (items.Count < pageSize)
            {
                if (!await MoveNextAsync())
                {
                    break;
                }
                items.Add(_enumerator!.Current);
                _position++;
            }

            return items;
        }

        private async Task<bool> MoveNextAsync()
        {
            if (_enumerator == null)
            {
                return false;
            }
            return await _enumerator.MoveNextAsync();
        }

        private async Task ResetAsync(CancellationToken cancellationToken)
        {
            if (_enumerator != null)
            {
                await _enumerator.DisposeAsync();
                _enumerator = null;
            }
            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            _position = 0;
            _stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );
            _enumerator = (
                IAsyncEnumerator<T>
            )JsonSerializer
                .DeserializeAsyncEnumerable<T>(_stream, _options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_enumerator != null)
            {
                await _enumerator.DisposeAsync();
                _enumerator = null;
            }
            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }
        }
    }
}
