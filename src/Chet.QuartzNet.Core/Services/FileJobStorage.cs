using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// 文件存储实现
/// </summary>
public class FileJobStorage : IJobStorage
{
    private readonly QuartzUIOptions _options;
    private readonly ILogger<FileJobStorage> _logger;
    private readonly string _jobsFilePath;
    private readonly string _logsFilePath;

    public FileJobStorage(IOptions<QuartzUIOptions> options, ILogger<FileJobStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
        _jobsFilePath = Path.Combine(_options.FileStoragePath, "jobs.json");
        _logsFilePath = Path.Combine(_options.FileStoragePath, "logs.json");

        // 确保目录存在
        Directory.CreateDirectory(_options.FileStoragePath);
        if (_options.EnableFileBackup)
        {
            Directory.CreateDirectory(_options.FileBackupPath);
        }
    }

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

            // 更新属性
            existingJob.TriggerName = jobInfo.TriggerName;
            existingJob.TriggerGroup = jobInfo.TriggerGroup;
            existingJob.CronExpression = jobInfo.CronExpression;
            existingJob.Description = jobInfo.Description;
            existingJob.JobType = jobInfo.JobType;
            existingJob.JobData = jobInfo.JobData;
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

            // 分页
            var totalCount = jobs.Count;
            var pagedJobs = jobs
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

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

            // 分页
            var totalCount = logs.Count;
            var pagedLogs = logs
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToList();

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

    public async Task<int> ClearExpiredLogsAsync(int daysToKeep, CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await LoadLogsAsync();
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

            var expiredLogs = logs.Where(l => l.CreateTime < cutoffDate).ToList();
            var expiredCount = expiredLogs.Count;

            if (expiredCount > 0)
            {
                foreach (var log in expiredLogs)
                {
                    logs.Remove(log);
                }

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

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查作业文件是否存在
            if (!File.Exists(_jobsFilePath))
            {
                await SaveJobsAsync(new List<QuartzJobInfo>());
            }

            // 检查日志文件是否存在
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

    public async Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default)
    {
        return File.Exists(_jobsFilePath) && File.Exists(_logsFilePath);
    }

    private async Task<List<QuartzJobInfo>> LoadJobsAsync()
    {
        try
        {
            if (!File.Exists(_jobsFilePath))
            {
                return new List<QuartzJobInfo>();
            }

            // 使用FileStream并设置FileShare参数，允许并发读取
            using (var stream = new FileStream(_jobsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var jobs = JsonSerializer.Deserialize<List<QuartzJobInfo>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<QuartzJobInfo>();

                return jobs;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载作业数据失败");
            return new List<QuartzJobInfo>();
        }
    }

    private async Task SaveJobsAsync(List<QuartzJobInfo> jobs)
    {
        try
        {
            // 创建备份
            if (_options.EnableFileBackup && File.Exists(_jobsFilePath))
            {
                await CreateBackupAsync(_jobsFilePath);
            }

            var json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 使用FileStream并设置FileShare参数，写入时允许其他进程读取
            using (var stream = new FileStream(_jobsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存作业数据失败");
            throw;
        }
    }

    private async Task<List<QuartzJobLog>> LoadLogsAsync()
    {
        try
        {
            if (!File.Exists(_logsFilePath))
            {
                return new List<QuartzJobLog>();
            }

            // 使用FileStream并设置FileShare参数，允许并发读取
            using (var stream = new FileStream(_logsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var logs = JsonSerializer.Deserialize<List<QuartzJobLog>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<QuartzJobLog>();

                return logs;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载日志数据失败");
            return new List<QuartzJobLog>();
        }
    }

    private async Task SaveLogsAsync(List<QuartzJobLog> logs)
    {
        try
        {
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 使用FileStream并设置FileShare参数，写入时允许其他进程读取
            using (var stream = new FileStream(_logsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存日志数据失败");
            throw;
        }
    }

    private async Task CreateBackupAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
            var backupFilePath = Path.Combine(_options.FileBackupPath, backupFileName);

            File.Copy(filePath, backupFilePath, true);

            // 清理旧备份
            await CleanupOldBackupsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建备份失败");
        }
    }

    private async Task CleanupOldBackupsAsync()
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
}