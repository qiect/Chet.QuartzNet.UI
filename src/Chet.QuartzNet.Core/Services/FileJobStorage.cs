using System.Collections.Concurrent;
using System.Text.Json;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// 文件存储实现类，实现了IJobStorage接口
/// 负责将作业信息和执行日志存储到本地文件中
/// </summary>
public class FileJobStorage : IJobStorage
{
    /// <summary>
    /// 作业数据文件名
    /// </summary>
    public const string JobsFileName = "jobs.json";

    /// <summary>
    /// 日志数据文件名
    /// </summary>
    public const string LogsFileName = "logs.json";

    /// <summary>
    /// 设置数据文件名
    /// </summary>
    public const string SettingsFileName = "setting.json";

    /// <summary>
    /// 通知数据文件名
    /// </summary>
    public const string NotificationsFileName = "notification.json";

    /// <summary>
    /// 配置选项
    /// </summary>
    private readonly QuartzUIOptions _options;

    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<FileJobStorage> _logger;

    /// <summary>
    /// 作业数据文件路径
    /// </summary>
    private readonly string _jobsFilePath;

    /// <summary>
    /// 日志数据文件路径
    /// </summary>
    private readonly string _logsFilePath;

    /// <summary>
    /// 设置数据文件路径
    /// </summary>
    private readonly string _settingsFilePath;

    /// <summary>
    /// 通知数据文件路径
    /// </summary>
    private readonly string _notificationsFilePath;

    /// <summary>
    /// 文件锁字典
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> _fileLocks = new();

    /// <summary>
    /// 最后备份时间字典，用于跟踪每个文件的最后备份时间
    /// </summary>
    private static readonly ConcurrentDictionary<string, DateTime> _lastBackupTimes = new();

    /// <summary>
    /// 初始化FileJobStorage实例
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public FileJobStorage(IOptions<QuartzUIOptions> options, ILogger<FileJobStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        // 构建文件路径
        _jobsFilePath = Path.Combine(_options.FileStoragePath, JobsFileName);
        _logsFilePath = Path.Combine(_options.FileStoragePath, LogsFileName);
        _settingsFilePath = Path.Combine(_options.FileStoragePath, SettingsFileName);
        _notificationsFilePath = Path.Combine(_options.FileStoragePath, NotificationsFileName);

        // 确保存储目录存在
        Directory.CreateDirectory(_options.FileStoragePath);

        // 如果启用了文件备份，确保备份目录存在
        if (_options.EnableFileBackup)
        {
            Directory.CreateDirectory(_options.FileBackupPath);
        }
    }

    #region 作业管理

    /// <summary>
    /// 添加新的作业信息
    /// </summary>
    /// <param name="jobInfo">作业信息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加成功返回true，失败返回false</returns>
    public Task<bool> AddJobAsync(
        QuartzJobInfo jobInfo,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_jobsFilePath);
        lock (lockObject)
        {
            try
            {
                var jobs = LoadJobsCore();
                jobs.Add(jobInfo);
                SaveJobsCore(jobs);

                _logger.LogSuccess("添加作业", "作业: {jobKey}", jobInfo.GetJobKey());
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("添加作业", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 更新现有作业信息
    /// </summary>
    /// <param name="jobInfo">作业信息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回true，失败返回false</returns>
    public Task<bool> UpdateJobAsync(
        QuartzJobInfo jobInfo,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_jobsFilePath);
        lock (lockObject)
        {
            try
            {
                var jobs = LoadJobsCore();
                var existingJob = jobs.FirstOrDefault(j =>
                    j.JobName == jobInfo.JobName && j.JobGroup == jobInfo.JobGroup
                );

                if (existingJob == null)
                {
                    return Task.FromResult(false);
                }

                // 更新作业属性
                existingJob.TriggerName = jobInfo.TriggerName;
                existingJob.TriggerGroup = jobInfo.TriggerGroup;
                existingJob.CronExpression = jobInfo.CronExpression;
                existingJob.Description = jobInfo.Description;
                existingJob.JobType = jobInfo.JobType;
                existingJob.JobClassOrApi = jobInfo.JobClassOrApi;
                existingJob.JobData = jobInfo.JobData;
                existingJob.ApiMethod = jobInfo.ApiMethod;
                existingJob.ApiHeaders = jobInfo.ApiHeaders;
                existingJob.ApiBody = jobInfo.ApiBody;
                existingJob.ApiTimeout = jobInfo.ApiTimeout;
                existingJob.RetryCount = jobInfo.RetryCount;
                existingJob.RetryIntervalSeconds = jobInfo.RetryIntervalSeconds;
                existingJob.SkipSslValidation = jobInfo.SkipSslValidation;
                existingJob.DisallowConcurrentExecution = jobInfo.DisallowConcurrentExecution;
                existingJob.RerunOnRecovery = jobInfo.RerunOnRecovery;
                existingJob.Dependencies = jobInfo.Dependencies;
                existingJob.StartTime = jobInfo.StartTime;
                existingJob.EndTime = jobInfo.EndTime;
                existingJob.IsEnabled = jobInfo.IsEnabled;
                existingJob.Status = jobInfo.Status;
                existingJob.NextRunTime = jobInfo.NextRunTime;
                existingJob.PreviousRunTime = jobInfo.PreviousRunTime;
                existingJob.UpdateTime = jobInfo.UpdateTime;
                existingJob.UpdateBy = jobInfo.UpdateBy;

                SaveJobsCore(jobs);

                _logger.LogSuccess("更新作业", "作业: {jobKey}", jobInfo.GetJobKey());
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("更新作业", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 删除指定作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回true，失败返回false</returns>
    public async Task<bool> DeleteJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var jobToRemove = jobs.FirstOrDefault(j =>
                j.JobName == jobName && j.JobGroup == jobGroup
            );

            if (jobToRemove == null)
            {
                return false;
            }

            jobs.Remove(jobToRemove);
            await SaveJobsAsync(jobs);

            _logger.LogSuccess("删除作业", $"{jobGroup}.{jobName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("删除作业", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取指定作业信息
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业信息对象，不存在返回null</returns>
    public async Task<QuartzJobInfo?> GetJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobs = await LoadJobsAsync();
            return jobs.FirstOrDefault(j =>
                j.JobName.Equals(jobName, StringComparison.CurrentCultureIgnoreCase)
                && j.JobGroup == jobGroup
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业信息", ex);
            return null;
        }
    }

    /// <summary>
    /// 获取作业列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页作业列表</returns>
    public async Task<PagedResponseDto<QuartzJobInfo>> GetJobsAsync(
        QuartzJobQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobs = await LoadJobsAsync();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                jobs = jobs.Where(j =>
                        j.JobName.Contains(queryDto.JobName, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                jobs = jobs.Where(j =>
                        j.JobGroup.Contains(queryDto.JobGroup, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.JobClassOrApi))
            {
                jobs = jobs.Where(j =>
                        j.JobClassOrApi.Contains(queryDto.JobClassOrApi, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (queryDto.Status.HasValue)
            {
                jobs = jobs.Where(j => j.Status == queryDto.Status.Value).ToList();
            }

            if (queryDto.IsEnabled.HasValue)
            {
                jobs = jobs.Where(j => j.IsEnabled == queryDto.IsEnabled.Value).ToList();
            }

            // 应用排序
            if (!string.IsNullOrEmpty(queryDto.SortBy) && !string.IsNullOrEmpty(queryDto.SortOrder))
            {
                var isAscending = queryDto.SortOrder.ToLower().StartsWith("asc");

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.JobName).ToList()
                            : jobs.OrderByDescending(j => j.JobName).ToList();
                        break;
                    case "jobgroup":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.JobGroup).ToList()
                            : jobs.OrderByDescending(j => j.JobGroup).ToList();
                        break;
                    case "status":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.Status).ToList()
                            : jobs.OrderByDescending(j => j.Status).ToList();
                        break;
                    case "isenabled":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.IsEnabled).ToList()
                            : jobs.OrderByDescending(j => j.IsEnabled).ToList();
                        break;
                    case "createtime":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.CreateTime).ToList()
                            : jobs.OrderByDescending(j => j.CreateTime).ToList();
                        break;
                    case "updatetime":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.UpdateTime).ToList()
                            : jobs.OrderByDescending(j => j.UpdateTime).ToList();
                        break;
                    case "previousruntime":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.PreviousRunTime).ToList()
                            : jobs.OrderByDescending(j => j.PreviousRunTime).ToList();
                        break;
                    case "nextruntime":
                        jobs = isAscending
                            ? jobs.OrderBy(j => j.NextRunTime).ToList()
                            : jobs.OrderByDescending(j => j.NextRunTime).ToList();
                        break;
                    default:
                        // 默认按创建时间降序排序
                        jobs = jobs.OrderByDescending(j => j.CreateTime).ToList();
                        break;
                }
            }
            else
            {
                // 默认按创建时间降序排序
                jobs = jobs.OrderByDescending(j => j.CreateTime).ToList();
            }

            // 分页处理
            var totalCount = jobs.Count;
            var pagedJobs = jobs.Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

            // 构建分页响应
            return new PagedResponseDto<QuartzJobInfo>
            {
                Items = pagedJobs,
                TotalCount = totalCount,
                PageIndex = queryDto.PageIndex,
                PageSize = queryDto.PageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业列表", ex);
            return new PagedResponseDto<QuartzJobInfo>();
        }
    }

    /// <summary>
    /// 获取所有作业信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业信息列表</returns>
    public async Task<List<QuartzJobInfo>> GetAllJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取所有作业", ex);
            return new List<QuartzJobInfo>();
        }
    }

    /// <summary>
    /// 更新作业状态
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="status">新状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回true，失败返回false</returns>
    public async Task<bool> UpdateJobStatusAsync(
        string jobName,
        string jobGroup,
        JobStatus status,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var job = jobs.FirstOrDefault(j => j.JobName == jobName && j.JobGroup == jobGroup);

            if (job == null)
            {
                return false;
            }

            job.Status = status;
            job.UpdateTime = DateTime.Now;

            await SaveJobsAsync(jobs);

            _logger.LogSuccess(
                "更新作业状态",
                "作业: {jobKey}, 状态: {status}",
                job.GetJobKey(),
                status
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("更新作业状态", ex);
            return false;
        }
    }

    #endregion

    #region 作业日志

    /// <summary>
    /// 添加作业执行日志
    /// </summary>
    /// <param name="jobLog">作业日志对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加成功返回true，失败返回false</returns>
    public Task<bool> AddJobLogAsync(
        QuartzJobLog jobLog,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                // 读取与写入在同一把锁内完成，避免并发执行时日志被覆盖丢失
                var logs = LoadLogsCore(throwOnError: true);
                logs.Add(jobLog);
                SaveLogsCore(logs);

                _logger.LogSuccess("添加作业日志", $"作业日志Id: {jobLog.LogId}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("添加作业日志", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 获取作业执行日志列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页日志列表</returns>
    public async Task<PagedResponseDto<QuartzJobLog>> GetJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var logs = await LoadLogsAsync();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                logs = logs.Where(l =>
                        l.JobName.Contains(queryDto.JobName, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                logs = logs.Where(l =>
                        l.JobGroup.Contains(queryDto.JobGroup, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            if (queryDto.Status.HasValue)
            {
                logs = logs.Where(l => l.Status == queryDto.Status.Value).ToList();
            }

            if (queryDto.Statuses != null && queryDto.Statuses.Count > 0)
            {
                var statusSet = queryDto.Statuses.ToHashSet();
                logs = logs.Where(l => statusSet.Contains(l.Status)).ToList();
            }

            if (queryDto.StartTime.HasValue)
            {
                logs = logs.Where(l => l.StartTime >= queryDto.StartTime.Value).ToList();
            }

            if (queryDto.EndTime.HasValue)
            {
                logs = logs.Where(l => l.StartTime <= queryDto.EndTime.Value).ToList();
            }

            // 应用排序
            if (!string.IsNullOrEmpty(queryDto.SortBy) && !string.IsNullOrEmpty(queryDto.SortOrder))
            {
                var isAscending = queryDto.SortOrder.ToLower().StartsWith("asc");

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        logs = isAscending
                            ? logs.OrderBy(l => l.JobName).ToList()
                            : logs.OrderByDescending(l => l.JobName).ToList();
                        break;
                    case "jobgroup":
                        logs = isAscending
                            ? logs.OrderBy(l => l.JobGroup).ToList()
                            : logs.OrderByDescending(l => l.JobGroup).ToList();
                        break;
                    case "status":
                        logs = isAscending
                            ? logs.OrderBy(l => l.Status).ToList()
                            : logs.OrderByDescending(l => l.Status).ToList();
                        break;
                    case "createtime":
                        logs = isAscending
                            ? logs.OrderBy(l => l.CreateTime).ToList()
                            : logs.OrderByDescending(l => l.CreateTime).ToList();
                        break;
                    case "starttime":
                        logs = isAscending
                            ? logs.OrderBy(l => l.StartTime).ToList()
                            : logs.OrderByDescending(l => l.StartTime).ToList();
                        break;
                    case "endtime":
                        logs = isAscending
                            ? logs.OrderBy(l => l.EndTime).ToList()
                            : logs.OrderByDescending(l => l.EndTime).ToList();
                        break;
                    case "duration":
                        logs = isAscending
                            ? logs.OrderBy(l => l.Duration).ToList()
                            : logs.OrderByDescending(l => l.Duration).ToList();
                        break;
                    default:
                        // 默认按创建时间降序排序
                        logs = logs.OrderByDescending(l => l.CreateTime).ToList();
                        break;
                }
            }
            else
            {
                // 默认按创建时间降序排序
                logs = logs.OrderByDescending(l => l.CreateTime).ToList();
            }

            // 分页处理
            var totalCount = logs.Count;
            var pagedLogs = logs.Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

            // 构建分页响应
            return new PagedResponseDto<QuartzJobLog>
            {
                Items = pagedLogs,
                TotalCount = totalCount,
                PageIndex = queryDto.PageIndex,
                PageSize = queryDto.PageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业日志列表", ex);
            return new PagedResponseDto<QuartzJobLog>();
        }
    }

    /// <summary>
    /// 获取指定作业最近一次执行日志
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最近一次日志，不存在返回null</returns>
    public async Task<QuartzJobLog?> GetLatestJobLogAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var logs = await LoadLogsAsync();
            return logs
                .Where(l =>
                    l.JobName.Equals(jobName, StringComparison.OrdinalIgnoreCase)
                    && l.JobGroup.Equals(jobGroup, StringComparison.OrdinalIgnoreCase)
                )
                .OrderByDescending(l => l.CreateTime)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业最近一次日志", ex);
            return null;
        }
    }

    /// <summary>
    /// 清除指定天数之前的过期日志
    /// </summary>
    /// <param name="daysToKeep">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清除的日志数量</returns>
    public async Task<int> ClearExpiredLogsAsync(
        int daysToKeep,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var logs = await LoadLogsAsync(throwOnError: true);
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

            // 筛选过期日志
            var expiredLogs = logs.Where(l => l.CreateTime < cutoffDate).ToList();
            var expiredCount = expiredLogs.Count;

            if (expiredCount > 0)
            {
                // 删除过期日志
                foreach (var log in expiredLogs)
                {
                    logs.Remove(log);
                }

                // 保存更新后的日志列表
                await SaveLogsAsync(logs);
                _logger.LogSuccess("清理过期作业日志", $"共计: {expiredCount} 条");
            }

            return expiredCount;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理过期作业日志", ex);
            return 0;
        }
    }

    /// <summary>
    /// 清除超出最大保留条数的日志（仅保留最新的 maxCount 条）
    /// </summary>
    /// <param name="maxCount">最大保留条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清除的日志数量</returns>
    public async Task<int> ClearOverflowLogsAsync(
        int maxCount,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (maxCount <= 0)
            {
                return 0;
            }

            var logs = await LoadLogsAsync(throwOnError: true);
            if (logs.Count <= maxCount)
            {
                return 0;
            }

            var overflowCount = logs.Count - maxCount;
            var keptLogs = logs.OrderByDescending(l => l.CreateTime).Take(maxCount).ToList();
            await SaveLogsAsync(keptLogs);

            _logger.LogSuccess("清理超出保留条数的作业日志", $"共计: {overflowCount} 条");
            return overflowCount;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理超出保留条数的作业日志", ex);
            return 0;
        }
    }

    /// <summary>
    /// 根据查询条件清空作业日志
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空成功返回true，失败返回false</returns>
    public async Task<bool> ClearJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var logs = await LoadLogsAsync(throwOnError: true);
            var originalCount = logs.Count;

            // 找出需要删除的日志（即匹配查询条件的日志）
            var logsToDelete = logs.Where(log =>
                {
                    // 应用与GetJobLogsAsync相同的过滤条件
                    bool match = true;

                    if (!string.IsNullOrEmpty(queryDto.JobName))
                    {
                        match &= log.JobName.Contains(
                            queryDto.JobName,
                            StringComparison.OrdinalIgnoreCase
                        );
                    }

                    if (!string.IsNullOrEmpty(queryDto.JobGroup))
                    {
                        match &= log.JobGroup.Contains(
                            queryDto.JobGroup,
                            StringComparison.OrdinalIgnoreCase
                        );
                    }

                    if (queryDto.Status.HasValue)
                    {
                        match &= log.Status == queryDto.Status.Value;
                    }

                    if (queryDto.Statuses != null && queryDto.Statuses.Count > 0)
                    {
                        match &= queryDto.Statuses.Contains(log.Status);
                    }

                    if (queryDto.StartTime.HasValue)
                    {
                        match &= log.StartTime >= queryDto.StartTime.Value;
                    }

                    if (queryDto.EndTime.HasValue)
                    {
                        match &= log.StartTime <= queryDto.EndTime.Value;
                    }

                    return match;
                })
                .ToList();

            // 如果没有指定查询条件，删除所有日志
            if (
                string.IsNullOrEmpty(queryDto.JobName)
                && string.IsNullOrEmpty(queryDto.JobGroup)
                && !queryDto.Status.HasValue
                && (queryDto.Statuses == null || queryDto.Statuses.Count == 0)
                && !queryDto.StartTime.HasValue
                && !queryDto.EndTime.HasValue
            )
            {
                logsToDelete = logs;
            }

            // 手动清空是用户明确操作，包含中断记录在内的全部匹配日志都删除。
            // 补偿链保护只用于后台自动清理（ClearExpiredLogsAsync / ClearOverflowLogsAsync）。

            // 创建新的日志列表，不包含需要删除的日志
            var logsToKeep = logs.Except(logsToDelete).ToList();

            // 保存保留的日志（即删除了匹配条件的日志）
            await SaveLogsAsync(logsToKeep);

            // 计算清空的日志数量
            var clearedCount = logsToDelete.Count;
            _logger.LogSuccess("清理作业日志", $"共计: {clearedCount} 条");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理作业日志", ex);
            return false;
        }
    }

    #region 中断恢复支撑

    /// <summary>
    /// 按 LogId 获取单条日志
    /// </summary>
    public Task<QuartzJobLog?> GetJobLogAsync(
        Guid logId,
        CancellationToken cancellationToken = default
    )
    {
        var logs = LoadLogsCore();
        return Task.FromResult(logs.FirstOrDefault(l => l.LogId == logId));
    }

    /// <summary>
    /// 更新日志（收尾/补偿状态回写）
    /// </summary>
    public Task<bool> UpdateJobLogAsync(
        QuartzJobLog jobLog,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var logs = LoadLogsCore(throwOnError: true);
                var index = logs.FindIndex(l => l.LogId == jobLog.LogId);
                if (index < 0)
                {
                    _logger.LogWarn(
                        "更新作业日志",
                        $"未找到日志记录，收尾结果未写入: {jobLog.LogId}, 状态: {jobLog.Status}"
                    );
                    return Task.FromResult(false);
                }

                logs[index] = jobLog;
                SaveLogsCore(logs);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("更新作业日志", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 批量更新运行中日志的心跳时间
    /// </summary>
    public Task<int> UpdateJobLogHeartbeatsAsync(
        IReadOnlyCollection<Guid> logIds,
        DateTime heartbeatTime,
        CancellationToken cancellationToken = default
    )
    {
        if (logIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var idSet = logIds.ToHashSet();
                var logs = LoadLogsCore(throwOnError: true);
                var updated = 0;
                foreach (var log in logs)
                {
                    if (idSet.Contains(log.LogId) && log.Status == LogStatus.Running)
                    {
                        log.HeartbeatTime = heartbeatTime;
                        updated++;
                    }
                }

                if (updated > 0)
                {
                    SaveLogsCore(logs);
                }

                return Task.FromResult(updated);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("更新作业日志心跳", ex);
                return Task.FromResult(0);
            }
        }
    }

    /// <summary>
    /// 获取可恢复的日志：运行中且心跳过期，或已中断但补偿仍待处理
    /// </summary>
    public Task<List<QuartzJobLog>> GetRecoverableLogsAsync(
        DateTime staleBefore,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var logs = LoadLogsCore()
            .Where(l =>
                (
                    l.Status == LogStatus.Running
                    && (l.HeartbeatTime ?? l.StartTime) < staleBefore
                )
                || (
                    l.Status == LogStatus.Interrupted
                    && l.CompensationStatus == CompensationStatus.Pending
                )
            )
            .OrderBy(l => l.StartTime)
            .Take(limit <= 0 ? 200 : limit)
            .ToList();

        return Task.FromResult(logs);
    }

    /// <summary>
    /// 原子认领并标记中断：仅当状态仍为 Running 时成功
    /// </summary>
    public Task<bool> TryMarkInterruptedAsync(
        Guid logId,
        string instanceId,
        DateTime interruptedAt,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var logs = LoadLogsCore(throwOnError: true);
                var log = logs.FirstOrDefault(l => l.LogId == logId);
                if (log == null || log.Status != LogStatus.Running)
                {
                    return Task.FromResult(false);
                }

                var lastSeen = log.HeartbeatTime ?? log.StartTime;
                log.Status = LogStatus.Interrupted;
                log.InterruptedAt = interruptedAt;
                log.EndTime = lastSeen;
                log.Duration = Math.Max(0, (long)(lastSeen - log.StartTime).TotalMilliseconds);
                log.CompensationStatus = CompensationStatus.Pending;
                log.Message = reason;
                log.InstanceId = string.IsNullOrEmpty(log.InstanceId) ? instanceId : log.InstanceId;

                SaveLogsCore(logs);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("标记作业中断", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 原子抢占补偿权：仅当已被判定中断且尚未触发补偿时成功
    /// </summary>
    public Task<bool> TryClaimCompensationAsync(
        Guid logId,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var logs = LoadLogsCore(throwOnError: true);
                var log = logs.FirstOrDefault(l => l.LogId == logId);
                if (
                    log == null
                    || log.Status != LogStatus.Interrupted
                    || log.CompensationStatus == CompensationStatus.Triggered
                )
                {
                    return Task.FromResult(false);
                }

                log.CompensationStatus = CompensationStatus.Triggered;
                SaveLogsCore(logs);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("抢占补偿权", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 回写补偿结果
    /// </summary>
    public Task<bool> MarkCompensationResultAsync(
        Guid logId,
        CompensationStatus status,
        Guid? compensationLogId,
        string? reason,
        CancellationToken cancellationToken = default
    )
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var logs = LoadLogsCore(throwOnError: true);
                var log = logs.FirstOrDefault(l => l.LogId == logId);
                if (log == null)
                {
                    return Task.FromResult(false);
                }

                log.CompensationStatus = status;
                log.CompensationLogId = compensationLogId;
                log.SkipReason = reason;
                SaveLogsCore(logs);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogFailure("回写补偿结果", ex);
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// 清除过期日志（带中断日志独立保留天数，保护待补偿记录）
    /// </summary>
    public async Task<int> ClearExpiredLogsAsync(
        int daysToKeep,
        int interruptedDaysToKeep,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var logs = await LoadLogsAsync(throwOnError: true);
            var now = DateTime.Now;
            var cutoff = now.AddDays(-Math.Max(0, daysToKeep));
            var interruptedCutoff = interruptedDaysToKeep > 0
                ? now.AddDays(-interruptedDaysToKeep)
                : cutoff;

            var expiredLogs = logs
                .Where(l =>
                {
                    if (IsRecoveryProtected(l))
                    {
                        return false;
                    }

                    var isRecoveryChain =
                        l.Status == LogStatus.Interrupted
                        || l.CompensationLogId.HasValue
                        || l.RecoverySourceLogId.HasValue;
                    return l.CreateTime < (isRecoveryChain ? interruptedCutoff : cutoff);
                })
                .ToList();

            if (expiredLogs.Count == 0)
            {
                return 0;
            }

            foreach (var log in expiredLogs)
            {
                logs.Remove(log);
            }

            await SaveLogsAsync(logs);
            _logger.LogSuccess("清理过期作业日志", $"共计: {expiredLogs.Count} 条");
            return expiredLogs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理过期作业日志", ex);
            return 0;
        }
    }

    /// <summary>
    /// 清除超出最大保留条数的日志（可选择保护待补偿记录）
    /// </summary>
    public async Task<int> ClearOverflowLogsAsync(
        int maxCount,
        bool keepRecoveryChain,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (maxCount <= 0)
            {
                return 0;
            }

            var logs = await LoadLogsAsync(throwOnError: true);
            if (logs.Count <= maxCount)
            {
                return 0;
            }

            var keep = logs.OrderByDescending(l => l.CreateTime).Take(maxCount).ToList();
            if (keepRecoveryChain)
            {
                var protectedLogs = logs.Where(IsRecoveryProtected).ToList();
                keep = keep.Concat(protectedLogs).DistinctBy(l => l.LogId).ToList();
            }

            var removed = logs.Count - keep.Count;
            if (removed <= 0)
            {
                return 0;
            }

            await SaveLogsAsync(keep);
            _logger.LogSuccess("清理超出保留条数的作业日志", $"共计: {removed} 条");
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理超出保留条数的作业日志", ex);
            return 0;
        }
    }

    /// <summary>
    /// 是否属于必须保留的恢复链记录（待判定或待补偿）
    /// </summary>
    private static bool IsRecoveryProtected(QuartzJobLog log)
    {
        return log.Status == LogStatus.Interrupted
            && (
                log.CompensationStatus == CompensationStatus.None
                || log.CompensationStatus == CompensationStatus.Pending
            );
    }

    #endregion

    #endregion

    #region 文件操作

    /// <summary>
    /// 获取文件锁对象
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>锁对象</returns>
    private static object GetFileLock(string filePath)
    {
        return _fileLocks.GetOrAdd(filePath, _ => new object());
    }

    /// <summary>
    /// 初始化文件存储
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化成功返回true，失败返回false</returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查作业文件是否存在，不存在则创建
            if (!File.Exists(_jobsFilePath))
            {
                await SaveJobsAsync(new List<QuartzJobInfo>());
            }

            // 检查日志文件是否存在，不存在则创建
            if (!File.Exists(_logsFilePath))
            {
                await SaveLogsAsync(new List<QuartzJobLog>());
            }

            // 检查设置文件是否存在，不存在则创建
            if (!File.Exists(_settingsFilePath))
            {
                await SaveSettingsAsync(new List<QuartzSetting>());
            }

            // 检查通知文件是否存在，不存在则创建
            if (!File.Exists(_notificationsFilePath))
            {
                await SaveNotificationsAsync(new List<QuartzNotification>());
            }

            _logger.LogSuccess("初始化文件存储");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("初始化文件存储", ex);
            return false;
        }
    }

    /// <summary>
    /// 检查文件存储是否已初始化
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已初始化返回true，未初始化返回false</returns>
    public async Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default)
    {
        // 检查作业文件和日志文件是否都存在
        return File.Exists(_jobsFilePath) && File.Exists(_logsFilePath);
    }

    /// <summary>
    /// 从文件加载作业数据
    /// </summary>
    /// <returns>作业信息列表</returns>
    private Task<List<QuartzJobInfo>> LoadJobsAsync()
    {
        return Task.FromResult(LoadJobsCore());
    }

    /// <summary>
    /// 读取作业数据（不加锁：写入端使用临时文件原子替换，读到的始终是完整文件）
    /// </summary>
    private List<QuartzJobInfo> LoadJobsCore()
    {
        try
        {
            // 文件不存在则返回空列表
            if (!File.Exists(_jobsFilePath))
            {
                return new List<QuartzJobInfo>();
            }

            // 使用FileStream并设置FileShare参数，允许并发读取
            using var stream = new FileStream(
                _jobsFilePath,
                FileMode.Open,
                FileAccess.Read,
                // 允许其他进程/线程读写并允许后续原子替换删除该文件
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<List<QuartzJobInfo>>(
                    json,
                    JsonSerializationConfig.JsonOptions()
                ) ?? new List<QuartzJobInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("加载作业", ex);
            return new List<QuartzJobInfo>();
        }
    }

    /// <summary>
    /// 保存作业数据到文件
    /// </summary>
    /// <param name="jobs">作业信息列表</param>
    private Task SaveJobsAsync(List<QuartzJobInfo> jobs)
    {
        var lockObject = GetFileLock(_jobsFilePath);
        lock (lockObject)
        {
            try
            {
                SaveJobsCore(jobs);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogFailure("保存作业", ex);
                throw;
            }
        }
    }

    private void SaveJobsCore(List<QuartzJobInfo> jobs)
    {
        // 创建备份
        if (_options.EnableFileBackup && File.Exists(_jobsFilePath))
        {
            CreateBackup(_jobsFilePath);
        }

        // 序列化作业列表为JSON
        var json = JsonSerializer.Serialize(jobs, JsonSerializationConfig.JsonOptions());

        // 先写临时文件再原子替换，避免读取方看到写了一半的内容
        WriteFileAtomically(_jobsFilePath, json);
    }

    private Task<List<QuartzJobLog>> LoadLogsAsync(bool throwOnError = false)
    {
        return Task.FromResult(LoadLogsCore(throwOnError));
    }

    /// <summary>
    /// 读取日志数据（不加锁：写入端使用临时文件原子替换，读到的始终是完整文件）
    /// </summary>
    /// <param name="throwOnError">
    /// 读取失败时是否抛出异常。写操作必须传 true，否则会把「读失败」当成「没有数据」写回，
    /// 造成收尾丢失甚至整表被覆盖。查询类调用保持 false，由上层降级为空结果。
    /// </param>
    private List<QuartzJobLog> LoadLogsCore(bool throwOnError = false)
    {
        try
        {
            if (!File.Exists(_logsFilePath))
            {
                return new List<QuartzJobLog>();
            }

            // 使用FileStream并设置FileShare参数，允许并发读取
            using var stream = new FileStream(
                _logsFilePath,
                FileMode.Open,
                FileAccess.Read,
                // 允许其他进程/线程读写并允许后续原子替换删除该文件
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<List<QuartzJobLog>>(
                    json,
                    JsonSerializationConfig.JsonOptions()
                ) ?? new List<QuartzJobLog>();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("加载作业日志", ex);
            if (throwOnError)
            {
                throw;
            }

            return new List<QuartzJobLog>();
        }
    }

    private Task SaveLogsAsync(List<QuartzJobLog> logs)
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                SaveLogsCore(logs);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogFailure("保存作业日志", ex);
                throw;
            }
        }
    }

    private void SaveLogsCore(List<QuartzJobLog> logs)
    {
        var json = JsonSerializer.Serialize(logs, JsonSerializationConfig.JsonOptions());

        // 先写临时文件再原子替换，避免读取方看到写了一半的内容
        WriteFileAtomically(_logsFilePath, json);
    }

    /// <summary>
    /// 原子写入文件：先写同目录临时文件，再替换目标文件，读取方不会读到写入中的内容
    /// </summary>
    private static void WriteFileAtomically(string filePath, string content)
    {
        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }

            File.Move(tempPath, filePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // 临时文件清理失败不影响主流程
                }
            }
        }
    }

    private void CreateBackup(string filePath)
    {
        try
        {
            // 检查备份间隔配置，如果小于等于0，则每次都备份
            if (_options.BackupIntervalSeconds > 0)
            {
                // 获取最后备份时间
                var lastBackupTime = _lastBackupTimes.GetOrAdd(filePath, DateTime.MinValue);
                var now = DateTime.Now;

                // 如果距离上次备份的时间小于配置的间隔，则跳过本次备份
                if ((now - lastBackupTime).TotalSeconds < _options.BackupIntervalSeconds)
                {
                    return;
                }
            }

            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName =
                $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
            var backupFilePath = Path.Combine(_options.FileBackupPath, backupFileName);

            File.Copy(filePath, backupFilePath, true);

            // 更新最后备份时间
            _lastBackupTimes[filePath] = DateTime.Now;

            // 清理旧备份
            CleanupOldBackups();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("创建备份", ex);
        }
    }

    private void CleanupOldBackups()
    {
        try
        {
            var backupFiles = Directory
                .GetFiles(_options.FileBackupPath, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            if (backupFiles.Count > _options.MaxBackupFiles)
            {
                var filesToDelete = backupFiles.Skip(_options.MaxBackupFiles).ToList();
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }

                _logger.LogSuccess("清理旧备份", "共计 {count} 个", filesToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清理旧备份", ex);
        }
    }

    #endregion

    #region 统计分析

    /// <summary>
    /// 获取统计分析概览聚合数据（合并作业统计+状态分布+执行趋势+热力图）
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计分析概览聚合数据</returns>
    public async Task<AnalyticsOverviewDto> GetAnalyticsOverviewAsync(
        StatsQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 一次加载作业，同时生成Stats和StatusDistribution
            var jobs = await LoadJobsAsync();
            var totalJobs = jobs.Count;
            var enabledJobs = jobs.Count(j => j.IsEnabled);
            var disabledJobs = jobs.Count(j => !j.IsEnabled);

            // 按状态分组统计
            var statusGroups = jobs
                .GroupBy(j => j.Status)
                .Select(group => new JobStatusDistributionDto
                {
                    Status = group.Key.ToString(),
                    Count = group.Count(),
                    Percentage = totalJobs > 0 ? Math.Round((double)group.Count() / totalJobs * 100, 2) : 0,
                })
                .ToList();

            // 一次加载日志，同时生成ExecutionTrend和ExecutionHeatmap
            var logs = await LoadLogsAsync();
            var filteredLogs = logs.Where(log => log.StartTime >= startTime && log.StartTime <= endTime).ToList();

            // 按小时分组统计（执行趋势）
            var trend = filteredLogs
                .GroupBy(l => new DateTime(l.StartTime.Year, l.StartTime.Month, l.StartTime.Day, l.StartTime.Hour, 0, 0))
                .Select(group => new JobExecutionTrendDto
                {
                    Time = group.Key.ToString("yyyy-MM-dd HH:00"),
                    SuccessCount = group.Count(l => l.Status == LogStatus.Success),
                    FailedCount = group.Count(l => l.Status == LogStatus.Failed),
                    TotalCount = group.Count(),
                })
                .OrderBy(t => t.Time)
                .ToList();

            // 按星期×小时分组统计（热力图）
            var heatmap = filteredLogs
                .GroupBy(l => new
                {
                    DayOfWeek = ((int)l.StartTime.DayOfWeek + 6) % 7 + 1,
                    Hour = l.StartTime.Hour,
                })
                .Select(g => new JobExecutionHeatmapDto
                {
                    DayOfWeek = g.Key.DayOfWeek,
                    Hour = g.Key.Hour,
                    Count = g.Count(),
                    SuccessCount = g.Count(l => l.Status == LogStatus.Success),
                    FailedCount = g.Count(l => l.Status == LogStatus.Failed),
                })
                .ToList();

            // 统计日志信息
            var successCount = filteredLogs.Count(l => l.Status == LogStatus.Success);
            var failedCount = filteredLogs.Count(l => l.Status == LogStatus.Failed);

            return new AnalyticsOverviewDto
            {
                Stats = new JobStatsDto
                {
                    TotalJobs = totalJobs,
                    EnabledJobs = enabledJobs,
                    DisabledJobs = disabledJobs,
                    TotalExecutions = successCount + failedCount,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                },
                StatusDistribution = statusGroups,
                ExecutionTrend = trend,
                ExecutionHeatmap = heatmap,
            };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取统计分析概览聚合数据", ex);
            return new AnalyticsOverviewDto();
        }
    }

    /// <summary>
    /// 获取作业性能分析聚合数据（合并健康概览+耗时排行）
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="topCount">耗时排行取前N条，默认10</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业性能分析聚合数据</returns>
    public async Task<AnalyticsJobPerformanceDto> GetAnalyticsJobPerformanceAsync(
        StatsQueryDto queryDto,
        int topCount = 10,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var (startTime, endTime) = CalculateTimeRange(queryDto);
            var jobs = await LoadJobsAsync();
            var logs = await LoadLogsAsync();

            var filteredLogs = logs.Where(l => l.StartTime >= startTime && l.StartTime <= endTime).ToList();

            // 按作业分组聚合
            var logGroups = filteredLogs
                .GroupBy(l => new { l.JobName, l.JobGroup })
                .Select(g => new
                {
                    g.Key.JobName,
                    g.Key.JobGroup,
                    ExecutionCount = g.Count(),
                    SuccessCount = g.Count(l => l.Status == LogStatus.Success),
                    DurationLogs = g.Where(l => l.Duration.HasValue).ToList(),
                    AvgDuration = g.Where(l => l.Duration.HasValue).Any()
                        ? g.Where(l => l.Duration.HasValue).Average(l => (double)l.Duration!.Value)
                        : 0,
                    MaxDuration = g.Where(l => l.Duration.HasValue).Any()
                        ? g.Where(l => l.Duration.HasValue).Max(l => (double)l.Duration!.Value)
                        : 0,
                    MinDuration = g.Where(l =>
                            l.Duration.HasValue
                            && l.Status != LogStatus.Running
                            && l.Status != LogStatus.Interrupted
                        )
                        .Any()
                        ? g.Where(l =>
                                l.Duration.HasValue
                                && l.Status != LogStatus.Running
                                && l.Status != LogStatus.Interrupted
                            )
                            .Min(l => (double)l.Duration!.Value)
                        : 0,
                    LastExecutionTime = g.Max(l => l.StartTime),
                })
                .ToList();

            // 生成健康概览数据
            var healthOverview = jobs.Select(job =>
            {
                var jobLog = logGroups.FirstOrDefault(l =>
                    l.JobName == job.JobName && l.JobGroup == job.JobGroup
                );
                var execCount = jobLog?.ExecutionCount ?? 0;
                var successCount = jobLog?.SuccessCount ?? 0;

                return new JobHealthDto
                {
                    JobName = job.JobName,
                    JobGroup = job.JobGroup,
                    Status = job.Status.ToString(),
                    IsEnabled = job.IsEnabled,
                    SuccessRate = execCount > 0 ? Math.Round((double)successCount / execCount * 100, 1) : 0,
                    AvgDuration = Math.Round(jobLog?.AvgDuration ?? 0, 0),
                    MaxDuration = Math.Round(jobLog?.MaxDuration ?? 0, 0),
                    ExecutionCount = execCount,
                    LastExecutionTime = jobLog?.LastExecutionTime,
                    CronExpression = job.CronExpression,
                };
            }).ToList();

            // 生成耗时排行数据（复用同一批logGroups，按平均耗时降序取Top）
            var topSlowJobs = logGroups
                .Where(l => l.AvgDuration > 0)
                .OrderByDescending(l => l.AvgDuration)
                .Take(topCount)
                .Select(l => new TopSlowJobDto
                {
                    JobName = l.JobName,
                    JobGroup = l.JobGroup,
                    AvgDuration = Math.Round(l.AvgDuration, 0),
                    MaxDuration = Math.Round(l.MaxDuration, 0),
                    MinDuration = Math.Round(l.MinDuration, 0),
                    ExecutionCount = l.ExecutionCount,
                    SuccessRate = l.ExecutionCount > 0 ? Math.Round((double)l.SuccessCount / l.ExecutionCount * 100, 1) : 0,
                    LastExecutionTime = l.LastExecutionTime,
                })
                .ToList();

            return new AnalyticsJobPerformanceDto
            {
                JobHealthOverview = healthOverview,
                TopSlowJobs = topSlowJobs,
            };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业性能分析聚合数据", ex);
            return new AnalyticsJobPerformanceDto();
        }
    }

    /// <summary>
    /// 计算时间范围
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <returns>开始时间和结束时间</returns>
    private (DateTime StartTime, DateTime EndTime) CalculateTimeRange(StatsQueryDto queryDto)
    {
        DateTime startTime,
            endTime;

        // 如果指定了自定义时间范围，使用自定义时间
        if (
            queryDto.TimeRangeType == "custom"
            && queryDto.StartTime.HasValue
            && queryDto.EndTime.HasValue
        )
        {
            startTime = queryDto.StartTime.Value;
            endTime = queryDto.EndTime.Value;
        }
        else
        {
            // 否则根据时间范围类型计算
            endTime = DateTime.Now;

            switch (queryDto.TimeRangeType)
            {
                case "today":
                    startTime = new DateTime(endTime.Year, endTime.Month, endTime.Day, 0, 0, 0);
                    break;
                case "yesterday":
                    startTime = new DateTime(
                        endTime.Year,
                        endTime.Month,
                        endTime.Day,
                        0,
                        0,
                        0
                    ).AddDays(-1);
                    endTime = new DateTime(
                        endTime.Year,
                        endTime.Month,
                        endTime.Day,
                        0,
                        0,
                        0
                    ).AddMilliseconds(-1);
                    break;
                case "thisWeek":
                    startTime = endTime.AddDays(-(int)endTime.DayOfWeek).Date;
                    break;
                case "thisMonth":
                    startTime = new DateTime(endTime.Year, endTime.Month, 1, 0, 0, 0);
                    break;
                case "last30Days":
                    startTime = endTime.AddDays(-30);
                    break;
                default:
                    startTime = endTime.AddDays(-7); // 默认最近7天
                    break;
            }
        }

        return (startTime, endTime);
    }

    #endregion

    #region 通知管理

    #region 设置操作

    /// <summary>
    /// 从文件加载设置数据
    /// </summary>
    /// <returns>设置信息列表</returns>
    private Task<List<QuartzSetting>> LoadSettingsAsync()
    {
        try
        {
            // 读取不加锁：写入端使用临时文件原子替换，读到的始终是完整文件
            if (!File.Exists(_settingsFilePath))
            {
                return Task.FromResult(new List<QuartzSetting>());
            }

            using var stream = new FileStream(
                _settingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                // 允许其他进程/线程读写并允许后续原子替换删除该文件
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return Task.FromResult(
                JsonSerializer.Deserialize<List<QuartzSetting>>(
                    json,
                    JsonSerializationConfig.JsonOptions()
                ) ?? new List<QuartzSetting>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("加载设置数据", ex);
            return Task.FromResult(new List<QuartzSetting>());
        }
    }

    /// <summary>
    /// 保存设置数据到文件
    /// </summary>
    /// <param name="settings">设置信息列表</param>
    private Task SaveSettingsAsync(List<QuartzSetting> settings)
    {
        var lockObject = GetFileLock(_settingsFilePath);
        lock (lockObject)
        {
            try
            {
                // 创建备份
                if (_options.EnableFileBackup && File.Exists(_settingsFilePath))
                {
                    CreateBackup(_settingsFilePath);
                }

                var json = JsonSerializer.Serialize(
                    settings,
                    JsonSerializationConfig.JsonOptions()
                );

                // 先写临时文件再原子替换，避免读取方看到写了一半的内容
                WriteFileAtomically(_settingsFilePath, json);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogFailure("保存设置", ex);
                throw;
            }
        }
    }

    #endregion

    #region 通知操作

    /// <summary>
    /// 从文件加载通知数据
    /// </summary>
    /// <returns>通知信息列表</returns>
    private Task<List<QuartzNotification>> LoadNotificationsAsync()
    {
        try
        {
            // 读取不加锁：写入端使用临时文件原子替换，读到的始终是完整文件
            if (!File.Exists(_notificationsFilePath))
            {
                return Task.FromResult(new List<QuartzNotification>());
            }

            using var stream = new FileStream(
                _notificationsFilePath,
                FileMode.Open,
                FileAccess.Read,
                // 允许其他进程/线程读写并允许后续原子替换删除该文件
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return Task.FromResult(
                JsonSerializer.Deserialize<List<QuartzNotification>>(
                    json,
                    JsonSerializationConfig.JsonOptions()
                ) ?? new List<QuartzNotification>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("加载通知数据", ex);
            return Task.FromResult(new List<QuartzNotification>());
        }
    }

    /// <summary>
    /// 保存通知数据到文件
    /// </summary>
    /// <param name="notifications">通知信息列表</param>
    private Task SaveNotificationsAsync(List<QuartzNotification> notifications)
    {
        var lockObject = GetFileLock(_notificationsFilePath);
        lock (lockObject)
        {
            try
            {
                // 创建备份
                if (_options.EnableFileBackup && File.Exists(_notificationsFilePath))
                {
                    CreateBackup(_notificationsFilePath);
                }

                var json = JsonSerializer.Serialize(
                    notifications,
                    JsonSerializationConfig.JsonOptions()
                );

                // 先写临时文件再原子替换，避免读取方看到写了一半的内容
                WriteFileAtomically(_notificationsFilePath, json);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogFailure("保存通知数据", ex);
                throw;
            }
        }
    }

    #endregion

    #region 接口实现

    /// <summary>
    /// 保存设置
    /// </summary>
    /// <param name="setting">设置信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存成功返回true，失败返回false</returns>
    public async Task<bool> SaveSettingAsync(
        QuartzSetting setting,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var settings = await LoadSettingsAsync();
            var existingSetting = settings.FirstOrDefault(s => s.Key == setting.Key);

            if (existingSetting != null)
            {
                // 更新现有设置
                existingSetting.Value = setting.Value;
                existingSetting.Description = setting.Description;
                existingSetting.Enabled = setting.Enabled;
                existingSetting.UpdateTime = DateTime.Now;
            }
            else
            {
                // 添加新设置
                setting.CreateTime = DateTime.Now;
                settings.Add(setting);
            }

            await SaveSettingsAsync(settings);
            _logger.LogSuccess("保存设置", setting.Key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("保存设置", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取设置
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置信息，不存在返回null</returns>
    public async Task<QuartzSetting?> GetSettingAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var settings = await LoadSettingsAsync();
            return settings.FirstOrDefault(s => s.Key == key);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取设置", ex);
            return null;
        }
    }

    /// <summary>
    /// 获取所有设置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置列表</returns>
    public async Task<List<QuartzSetting>> GetAllSettingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取所有设置", ex);
            return new List<QuartzSetting>();
        }
    }

    /// <summary>
    /// 添加通知消息
    /// </summary>
    /// <param name="notification">通知消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加成功返回true，失败返回false</returns>
    public async Task<bool> AddNotificationAsync(
        QuartzNotification notification,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();
            notification.NotificationId = Guid.NewGuid();
            notification.CreateTime = DateTime.Now;
            notifications.Add(notification);
            await SaveNotificationsAsync(notifications);
            _logger.LogSuccess("添加通知消息", notification.NotificationId.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("添加通知消息", ex);
            return false;
        }
    }

    /// <summary>
    /// 更新通知消息
    /// </summary>
    /// <param name="notification">通知消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回true，失败返回false</returns>
    public async Task<bool> UpdateNotificationAsync(
        QuartzNotification notification,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();
            var existingNotification = notifications.FirstOrDefault(n =>
                n.NotificationId == notification.NotificationId
            );

            if (existingNotification == null)
            {
                _logger.LogWarn("更新通知消息", $"通知: {notification.NotificationId} 不存在");
                return false;
            }

            // 更新通知信息
            existingNotification.Title = notification.Title;
            existingNotification.Content = notification.Content;
            existingNotification.Status = notification.Status;
            existingNotification.ErrorMessage = notification.ErrorMessage;
            existingNotification.TriggeredBy = notification.TriggeredBy;
            existingNotification.SendTime = notification.SendTime;
            existingNotification.Duration = notification.Duration;

            await SaveNotificationsAsync(notifications);
            _logger.LogSuccess("更新通知消息", notification.NotificationId.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("更新通知消息", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息，不存在返回null</returns>
    public async Task<QuartzNotification?> GetNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();
            return notifications.FirstOrDefault(n => n.NotificationId == notificationId);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息", ex);
            return null;
        }
    }

    /// <summary>
    /// 获取通知消息列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页通知消息列表</returns>
    public async Task<PagedResponseDto<QuartzNotification>> GetNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();

            // 应用过滤条件
            if (queryDto.Status.HasValue)
            {
                notifications = notifications
                    .Where(n => n.Status == queryDto.Status.Value)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.TriggeredBy))
            {
                notifications = notifications
                    .Where(n =>
                        n.TriggeredBy?.Contains(
                            queryDto.TriggeredBy,
                            StringComparison.OrdinalIgnoreCase
                        ) ?? false
                    )
                    .ToList();
            }

            if (queryDto.StartTime.HasValue)
            {
                notifications = notifications
                    .Where(n => n.CreateTime >= queryDto.StartTime.Value)
                    .ToList();
            }

            if (queryDto.EndTime.HasValue)
            {
                notifications = notifications
                    .Where(n => n.CreateTime <= queryDto.EndTime.Value)
                    .ToList();
            }

            // 应用排序
            if (!string.IsNullOrEmpty(queryDto.SortBy) && !string.IsNullOrEmpty(queryDto.SortOrder))
            {
                var isAscending = queryDto.SortOrder.ToLower().StartsWith("asc");

                switch (queryDto.SortBy.ToLower())
                {
                    case "title":
                        notifications = isAscending
                            ? notifications.OrderBy(n => n.Title).ToList()
                            : notifications.OrderByDescending(n => n.Title).ToList();
                        break;
                    case "status":
                        notifications = isAscending
                            ? notifications.OrderBy(n => n.Status).ToList()
                            : notifications.OrderByDescending(n => n.Status).ToList();
                        break;
                    case "createtime":
                        notifications = isAscending
                            ? notifications.OrderBy(n => n.CreateTime).ToList()
                            : notifications.OrderByDescending(n => n.CreateTime).ToList();
                        break;
                    case "sendtime":
                        notifications = isAscending
                            ? notifications.OrderBy(n => n.SendTime).ToList()
                            : notifications.OrderByDescending(n => n.SendTime).ToList();
                        break;
                    default:
                        // 默认按创建时间降序排序
                        notifications = notifications.OrderByDescending(n => n.CreateTime).ToList();
                        break;
                }
            }
            else
            {
                // 默认按创建时间降序排序
                notifications = notifications.OrderByDescending(n => n.CreateTime).ToList();
            }

            // 分页处理
            var totalCount = notifications.Count;
            var pagedNotifications = notifications
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

            // 构建分页响应
            return new PagedResponseDto<QuartzNotification>
            {
                Items = pagedNotifications,
                TotalCount = totalCount,
                PageIndex = queryDto.PageIndex,
                PageSize = queryDto.PageSize,
            };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息列表", ex);
            return new PagedResponseDto<QuartzNotification>();
        }
    }

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回true，失败返回false</returns>
    public async Task<bool> DeleteNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();
            var notificationToDelete = notifications.FirstOrDefault(n =>
                n.NotificationId == notificationId
            );

            if (notificationToDelete == null)
            {
                _logger.LogWarn("删除通知消息", "通知Id {notificationId} 不存在", notificationId);
                return false;
            }

            notifications.Remove(notificationToDelete);
            await SaveNotificationsAsync(notifications);
            _logger.LogSuccess("删除通知消息", notificationId.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("删除通知消息", ex);
            return false;
        }
    }

    /// <summary>
    /// 根据查询条件清空通知消息
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空成功返回true，失败返回false</returns>
    public async Task<bool> ClearNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notifications = await LoadNotificationsAsync();
            var originalCount = notifications.Count;

            // 找出需要删除的通知（即匹配查询条件的通知）
            var notificationsToDelete = notifications
                .Where(notification =>
                {
                    // 应用过滤条件
                    bool match = true;

                    if (queryDto.Status.HasValue)
                    {
                        match &= notification.Status == queryDto.Status.Value;
                    }

                    if (!string.IsNullOrEmpty(queryDto.TriggeredBy))
                    {
                        match &=
                            notification.TriggeredBy?.Contains(
                                queryDto.TriggeredBy,
                                StringComparison.OrdinalIgnoreCase
                            ) ?? false;
                    }

                    if (queryDto.StartTime.HasValue)
                    {
                        match &= notification.CreateTime >= queryDto.StartTime.Value;
                    }

                    if (queryDto.EndTime.HasValue)
                    {
                        match &= notification.CreateTime <= queryDto.EndTime.Value;
                    }

                    return match;
                })
                .ToList();

            // 如果没有指定查询条件，删除所有通知
            if (
                !queryDto.Status.HasValue
                && string.IsNullOrEmpty(queryDto.TriggeredBy)
                && !queryDto.StartTime.HasValue
                && !queryDto.EndTime.HasValue
            )
            {
                notificationsToDelete = notifications;
            }

            // 创建新的通知列表，不包含需要删除的通知
            var notificationsToKeep = notifications.Except(notificationsToDelete).ToList();

            // 保存保留的通知（即删除了匹配条件的通知）
            await SaveNotificationsAsync(notificationsToKeep);

            // 计算清空的通知数量
            var clearedCount = notificationsToDelete.Count;
            _logger.LogSuccess("清空通知消息", "共清空 {clearedCount} 条通知", clearedCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清空通知消息", ex);
            return false;
        }
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量添加作业（跳过已存在的作业）
    /// </summary>
    /// <param name="jobs">作业信息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功插入的数量</returns>
    public async Task<int> AddJobsBatchAsync(
        List<QuartzJobInfo> jobs,
        CancellationToken cancellationToken = default
    )
    {
        if (jobs == null || jobs.Count == 0)
            return 0;

        try
        {
            var existingJobs = await LoadJobsAsync();
            var existingKeys = new HashSet<string>(
                existingJobs.Select(j => $"{j.JobName}|{j.JobGroup}")
            );

            var newJobs = jobs
                .Where(j => !existingKeys.Contains($"{j.JobName}|{j.JobGroup}"))
                .ToList();

            if (newJobs.Count == 0)
                return 0;

            existingJobs.AddRange(newJobs);
            await SaveJobsAsync(existingJobs);

            _logger.LogSuccess("批量添加作业", "成功插入 {Count} 条，跳过 {Skipped} 条",
                newJobs.Count, jobs.Count - newJobs.Count);
            return newJobs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("批量添加作业", ex);
            return 0;
        }
    }

    /// <summary>
    /// 批量添加作业日志（跳过已存在的日志）
    /// </summary>
    /// <param name="logs">作业日志列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功插入的数量</returns>
    public async Task<int> AddJobLogsBatchAsync(
        List<QuartzJobLog> logs,
        CancellationToken cancellationToken = default
    )
    {
        if (logs == null || logs.Count == 0)
            return 0;

        try
        {
            var existingLogs = await LoadLogsAsync(throwOnError: true);
            var existingIds = new HashSet<Guid>(existingLogs.Select(l => l.LogId));

            var newLogs = logs.Where(l => !existingIds.Contains(l.LogId)).ToList();

            if (newLogs.Count == 0)
                return 0;

            existingLogs.AddRange(newLogs);
            await SaveLogsAsync(existingLogs);

            _logger.LogSuccess("批量添加日志", "成功插入 {Count} 条，跳过 {Skipped} 条",
                newLogs.Count, logs.Count - newLogs.Count);
            return newLogs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("批量添加日志", ex);
            return 0;
        }
    }

    /// <summary>
    /// 批量保存设置（存在则更新，不存在则添加）
    /// </summary>
    /// <param name="settings">设置列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功保存的数量</returns>
    public async Task<int> SaveSettingsBatchAsync(
        List<QuartzSetting> settings,
        CancellationToken cancellationToken = default
    )
    {
        if (settings == null || settings.Count == 0)
            return 0;

        try
        {
            var existingSettings = await LoadSettingsAsync();
            var existingDict = existingSettings.ToDictionary(s => s.Key);

            foreach (var setting in settings)
            {
                if (existingDict.TryGetValue(setting.Key, out var existing))
                {
                    existing.Value = setting.Value;
                    existing.Description = setting.Description;
                    existing.Enabled = setting.Enabled;
                    existing.UpdateTime = DateTime.Now;
                }
                else
                {
                    setting.CreateTime = DateTime.Now;
                    existingSettings.Add(setting);
                }
            }

            await SaveSettingsAsync(existingSettings);

            _logger.LogSuccess("批量保存设置", "成功保存 {Count} 条", settings.Count);
            return settings.Count;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("批量保存设置", ex);
            return 0;
        }
    }

    /// <summary>
    /// 批量添加通知消息（跳过已存在的通知）
    /// </summary>
    /// <param name="notifications">通知消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功插入的数量</returns>
    public async Task<int> AddNotificationsBatchAsync(
        List<QuartzNotification> notifications,
        CancellationToken cancellationToken = default
    )
    {
        if (notifications == null || notifications.Count == 0)
            return 0;

        try
        {
            var existingNotifications = await LoadNotificationsAsync();
            var existingIds = new HashSet<Guid>(
                existingNotifications.Select(n => n.NotificationId)
            );

            var newNotifications = notifications
                .Where(n => !existingIds.Contains(n.NotificationId))
                .ToList();

            if (newNotifications.Count == 0)
                return 0;

            existingNotifications.AddRange(newNotifications);
            await SaveNotificationsAsync(existingNotifications);

            _logger.LogSuccess("批量添加通知", "成功插入 {Count} 条，跳过 {Skipped} 条",
                newNotifications.Count, notifications.Count - newNotifications.Count);
            return newNotifications.Count;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("批量添加通知", ex);
            return 0;
        }
    }

    #endregion

    #endregion
}