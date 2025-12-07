using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.EFCore.Data;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.EFCore.Services;

/// <summary>
/// EFCore数据库存储实现
/// </summary>
public class EFCoreJobStorage : IJobStorage
{
    private readonly QuartzDbContext _dbContext;
    private readonly ILogger<EFCoreJobStorage> _logger;

    /// <summary>
    /// 初始化 EFCoreJobStorage 实例
    /// </summary>
    /// <param name="dbContext">数据库上下文实例</param>
    /// <param name="logger">日志记录器实例</param>
    public EFCoreJobStorage(QuartzDbContext dbContext, ILogger<EFCoreJobStorage> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region 初始化数据库

    /// <summary>
    /// 初始化数据库存储，应用所有未应用的迁移
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>初始化成功返回 true，失败返回 false</returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 应用所有未应用的迁移
            await _dbContext.Database.MigrateAsync(cancellationToken);

            _logger.LogInformation("EFCore数据库存储初始化成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EFCore数据库存储初始化失败");
            return false;
        }
    }

    /// <summary>
    /// 检查数据库存储是否已初始化
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已初始化返回 true，未初始化返回 false</returns>
    public async Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 作业管理

    /// <summary>
    /// 添加新的定时作业
    /// </summary>
    /// <param name="jobInfo">作业信息对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加成功返回 true，失败返回 false</returns>
    public async Task<bool> AddJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查是否已存在
            var existingJob = await _dbContext.QuartzJobs
                .FirstOrDefaultAsync(j => j.JobName == jobInfo.JobName && j.JobGroup == jobInfo.JobGroup, cancellationToken);

            if (existingJob != null)
            {
                _logger.LogWarning("作业已存在: {JobKey}", jobInfo.GetJobKey());
                return false;
            }

            await _dbContext.QuartzJobs.AddAsync(jobInfo, cancellationToken);
            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("作业添加成功: {JobKey}", jobInfo.GetJobKey());
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业失败: {JobKey}", jobInfo.GetJobKey());
            return false;
        }
    }

    /// <summary>
    /// 更新现有的定时作业
    /// </summary>
    /// <param name="jobInfo">作业信息对象，包含更新后的作业数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回 true，失败返回 false</returns>
    public async Task<bool> UpdateJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var existingJob = await _dbContext.QuartzJobs
                .FirstOrDefaultAsync(j => j.JobName == jobInfo.JobName && j.JobGroup == jobInfo.JobGroup, cancellationToken);

            if (existingJob == null)
            {
                _logger.LogWarning("作业不存在: {JobKey}", jobInfo.GetJobKey());
                return false;
            }

            // 更新属性
            existingJob.TriggerName = jobInfo.TriggerName;
            existingJob.TriggerGroup = jobInfo.TriggerGroup;
            existingJob.CronExpression = jobInfo.CronExpression;
            existingJob.Description = jobInfo.Description;
            existingJob.JobType = jobInfo.JobType;
            existingJob.JobClassOrApi = jobInfo.JobClassOrApi;
            existingJob.JobData = jobInfo.JobData;
            existingJob.StartTime = jobInfo.StartTime;
            existingJob.EndTime = jobInfo.EndTime;
            existingJob.IsEnabled = jobInfo.IsEnabled;
            existingJob.Status = jobInfo.Status;
            existingJob.UpdateTime = jobInfo.UpdateTime;
            existingJob.UpdateBy = jobInfo.UpdateBy;
            existingJob.Remark = jobInfo.Remark;

            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("作业更新成功: {JobKey}", jobInfo.GetJobKey());
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业失败: {JobKey}", jobInfo.GetJobKey());
            return false;
        }
    }

    /// <summary>
    /// 根据作业名称和分组删除定时作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回 true，失败返回 false</returns>
    public async Task<bool> DeleteJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _dbContext.QuartzJobs
                .FirstOrDefaultAsync(j => j.JobName == jobName && j.JobGroup == jobGroup, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("作业不存在: {JobKey}", $"{jobGroup}.{jobName}");
                return false;
            }

            _dbContext.QuartzJobs.Remove(job);
            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("作业删除成功: {JobKey}", $"{jobGroup}.{jobName}");
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return false;
        }
    }

    /// <summary>
    /// 根据作业名称和分组获取单个定时作业信息
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>找到的作业信息对象，找不到则返回 null</returns>
    public async Task<QuartzJobInfo?> GetJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.QuartzJobs
                .FirstOrDefaultAsync(j => j.JobName == jobName && j.JobGroup == jobGroup, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return null;
        }
    }

    /// <summary>
    /// 根据查询条件获取定时作业列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含分页参数、过滤条件和排序规则</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含分页信息和作业列表的响应对象</returns>
    public async Task<PagedResponseDto<QuartzJobInfo>> GetJobsAsync(QuartzJobQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.QuartzJobs.AsQueryable();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                query = query.Where(j => EF.Functions.Like(j.JobName, $"%{queryDto.JobName}%"));
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                query = query.Where(j => EF.Functions.Like(j.JobGroup, $"%{queryDto.JobGroup}%"));
            }

            if (queryDto.Status.HasValue)
            {
                query = query.Where(j => j.Status == queryDto.Status.Value);
            }

            if (queryDto.IsEnabled.HasValue)
            {
                query = query.Where(j => j.IsEnabled == queryDto.IsEnabled.Value);
            }

            // 应用排序
            if (!string.IsNullOrEmpty(queryDto.SortBy))
            {
                var isAscending = string.Equals(queryDto.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        query = isAscending ? query.OrderBy(j => j.JobName) : query.OrderByDescending(j => j.JobName);
                        break;
                    case "jobgroup":
                        query = isAscending ? query.OrderBy(j => j.JobGroup) : query.OrderByDescending(j => j.JobGroup);
                        break;
                    case "status":
                        query = isAscending ? query.OrderBy(j => j.Status) : query.OrderByDescending(j => j.Status);
                        break;
                    case "isenabled":
                        query = isAscending ? query.OrderBy(j => j.IsEnabled) : query.OrderByDescending(j => j.IsEnabled);
                        break;
                    case "createtime":
                        query = isAscending ? query.OrderBy(j => j.CreateTime) : query.OrderByDescending(j => j.CreateTime);
                        break;
                    case "updatetime":
                        query = isAscending ? query.OrderBy(j => j.UpdateTime) : query.OrderByDescending(j => j.UpdateTime);
                        break;
                    case "previousruntime":
                        query = isAscending ? query.OrderBy(j => j.PreviousRunTime) : query.OrderByDescending(j => j.PreviousRunTime);
                        break;
                    case "nextruntime":
                        query = isAscending ? query.OrderBy(j => j.NextRunTime) : query.OrderByDescending(j => j.NextRunTime);
                        break;
                    default:
                        // 默认按创建时间降序排序
                        query = query.OrderByDescending(j => j.CreateTime);
                        break;
                }
            }
            else
            {
                // 默认按创建时间降序排序
                query = query.OrderByDescending(j => j.CreateTime);
            }

            // 分页
            var totalCount = await query.CountAsync(cancellationToken);
            var pagedJobs = await query
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToListAsync(cancellationToken);

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
    /// 获取所有定时作业列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业列表</returns>
    public async Task<List<QuartzJobInfo>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.QuartzJobs.ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有作业失败");
            return new List<QuartzJobInfo>();
        }
    }

    /// <summary>
    /// 更新定时作业的状态
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="status">新的作业状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回 true，失败返回 false</returns>
    public async Task<bool> UpdateJobStatusAsync(string jobName, string jobGroup, JobStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _dbContext.QuartzJobs
                .FirstOrDefaultAsync(j => j.JobName == jobName && j.JobGroup == jobGroup, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("作业不存在: {JobKey}", $"{jobGroup}.{jobName}");
                return false;
            }

            job.Status = status;
            job.UpdateTime = DateTime.UtcNow;

            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("作业状态更新成功: {JobKey}, 状态: {Status}", $"{jobGroup}.{jobName}", status);
            return result > 0;
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
    /// <returns>添加成功返回 true，失败返回 false</returns>
    public async Task<bool> AddJobLogAsync(QuartzJobLog jobLog, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.QuartzJobLogs.AddAsync(jobLog, cancellationToken);
            var result = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("作业日志添加成功: {LogId}", jobLog.LogId);
            return result > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业日志失败");
            return false;
        }
    }

    /// <summary>
    /// 根据查询条件获取作业执行日志列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含分页参数、过滤条件和排序规则</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含分页信息和日志列表的响应对象</returns>
    public async Task<PagedResponseDto<QuartzJobLog>> GetJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.QuartzJobLogs.AsQueryable();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                query = query.Where(l => EF.Functions.Like(l.JobName, $"%{queryDto.JobName}%"));
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                query = query.Where(l => EF.Functions.Like(l.JobGroup, $"%{queryDto.JobGroup}%"));
            }

            if (queryDto.Status.HasValue)
            {
                query = query.Where(l => l.Status == queryDto.Status.Value);
            }

            if (queryDto.StartTime.HasValue)
            {
                query = query.Where(l => l.StartTime >= queryDto.StartTime.Value);
            }

            if (queryDto.EndTime.HasValue)
            {
                query = query.Where(l => l.StartTime <= queryDto.EndTime.Value);
            }

            // 应用排序
            if (!string.IsNullOrEmpty(queryDto.SortBy))
            {
                var isAscending = string.Equals(queryDto.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

                switch (queryDto.SortBy.ToLower())
                {
                    case "jobname":
                        query = isAscending ? query.OrderBy(l => l.JobName) : query.OrderByDescending(l => l.JobName);
                        break;
                    case "jobgroup":
                        query = isAscending ? query.OrderBy(l => l.JobGroup) : query.OrderByDescending(l => l.JobGroup);
                        break;
                    case "status":
                        query = isAscending ? query.OrderBy(l => l.Status) : query.OrderByDescending(l => l.Status);
                        break;
                    case "createtime":
                        query = isAscending ? query.OrderBy(l => l.CreateTime) : query.OrderByDescending(l => l.CreateTime);
                        break;
                    case "starttime":
                        query = isAscending ? query.OrderBy(l => l.StartTime) : query.OrderByDescending(l => l.StartTime);
                        break;
                    case "endtime":
                        query = isAscending ? query.OrderBy(l => l.EndTime) : query.OrderByDescending(l => l.EndTime);
                        break;
                    case "duration":
                        query = isAscending ? query.OrderBy(l => l.Duration) : query.OrderByDescending(l => l.Duration);
                        break;
                    default:
                        // 默认按创建时间降序排序
                        query = query.OrderByDescending(l => l.CreateTime);
                        break;
                }
            }
            else
            {
                // 默认按创建时间降序排序
                query = query.OrderByDescending(l => l.CreateTime);
            }

            // 分页
            var totalCount = await query.CountAsync(cancellationToken);
            var pagedLogs = await query
                .Skip((queryDto.PageIndex - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .ToListAsync(cancellationToken);

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
    /// 清除指定天数之前的过期作业日志
    /// </summary>
    /// <param name="daysToKeep">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清除的日志数量</returns>
    public async Task<int> ClearExpiredLogsAsync(int daysToKeep, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            var expiredLogs = await _dbContext.QuartzJobLogs
                .Where(l => l.CreateTime < cutoffDate)
                .ToListAsync(cancellationToken);

            var expiredCount = expiredLogs.Count;

            if (expiredCount > 0)
            {
                _dbContext.QuartzJobLogs.RemoveRange(expiredLogs);
                await _dbContext.SaveChangesAsync(cancellationToken);

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
    /// 根据查询条件清空作业执行日志
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空成功返回 true，失败返回 false</returns>
    public async Task<bool> ClearJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.QuartzJobLogs.AsQueryable();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(queryDto.JobName))
            {
                query = query.Where(l => EF.Functions.Like(l.JobName, $"%{queryDto.JobName}%"));
            }

            if (!string.IsNullOrEmpty(queryDto.JobGroup))
            {
                query = query.Where(l => EF.Functions.Like(l.JobGroup, $"%{queryDto.JobGroup}%"));
            }

            if (queryDto.Status.HasValue)
            {
                query = query.Where(l => l.Status == queryDto.Status.Value);
            }

            if (queryDto.StartTime.HasValue)
            {
                query = query.Where(l => l.StartTime >= queryDto.StartTime.Value);
            }

            if (queryDto.EndTime.HasValue)
            {
                query = query.Where(l => l.StartTime <= queryDto.EndTime.Value);
            }

            // 执行删除操作
            var logsToDelete = await query.ToListAsync(cancellationToken);
            var deletedCount = logsToDelete.Count;

            if (deletedCount > 0)
            {
                _dbContext.QuartzJobLogs.RemoveRange(logsToDelete);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("清空作业日志成功: 共清空 {Count} 条日志", deletedCount);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空作业日志失败");
            return false;
        }
    }

    #endregion

    #region 统计分析

    /// <summary>
    /// 获取作业统计数据
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含时间范围等参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业统计数据</returns>
    public async Task<JobStatsDto> GetJobStatsAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 统计作业基本信息
            var totalJobs = await _dbContext.QuartzJobs.CountAsync(cancellationToken);
            var enabledJobs = await _dbContext.QuartzJobs.CountAsync(j => j.IsEnabled, cancellationToken);
            var disabledJobs = await _dbContext.QuartzJobs.CountAsync(j => !j.IsEnabled, cancellationToken);
            var pausedCount = await _dbContext.QuartzJobs.CountAsync(j => j.Status == JobStatus.Paused, cancellationToken);
            var blockedCount = await _dbContext.QuartzJobs.CountAsync(j => j.Status == JobStatus.Blocked, cancellationToken);

            // 统计日志信息
            var executingJobs = await _dbContext.QuartzJobLogs
                .CountAsync(l => l.Status == LogStatus.Running && l.StartTime >= startTime && l.StartTime <= endTime, cancellationToken);

            var successCount = await _dbContext.QuartzJobLogs
                .CountAsync(l => l.Status == LogStatus.Success && l.StartTime >= startTime && l.StartTime <= endTime, cancellationToken);

            var failedCount = await _dbContext.QuartzJobLogs
                .CountAsync(l => l.Status == LogStatus.Failed && l.StartTime >= startTime && l.StartTime <= endTime, cancellationToken);

            // 统计数据
            var stats = new JobStatsDto
            {
                TotalJobs = totalJobs,
                EnabledJobs = enabledJobs,
                DisabledJobs = disabledJobs,
                ExecutingJobs = executingJobs,
                SuccessCount = successCount,
                FailedCount = failedCount,
                PausedCount = pausedCount,
                BlockedCount = blockedCount
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业统计数据失败");
            return new JobStatsDto();
        }
    }

    /// <summary>
    /// 获取作业状态分布数据
    /// </summary>
    /// <param name="queryDto">查询条件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业状态分布数据列表</returns>
    public async Task<List<JobStatusDistributionDto>> GetJobStatusDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 按状态分组统计
            var statusGroups = await _dbContext.QuartzJobs
                .GroupBy(j => j.Status)
                .Select(group => new
                {
                    Status = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var totalJobs = await _dbContext.QuartzJobs.CountAsync(cancellationToken);

            // 转换为分布数据
            var distribution = statusGroups.Select(group => new JobStatusDistributionDto
            {
                Status = group.Status.ToString(),
                Count = group.Count,
                Percentage = totalJobs > 0 ? Math.Round((double)group.Count / totalJobs * 100, 2) : 0
            }).ToList();

            return distribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业状态分布数据失败");
            return new List<JobStatusDistributionDto>();
        }
    }

    /// <summary>
    /// 获取作业执行趋势数据，按小时统计成功、失败和总执行次数
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含时间范围等参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业执行趋势数据列表，按小时分组</returns>
    public async Task<List<JobExecutionTrendDto>> GetJobExecutionTrendAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 获取所有符合条件的日志，然后在内存中分组
            var logs = await _dbContext.QuartzJobLogs
                .Where(l => l.StartTime >= startTime && l.StartTime <= endTime)
                .ToListAsync(cancellationToken);

            // 在内存中按小时分组统计
            var timeGroups = logs
                .GroupBy(l => new DateTime(l.StartTime.Year, l.StartTime.Month, l.StartTime.Day, l.StartTime.Hour, 0, 0))
                .Select(group => new
                {
                    Time = group.Key,
                    SuccessCount = group.Count(l => l.Status == LogStatus.Success),
                    FailedCount = group.Count(l => l.Status == LogStatus.Failed),
                    TotalCount = group.Count()
                })
                .OrderBy(g => g.Time)
                .ToList();

            // 转换为趋势数据
            var trend = timeGroups.Select(group => new JobExecutionTrendDto
            {
                Time = group.Time.ToString("yyyy-MM-dd HH:00"),
                SuccessCount = group.SuccessCount,
                FailedCount = group.FailedCount,
                TotalCount = group.TotalCount
            }).ToList();

            return trend;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业执行趋势数据失败");
            return new List<JobExecutionTrendDto>();
        }
    }

    /// <summary>
    /// 获取作业类型分布数据
    /// </summary>
    /// <param name="queryDto">查询条件对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业类型分布数据列表</returns>
    public async Task<List<JobTypeDistributionDto>> GetJobTypeDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 按类型分组统计
            var typeGroups = await _dbContext.QuartzJobs
                .GroupBy(j => j.JobType)
                .Select(group => new
                {
                    Type = group.Key,
                    Count = group.Count()
                })
                .ToListAsync(cancellationToken);

            var totalJobs = await _dbContext.QuartzJobs.CountAsync(cancellationToken);

            // 转换为分布数据
            var distribution = typeGroups.Select(group => new JobTypeDistributionDto
            {
                Type = group.Type.ToString(),
                Count = group.Count,
                Percentage = totalJobs > 0 ? Math.Round((double)group.Count / totalJobs * 100, 2) : 0
            }).ToList();

            return distribution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业类型分布数据失败");
            return new List<JobTypeDistributionDto>();
        }
    }

    /// <summary>
    /// 获取作业执行耗时分布数据
    /// </summary>
    /// <param name="queryDto">查询条件对象，包含时间范围等参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业执行耗时分布数据列表</returns>
    public async Task<List<JobExecutionTimeDto>> GetJobExecutionTimeAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 计算时间范围
            var (startTime, endTime) = CalculateTimeRange(queryDto);

            // 获取所有符合条件的日志
            var logs = await _dbContext.QuartzJobLogs
                .Where(l => l.StartTime >= startTime && l.StartTime <= endTime && l.Status != LogStatus.Running)
                .ToListAsync(cancellationToken);

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
                Count = logs.Count(l => l.Duration.HasValue && group.Predicate(l.Duration.Value / 1000.0))
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