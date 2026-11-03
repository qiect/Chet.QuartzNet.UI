using System.Text.Json;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.Core.Migration;

/// <summary>
/// 文件存储迁移写入器
/// 负责按实体将数据合并/覆盖/替换写入 JSON 文件，日志与通知使用临时文件流式写入
/// </summary>
internal sealed class FileMigrationWriter
{
    private readonly string _basePath;
    private readonly MigrationWriteMode _mode;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;
    private readonly HashSet<MigrationEntity> _cleared = new();

    private Dictionary<string, QuartzJobInfo>? _jobsByKey;
    private Dictionary<string, QuartzSetting>? _settingsByKey;

    private MigrationEntity? _streamEntity;
    private FileStream? _stream;
    private Utf8JsonWriter? _writer;
    private string? _tempPath;
    private string? _finalPath;
    private HashSet<string>? _streamKeys;

    /// <summary>
    /// 构造函数
    /// </summary>
    public FileMigrationWriter(
        string basePath,
        MigrationWriteMode mode,
        ILogger logger,
        JsonSerializerOptions? jsonOptions = null
    )
    {
        _basePath = basePath;
        _mode = mode;
        _logger = logger;
        _jsonOptions = jsonOptions ?? JsonSerializationConfig.JsonOptions();
    }

    /// <summary>
    /// 是否已开始写入指定实体
    /// </summary>
    public bool IsPrepared(MigrationEntity entity) =>
        entity switch
        {
            MigrationEntity.Jobs => _jobsByKey != null,
            MigrationEntity.Settings => _settingsByKey != null,
            MigrationEntity.Logs or MigrationEntity.Notifications => _streamEntity == entity,
            _ => false,
        };

    /// <summary>
    /// 准备写入指定实体
    /// </summary>
    public async Task PrepareAsync(MigrationEntity entity, CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case MigrationEntity.Jobs:
                if (_jobsByKey == null)
                {
                    var jobs = _cleared.Contains(entity)
                        ? new List<QuartzJobInfo>()
                        : await ReadListAsync<QuartzJobInfo>(GetFilePath(entity), cancellationToken);
                    _jobsByKey = jobs.ToDictionary(MigrationKeys.Job, StringComparer.Ordinal);
                }
                break;

            case MigrationEntity.Settings:
                if (_settingsByKey == null)
                {
                    var settings = _cleared.Contains(entity)
                        ? new List<QuartzSetting>()
                        : await ReadListAsync<QuartzSetting>(GetFilePath(entity), cancellationToken);
                    _settingsByKey = settings.ToDictionary(
                        MigrationKeys.Setting,
                        StringComparer.Ordinal
                    );
                }
                break;

            case MigrationEntity.Logs:
            case MigrationEntity.Notifications:
                await PrepareStreamAsync(entity, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// 写入作业
    /// </summary>
    public Task<WriteOutcome> WriteJobsAsync(
        IReadOnlyList<QuartzJobInfo> items,
        MigrationWriteMode mode
    )
    {
        if (_jobsByKey == null)
        {
            throw new InvalidOperationException("作业写入器尚未准备好，请先调用 BeginWriteAsync");
        }

        var outcome = new WriteOutcome();
        foreach (var item in items)
        {
            var key = MigrationKeys.Job(item);
            var existing = _jobsByKey.GetValueOrDefault(key);
            if (existing == null)
            {
                _jobsByKey[key] = item;
                outcome.Inserted++;
                continue;
            }

            if (mode == MigrationWriteMode.Overwrite)
            {
                CopyJobFields(item, existing);
                outcome.Updated++;
            }
            else
            {
                outcome.Skipped++;
            }
        }

        return Task.FromResult(outcome);
    }

    /// <summary>
    /// 写入设置
    /// </summary>
    public Task<WriteOutcome> WriteSettingsAsync(
        IReadOnlyList<QuartzSetting> items,
        MigrationWriteMode mode
    )
    {
        if (_settingsByKey == null)
        {
            throw new InvalidOperationException("设置写入器尚未准备好，请先调用 BeginWriteAsync");
        }

        var outcome = new WriteOutcome();
        foreach (var item in items)
        {
            var key = MigrationKeys.Setting(item);
            var existing = _settingsByKey.GetValueOrDefault(key);
            if (existing == null)
            {
                _settingsByKey[key] = item;
                outcome.Inserted++;
                continue;
            }

            if (mode == MigrationWriteMode.Merge)
            {
                outcome.Skipped++;
                continue;
            }

            existing.Value = item.Value;
            existing.Description = item.Description;
            existing.Enabled = item.Enabled;
            existing.UpdateTime = item.UpdateTime;
            outcome.Updated++;
        }

        return Task.FromResult(outcome);
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    public Task<WriteOutcome> WriteLogsAsync(
        IReadOnlyList<QuartzJobLog> items,
        MigrationWriteMode mode
    ) => WriteStreamItemsAsync(items, mode, MigrationKeys.Log);

    /// <summary>
    /// 写入通知
    /// </summary>
    public Task<WriteOutcome> WriteNotificationsAsync(
        IReadOnlyList<QuartzNotification> items,
        MigrationWriteMode mode
    ) => WriteStreamItemsAsync(items, mode, MigrationKeys.Notification);

    /// <summary>
    /// 完成指定实体写入并提交
    /// </summary>
    public async Task CompleteAsync(MigrationEntity entity, CancellationToken cancellationToken)
    {
        if (entity == MigrationEntity.Jobs && _jobsByKey != null)
        {
            var jobs = _jobsByKey.Values
                .OrderBy(j => j.JobGroup, StringComparer.Ordinal)
                .ThenBy(j => j.JobName, StringComparer.Ordinal)
                .ToList();
            await WriteListAsync(GetFilePath(entity), jobs, cancellationToken);
            _jobsByKey = null;
            return;
        }

        if (entity == MigrationEntity.Settings && _settingsByKey != null)
        {
            var settings = _settingsByKey.Values
                .OrderBy(s => s.Key, StringComparer.Ordinal)
                .ToList();
            await WriteListAsync(GetFilePath(entity), settings, cancellationToken);
            _settingsByKey = null;
            return;
        }

        if (_streamEntity == entity)
        {
            await CompleteStreamAsync();
        }
    }

    /// <summary>
    /// 清空指定实体，返回清空前的数量
    /// </summary>
    public async Task<long> ClearAsync(MigrationEntity entity, CancellationToken cancellationToken)
    {
        var count = await CountAsync(entity, cancellationToken);
        _cleared.Add(entity);

        switch (entity)
        {
            case MigrationEntity.Jobs:
                _jobsByKey = new Dictionary<string, QuartzJobInfo>(StringComparer.Ordinal);
                break;
            case MigrationEntity.Settings:
                _settingsByKey = new Dictionary<string, QuartzSetting>(StringComparer.Ordinal);
                break;
            case MigrationEntity.Logs:
            case MigrationEntity.Notifications:
                if (_streamEntity == entity)
                {
                    await AbortStreamAsync();
                }
                break;
        }

        return count;
    }

    /// <summary>
    /// 统计指定实体的记录数
    /// </summary>
    public async Task<long> CountAsync(MigrationEntity entity, CancellationToken cancellationToken)
    {
        var path = GetFilePath(entity);
        return entity switch
        {
            MigrationEntity.Jobs => (await ReadListAsync<QuartzJobInfo>(path, cancellationToken))
                .Count,
            MigrationEntity.Settings => (
                await ReadListAsync<QuartzSetting>(path, cancellationToken)
            ).Count,
            MigrationEntity.Logs => await CountJsonArrayAsync<QuartzJobLog>(path, cancellationToken),
            MigrationEntity.Notifications => await CountJsonArrayAsync<QuartzNotification>(
                path,
                cancellationToken
            ),
            _ => 0,
        };
    }

    /// <summary>
    /// 备份当前目录下的数据文件
    /// </summary>
    public static void BackupDirectory(string basePath, string backupRootPath)
    {
        if (!Directory.Exists(basePath))
        {
            return;
        }

        Directory.CreateDirectory(backupRootPath);
        foreach (var file in KnownFiles())
        {
            var source = Path.Combine(basePath, file);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(backupRootPath, file), true);
            }
        }
    }

    /// <summary>
    /// 释放资源并删除未提交的临时文件
    /// </summary>
    public async Task AbortAsync()
    {
        await AbortStreamAsync();
    }

    private async Task PrepareStreamAsync(
        MigrationEntity entity,
        CancellationToken cancellationToken
    )
    {
        if (_streamEntity == entity)
        {
            return;
        }

        if (_streamEntity != null)
        {
            await AbortStreamAsync();
        }

        _streamEntity = entity;
        _finalPath = GetFilePath(entity);
        _tempPath = _finalPath + ".tmp";
        _streamKeys = new HashSet<string>(StringComparer.Ordinal);

        var directory = Path.GetDirectoryName(_finalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        _writer = new Utf8JsonWriter(
            _stream,
            new JsonWriterOptions
            {
                Indented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }
        );
        _writer.WriteStartArray();

        if (!_cleared.Contains(entity) && File.Exists(_finalPath))
        {
            // 保留目标已有数据，并记录业务键用于幂等去重
            if (entity == MigrationEntity.Logs)
            {
                await foreach (
                    var item in ReadJsonArrayAsync<QuartzJobLog>(_finalPath, cancellationToken)
                )
                {
                    _streamKeys.Add(MigrationKeys.Log(item));
                    JsonSerializer.Serialize(_writer, item, _jsonOptions);
                }
            }
            else
            {
                await foreach (
                    var item in ReadJsonArrayAsync<QuartzNotification>(
                        _finalPath,
                        cancellationToken
                    )
                )
                {
                    _streamKeys.Add(MigrationKeys.Notification(item));
                    JsonSerializer.Serialize(_writer, item, _jsonOptions);
                }
            }
        }

        _logger.LogDebug("已准备文件迁移写入：{Path}", _finalPath);
    }

    private Task<WriteOutcome> WriteStreamItemsAsync<T>(
        IReadOnlyList<T> items,
        MigrationWriteMode mode,
        Func<T, string> keySelector
    )
    {
        if (_writer == null || _streamKeys == null)
        {
            throw new InvalidOperationException("流式写入器尚未准备好，请先调用 BeginWriteAsync");
        }

        var isCleared = _streamEntity is { } streamEntity && _cleared.Contains(streamEntity);

        var outcome = new WriteOutcome();
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!isCleared && _streamKeys.Contains(key))
            {
                outcome.Skipped++;
                continue;
            }

            _streamKeys.Add(key);
            JsonSerializer.Serialize(_writer, item, _jsonOptions);
            outcome.Inserted++;
        }

        return Task.FromResult(outcome);
    }

    private async Task CompleteStreamAsync()
    {
        if (_writer == null || _stream == null || _tempPath == null || _finalPath == null)
        {
            return;
        }

        _writer.WriteEndArray();
        await _writer.FlushAsync();
        _writer.Dispose();
        await _stream.DisposeAsync();

        if (File.Exists(_finalPath))
        {
            File.Move(_tempPath, _finalPath, true);
        }
        else
        {
            File.Move(_tempPath, _finalPath);
        }

        _logger.LogDebug("文件迁移写入已提交：{Path}", _finalPath);

        _writer = null;
        _stream = null;
        _tempPath = null;
        _finalPath = null;
        _streamEntity = null;
        _streamKeys = null;
    }

    private async Task AbortStreamAsync()
    {
        if (_writer != null)
        {
            try
            {
                _writer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放文件迁移写入器失败");
            }
            _writer = null;
        }

        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        if (_tempPath != null && File.Exists(_tempPath))
        {
            try
            {
                File.Delete(_tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除临时迁移文件失败：{Path}", _tempPath);
            }
        }

        _tempPath = null;
        _finalPath = null;
        _streamEntity = null;
        _streamKeys = null;
    }

    private async Task<List<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new List<T>();
        }

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

    private async Task WriteListAsync<T>(
        string path,
        List<T> items,
        CancellationToken cancellationToken
    )
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        ))
        {
            await JsonSerializer.SerializeAsync(stream, items, _jsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Move(tempPath, path, true);
        }
        else
        {
            File.Move(tempPath, path);
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

    private static IEnumerable<string> KnownFiles()
    {
        yield return "jobs.json";
        yield return "logs.json";
        yield return "setting.json";
        yield return "notification.json";
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
