using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// 文件存储实现类，实现了IJobStorage接口
/// 负责将作业信息和执行日志存储到本地文件中
/// </summary>
public class FileJobStorage : IJobStorage
{
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
    /// 文件锁字典
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> _fileLocks = new();


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
        _jobsFilePath = Path.Combine(_options.FileStoragePath, "jobs.json");
        _logsFilePath = Path.Combine(_options.FileStoragePath, "logs.json");

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
    public async Task<bool> AddJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            jobs.Add(jobInfo);
            await SaveJobsAsync(jobs);

            _logger.LogInformation("作业添加成功: {JobKey}", jobInfo.GetJobKey());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业失败: {JobKey}", jobInfo.GetJobKey());
            return false;
        }
    }

    /// <summary>
    /// 更新现有作业信息
    /// </summary>
    /// <param name="jobInfo">作业信息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回true，失败返回false</returns>
    public async Task<bool> UpdateJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var existingJob = jobs.FirstOrDefault(j => j.JobName == jobInfo.JobName && j.JobGroup == jobInfo.JobGroup);

            if (existingJob == null)
            {
                return false;
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
            existingJob.SkipSslValidation = jobInfo.SkipSslValidation;
            existingJob.StartTime = jobInfo.StartTime;
            existingJob.EndTime = jobInfo.EndTime;
            existingJob.IsEnabled = jobInfo.IsEnabled;
            existingJob.Status = jobInfo.Status;
            existingJob.NextRunTime = jobInfo.NextRunTime;
            existingJob.PreviousRunTime = jobInfo.PreviousRunTime;
            existingJob.UpdateTime = jobInfo.UpdateTime;
            existingJob.UpdateBy = jobInfo.UpdateBy;

            await SaveJobsAsync(jobs);

            _logger.LogInformation("作业更新成功: {JobKey}", jobInfo.GetJobKey());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业失败: {JobKey}", jobInfo.GetJobKey());
            return false;
        }
    }

    /// <summary>
    /// 删除指定作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回true，失败返回false</returns>
    public async Task<bool> DeleteJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var jobToRemove = jobs.FirstOrDefault(j => j.JobName == jobName && j.JobGroup == jobGroup);

            if (jobToRemove == null)
            {
                return false;
            }

            jobs.Remove(jobToRemove);
            await SaveJobsAsync(jobs);

            _logger.LogInformation("作业删除成功: {JobKey}", $"{jobGroup}.{jobName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除作业失败: {JobKey}", $"{jobGroup}.{jobName}");
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
    public async Task<QuartzJobInfo?> GetJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            return jobs.FirstOrDefault(j => j.JobName.Equals(jobName, StringComparison.CurrentCultureIgnoreCase) && j.JobGroup == jobGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return null;
        }
    }

    /// <summary>
    /// 获取作业列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页作业列表</returns>
    public async Task<PagedResponseDto<QuartzJobInfo>> GetJobsAsync(QuartzJobQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                jobs = jobs.Where(j => j.JobName.Contains(queryDto.JobName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                jobs = jobs.Where(j => j.JobGroup.Contains(queryDto.JobGroup, StringComparison.OrdinalIgnoreCase)).ToList();
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
            if (!string.IsNullOrEmpty(queryDto.SortBy))
            {
                var isAscending = string.Equals(queryDto.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        jobs = isAscending ? jobs.OrderBy(j => j.JobName).ToList() : jobs.OrderByDescending(j => j.JobName).ToList();
                        break;
                    case "jobgroup":
                        jobs = isAscending ? jobs.OrderBy(j => j.JobGroup).ToList() : jobs.OrderByDescending(j => j.JobGroup).ToList();
                        break;
                    case "status":
                        jobs = isAscending ? jobs.OrderBy(j => j.Status).ToList() : jobs.OrderByDescending(j => j.Status).ToList();
                        break;
                    case "isenabled":
                        jobs = isAscending ? jobs.OrderBy(j => j.IsEnabled).ToList() : jobs.OrderByDescending(j => j.IsEnabled).ToList();
                        break;
                    case "createtime":
                        jobs = isAscending ? jobs.OrderBy(j => j.CreateTime).ToList() : jobs.OrderByDescending(j => j.CreateTime).ToList();
                        break;
                    case "updatetime":
                        jobs = isAscending ? jobs.OrderBy(j => j.UpdateTime).ToList() : jobs.OrderByDescending(j => j.UpdateTime).ToList();
                        break;
                    case "previousruntime":
                        jobs = isAscending ? jobs.OrderBy(j => j.PreviousRunTime).ToList() : jobs.OrderByDescending(j => j.PreviousRunTime).ToList();
                        break;
                    case "nextruntime":
                        jobs = isAscending ? jobs.OrderBy(j => j.NextRunTime).ToList() : jobs.OrderByDescending(j => j.NextRunTime).ToList();
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
            var pagedJobs = jobs
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

            // 构建分页响应
            return new PagedResponseDto<QuartzJobInfo>
            {
                Items = pagedJobs,
                TotalCount = totalCount,
                PageIndex = queryDto.PageIndex,
                PageSize = queryDto.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业列表失败");
            return new PagedResponseDto<QuartzJobInfo>();
        }
    }

    /// <summary>
    /// 获取所有作业信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业信息列表</returns>
    public async Task<List<QuartzJobInfo>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有作业失败");
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
    public async Task<bool> UpdateJobStatusAsync(string jobName, string jobGroup, JobStatus status, CancellationToken cancellationToken = default)
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

            _logger.LogInformation("作业状态更新成功: {JobKey}, 状态: {Status}", $"{jobGroup}.{jobName}", status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业状态失败: {JobKey}", $"{jobGroup}.{jobName}");
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
    public async Task<bool> AddJobLogAsync(QuartzJobLog jobLog, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();
            logs.Add(jobLog);
            await SaveLogsAsync(logs);

            _logger.LogInformation("作业日志添加成功: {LogId}", jobLog.LogId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业日志失败");
            return false;
        }
    }

    /// <summary>
    /// 获取作业执行日志列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页日志列表</returns>
    public async Task<PagedResponseDto<QuartzJobLog>> GetJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                logs = logs.Where(l => l.JobName.Contains(queryDto.JobName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                logs = logs.Where(l => l.JobGroup.Contains(queryDto.JobGroup, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (queryDto.Status.HasValue)
            {
                logs = logs.Where(l => l.Status == queryDto.Status.Value).ToList();
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
            if (!string.IsNullOrEmpty(queryDto.SortBy))
            {
                var isAscending = string.Equals(queryDto.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        logs = isAscending ? logs.OrderBy(l => l.JobName).ToList() : logs.OrderByDescending(l => l.JobName).ToList();
                        break;
                    case "jobgroup":
                        logs = isAscending ? logs.OrderBy(l => l.JobGroup).ToList() : logs.OrderByDescending(l => l.JobGroup).ToList();
                        break;
                    case "status":
                        logs = isAscending ? logs.OrderBy(l => l.Status).ToList() : logs.OrderByDescending(l => l.Status).ToList();
                        break;
                    case "createtime":
                        logs = isAscending ? logs.OrderBy(l => l.CreateTime).ToList() : logs.OrderByDescending(l => l.CreateTime).ToList();
                        break;
                    case "starttime":
                        logs = isAscending ? logs.OrderBy(l => l.StartTime).ToList() : logs.OrderByDescending(l => l.StartTime).ToList();
                        break;
                    case "endtime":
                        logs = isAscending ? logs.OrderBy(l => l.EndTime).ToList() : logs.OrderByDescending(l => l.EndTime).ToList();
                        break;
                    case "duration":
                        logs = isAscending ? logs.OrderBy(l => l.Duration).ToList() : logs.OrderByDescending(l => l.Duration).ToList();
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
            var pagedLogs = logs
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

            // 构建分页响应
            return new PagedResponseDto<QuartzJobLog>
            {
                Items = pagedLogs,
                TotalCount = totalCount,
                PageIndex = queryDto.PageIndex,
                PageSize = queryDto.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业日志失败");
            return new PagedResponseDto<QuartzJobLog>();
        }
    }

    /// <summary>
    /// 清除指定天数之前的过期日志
    /// </summary>
    /// <param name="daysToKeep">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清除的日志数量</returns>
    public async Task<int> ClearExpiredLogsAsync(int daysToKeep, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();
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
                _logger.LogInformation("清除过期日志成功: {Count} 条", expiredCount);
            }

            return expiredCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除过期日志失败");
            return 0;
        }
    }

    /// <summary>
    /// 根据查询条件清空作业日志
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空成功返回true，失败返回false</returns>
    public async Task<bool> ClearJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();
            var originalCount = logs.Count;

            // 找出需要删除的日志（即匹配查询条件的日志）
            var logsToDelete = logs.Where(log =>
            {
                // 应用与GetJobLogsAsync相同的过滤条件
                bool match = true;

                if (!string.IsNullOrEmpty(queryDto.JobName))
                {
                    match &= log.JobName.Contains(queryDto.JobName, StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(queryDto.JobGroup))
                {
                    match &= log.JobGroup.Contains(queryDto.JobGroup, StringComparison.OrdinalIgnoreCase);
                }

                if (queryDto.Status.HasValue)
                {
                    match &= log.Status == queryDto.Status.Value;
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
            }).ToList();

            // 如果没有指定查询条件，删除所有日志
            if (string.IsNullOrEmpty(queryDto.JobName) &&
                string.IsNullOrEmpty(queryDto.JobGroup) &&
                !queryDto.Status.HasValue &&
                !queryDto.StartTime.HasValue &&
                !queryDto.EndTime.HasValue)
            {
                logsToDelete = logs;
            }

            // 创建新的日志列表，不包含需要删除的日志
            var logsToKeep = logs.Except(logsToDelete).ToList();

            // 保存保留的日志（即删除了匹配条件的日志）
            await SaveLogsAsync(logsToKeep);

            // 计算清空的日志数量
            var clearedCount = logsToDelete.Count;
            _logger.LogInformation("清空作业日志成功: 共清空 {Count} 条日志", clearedCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空作业日志失败");
            return false;
        }
    }

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

            _logger.LogInformation("文件存储初始化成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文件存储初始化失败");
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
        var lockObject = GetFileLock(_jobsFilePath);
        lock (lockObject)
        {
            try
            {
                // 文件不存在则返回空列表
                if (!File.Exists(_jobsFilePath))
                {
                    return Task.FromResult(new List<QuartzJobInfo>());
                }

                // 使用FileStream并设置FileShare参数，允许并发读取
                using (var stream = new FileStream(_jobsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var jobs = JsonSerializer.Deserialize<List<QuartzJobInfo>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<QuartzJobInfo>();

                    return Task.FromResult(jobs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载作业数据失败");
                return Task.FromResult(new List<QuartzJobInfo>());
            }
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
                // 创建备份
                if (_options.EnableFileBackup && File.Exists(_jobsFilePath))
                {
                    CreateBackup(_jobsFilePath);
                }

                // 序列化作业列表为JSON
                var json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // 使用FileStream并设置FileShare参数，写入时允许其他进程读取
                using (var stream = new FileStream(_jobsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存作业数据失败");
                throw;
            }
        }
    }

    private Task<List<QuartzJobLog>> LoadLogsAsync()
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                if (!File.Exists(_logsFilePath))
                {
                    return Task.FromResult(new List<QuartzJobLog>());
                }

                // 使用FileStream并设置FileShare参数，允许并发读取
                using (var stream = new FileStream(_logsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var logs = JsonSerializer.Deserialize<List<QuartzJobLog>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<QuartzJobLog>();

                    return Task.FromResult(logs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载日志数据失败");
                return Task.FromResult(new List<QuartzJobLog>());
            }
        }
    }

    private Task SaveLogsAsync(List<QuartzJobLog> logs)
    {
        var lockObject = GetFileLock(_logsFilePath);
        lock (lockObject)
        {
            try
            {
                var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // 使用FileStream并设置FileShare参数，写入时允许其他进程读取
                using (var stream = new FileStream(_logsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存日志数据失败");
                throw;
            }
        }
    }

    private void CreateBackup(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
            var backupFilePath = Path.Combine(_options.FileBackupPath, backupFileName);

            File.Copy(filePath, backupFilePath, true);

            // 清理旧备份
            CleanupOldBackups();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建备份失败");
        }
    }

    private void CleanupOldBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_options.FileBackupPath, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            if (backupFiles.Count > _options.MaxBackupFiles)
            {
                var filesToDelete = backupFiles.Skip(_options.MaxBackupFiles).ToList();
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }

                _logger.LogInformation("清理旧备份文件: {Count} 个", filesToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理旧备份文件失败");
        }
    }

    #endregion

    #region 统计分析

    public async Task<JobStatsDto> GetJobStatsAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var logs = await LoadLogsAsync();

            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 过滤日志
            var filteredLogs = logs.Where(log => log.StartTime >= startTime && log.StartTime <= endTime).ToList();

            // 统计数据
            var stats = new JobStatsDto
            {
                TotalJobs = jobs.Count,
                EnabledJobs = jobs.Count(j => j.IsEnabled),
                DisabledJobs = jobs.Count(j => !j.IsEnabled),
                ExecutingJobs = filteredLogs.Count(l => l.Status == LogStatus.Running),
                SuccessCount = filteredLogs.Count(l => l.Status == LogStatus.Success),
                FailedCount = filteredLogs.Count(l => l.Status == LogStatus.Failed),
                PausedCount = jobs.Count(j => j.Status == JobStatus.Paused),
                BlockedCount = jobs.Count(j => j.Status == JobStatus.Blocked)
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业统计数据失败");
            return new JobStatsDto();
        }
    }

    public async Task<List<JobStatusDistributionDto>> GetJobStatusDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();

            // 按状态分组
            var statusGroups = jobs.GroupBy(j => j.Status).ToList();
            var totalJobs = jobs.Count;

            // 转换为分布数据
            var distribution = statusGroups.Select(group => new JobStatusDistributionDto
            {
                Status = group.Key.ToString(),
                Count = group.Count(),
                Percentage = totalJobs > 0 ? Math.Round((double)group.Count() / totalJobs * 100, 2) : 0
            }).ToList();

            return distribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业状态分布数据失败");
            return new List<JobStatusDistributionDto>();
        }
    }

    public async Task<List<JobExecutionTrendDto>> GetJobExecutionTrendAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();

            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 过滤日志
            var filteredLogs = logs.Where(log => log.StartTime >= startTime && log.StartTime <= endTime).ToList();

            // 按时间分组（小时）
            var timeGroups = filteredLogs.GroupBy(l => new DateTime(l.StartTime.Year, l.StartTime.Month, l.StartTime.Day, l.StartTime.Hour, 0, 0)).ToList();

            // 转换为趋势数据
            var trend = timeGroups.Select(group => new JobExecutionTrendDto
            {
                Time = group.Key.ToString("yyyy-MM-dd HH:00"),
                SuccessCount = group.Count(l => l.Status == LogStatus.Success),
                FailedCount = group.Count(l => l.Status == LogStatus.Failed),
                TotalCount = group.Count()
            }).OrderBy(t => t.Time).ToList();

            return trend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业执行趋势数据失败");
            return new List<JobExecutionTrendDto>();
        }
    }

    public async Task<List<JobTypeDistributionDto>> GetJobTypeDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await LoadJobsAsync();

            // 按类型分组
            var typeGroups = jobs.GroupBy(j => j.JobType).ToList();
            var totalJobs = jobs.Count;

            // 转换为分布数据
            var distribution = typeGroups.Select(group => new JobTypeDistributionDto
            {
                Type = group.Key.ToString(),
                Count = group.Count(),
                Percentage = totalJobs > 0 ? Math.Round((double)group.Count() / totalJobs * 100, 2) : 0
            }).ToList();

            return distribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业类型分布数据失败");
            return new List<JobTypeDistributionDto>();
        }
    }

    public async Task<List<JobExecutionTimeDto>> GetJobExecutionTimeAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();

            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 过滤日志
            var filteredLogs = logs.Where(log => log.StartTime >= startTime && log.StartTime <= endTime && log.Status != LogStatus.Running).ToList();

            // 按耗时分组
            var timeRangeGroups = new List<(string Range, Func<double, bool> Predicate)>
            {
                ("< 1s", d => d < 1),
                ("1-5s", d => d >= 1 && d < 5),
                ("5-10s", d => d >= 5 && d < 10),
                ("10-30s", d => d >= 10 && d < 30),
                ("30s-1m", d => d >= 30 && d < 60),
                ("1-5m", d => d >= 60 && d < 300),
                (">= 5m", d => d >= 300)
            };

            // 统计每个耗时区间的作业数
            var executionTimeData = timeRangeGroups.Select(group => new JobExecutionTimeDto
            {
                TimeRange = group.Range,
                Count = filteredLogs.Count(l => l.Duration.HasValue && group.Predicate(l.Duration.Value / 1000.0))
            }).ToList();

            return executionTimeData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业执行耗时数据失败");
            return new List<JobExecutionTimeDto>();
        }
    }

    /// <summary>
    /// 计算时间范围
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <returns>开始时间和结束时间</returns>
    private (DateTime StartTime, DateTime EndTime) CalculateTimeRange(StatsQueryDto queryDto)
    {
        DateTime startTime, endTime;

        // 如果指定了自定义时间范围，使用自定义时间
        if (queryDto.TimeRangeType == "custom" && queryDto.StartTime.HasValue && queryDto.EndTime.HasValue)
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
                    startTime = new DateTime(endTime.Year, endTime.Month, endTime.Day, 0, 0, 0).AddDays(-1);
                    endTime = new DateTime(endTime.Year, endTime.Month, endTime.Day, 0, 0, 0).AddMilliseconds(-1);
                    break;
                case "thisWeek":
                    startTime = endTime.AddDays(-(int)endTime.DayOfWeek).Date;
                    break;
                case "thisMonth":
                    startTime = new DateTime(endTime.Year, endTime.Month, 1, 0, 0, 0);
                    break;
                default:
                    startTime = endTime.AddDays(-7); // 默认最近7天
                    break;
            }
        }

        return (startTime, endTime);
    }

    #endregion
}