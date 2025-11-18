using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Jobs;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// Quartz作业服务实现
/// </summary>
public class QuartzJobService : IQuartzJobService
{
    private IScheduler _scheduler;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IJobStorage _jobStorage;
    private readonly ILogger<QuartzJobService> _logger;
    private readonly QuartzUIOptions _options;
    private readonly JobClassScanner _jobClassScanner;

    public QuartzJobService(
        ISchedulerFactory schedulerFactory,
        IJobStorage jobStorage,
        ILogger<QuartzJobService> logger,
        IOptions<QuartzUIOptions> options,
        JobClassScanner jobClassScanner)
    {
        _schedulerFactory = schedulerFactory;
        _scheduler = _schedulerFactory.GetScheduler().Result;
        _jobStorage = jobStorage;
        _logger = logger;
        _options = options.Value;
        _jobClassScanner = jobClassScanner;
    }

    public async Task<ApiResponseDto<bool>> AddJobAsync(QuartzJobDto jobDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 验证Cron表达式
            var cronValidation = ValidateCronExpression(jobDto.CronExpression);
            if (!cronValidation.Success)
            {
                return ApiResponseDto<bool>.ErrorResponse(cronValidation.Message);
            }

            // 检查作业是否已存在
            var existingJob = await _jobStorage.GetJobAsync(jobDto.JobName, jobDto.JobGroup, cancellationToken);
            if (existingJob != null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业已存在");
            }

            // 验证作业类型
            if (!ValidateJobType(jobDto.JobType, jobDto.JobTypeEnum))
            {
                return ApiResponseDto<bool>.ErrorResponse("无效的作业类型");
            }

            // 创建作业信息
            var jobInfo = new QuartzJobInfo
            {
                JobName = jobDto.JobName,
                JobGroup = jobDto.JobGroup,
                TriggerName = jobDto.TriggerName,
                TriggerGroup = jobDto.TriggerGroup,
                CronExpression = jobDto.CronExpression,
                Description = jobDto.Description,
                JobTypeEnum = jobDto.JobTypeEnum,
                JobType = jobDto.JobType,
                JobData = jobDto.JobData,
                ApiMethod = jobDto.ApiMethod,
                ApiHeaders = jobDto.ApiHeaders,
                ApiBody = jobDto.ApiBody,
                ApiTimeout = jobDto.ApiTimeout,
                SkipSslValidation = jobDto.SkipSslValidation,
                StartTime = jobDto.StartTime,
                EndTime = jobDto.EndTime,
                IsEnabled = jobDto.IsEnabled,
                Status = JobStatus.Normal,
                CreateTime = DateTime.Now
            };

            // 保存到存储
            var saveResult = await _jobStorage.AddJobAsync(jobInfo, cancellationToken);
            if (!saveResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("保存作业信息失败");
            }

            // 如果启用，则立即调度作业
            if (jobDto.IsEnabled)
            {
                await ScheduleJobAsync(jobInfo, cancellationToken);
            }

            _logger.LogInformation("作业添加成功: {JobKey}", jobInfo.GetJobKey());
            return ApiResponseDto<bool>.SuccessResponse(true, "作业添加成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业失败: {JobName}", jobDto.JobName);
            return ApiResponseDto<bool>.ErrorResponse($"添加作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> UpdateJobAsync(QuartzJobDto jobDto, CancellationToken cancellationToken = default)
    {
        try
        {
            // 验证Cron表达式
            var cronValidation = ValidateCronExpression(jobDto.CronExpression);
            if (!cronValidation.Success)
            {
                return ApiResponseDto<bool>.ErrorResponse(cronValidation.Message);
            }

            // 获取现有作业
            var existingJob = await _jobStorage.GetJobAsync(jobDto.JobName, jobDto.JobGroup, cancellationToken);
            if (existingJob == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在");
            }

            // 验证作业类型
            if (!ValidateJobType(jobDto.JobType, jobDto.JobTypeEnum))
            {
                return ApiResponseDto<bool>.ErrorResponse("无效的作业类型");
            }

            // 删除现有的作业调度
            var jobKey = new JobKey(jobDto.JobName, jobDto.JobGroup);
            var triggerKey = new TriggerKey(existingJob.TriggerName, existingJob.TriggerGroup);

            await _scheduler.PauseJob(jobKey, cancellationToken);
            await _scheduler.UnscheduleJob(triggerKey, cancellationToken);
            await _scheduler.DeleteJob(jobKey, cancellationToken);

            // 更新作业信息
            existingJob.TriggerName = jobDto.TriggerName;
            existingJob.TriggerGroup = jobDto.TriggerGroup;
            existingJob.CronExpression = jobDto.CronExpression;
            existingJob.Description = jobDto.Description;
            existingJob.JobTypeEnum = jobDto.JobTypeEnum;
            existingJob.JobType = jobDto.JobType;
            existingJob.JobData = jobDto.JobData;
            existingJob.ApiMethod = jobDto.ApiMethod;
            existingJob.ApiHeaders = jobDto.ApiHeaders;
            existingJob.ApiBody = jobDto.ApiBody;
            existingJob.ApiTimeout = jobDto.ApiTimeout;
            existingJob.SkipSslValidation = jobDto.SkipSslValidation;
            existingJob.StartTime = jobDto.StartTime;
            existingJob.EndTime = jobDto.EndTime;
            existingJob.IsEnabled = jobDto.IsEnabled;
            existingJob.UpdateTime = DateTime.Now;

            // 保存更新
            var updateResult = await _jobStorage.UpdateJobAsync(existingJob, cancellationToken);
            if (!updateResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("更新作业信息失败");
            }

            // 如果启用，则重新调度作业
            if (jobDto.IsEnabled)
            {
                await ScheduleJobAsync(existingJob, cancellationToken);
            }

            _logger.LogInformation("作业更新成功: {JobKey}", existingJob.GetJobKey());
            return ApiResponseDto<bool>.SuccessResponse(true, "作业更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业失败: {JobName}", jobDto.JobName);
            return ApiResponseDto<bool>.ErrorResponse($"更新作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> DeleteJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);

            // 删除调度器中的作业
            var deleteResult = await _scheduler.DeleteJob(jobKey, cancellationToken);
            if (!deleteResult)
            {
                _logger.LogWarning("从调度器删除作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            }

            // 删除存储中的作业信息
            var storageResult = await _jobStorage.DeleteJobAsync(jobName, jobGroup, cancellationToken);

            _logger.LogInformation("作业删除成功: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.ErrorResponse($"删除作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> PauseJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            await _scheduler.PauseJob(jobKey, cancellationToken);

            // 更新作业状态
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo != null)
            {
                jobInfo.Status = JobStatus.Paused;
                // 暂停后，下次执行时间设为null
                jobInfo.NextRunTime = null;
                jobInfo.UpdateTime = DateTime.Now;
                await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
            }

            _logger.LogInformation("作业暂停成功: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业暂停成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.ErrorResponse($"暂停作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> ResumeJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            
            // 获取作业信息
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在");
            }

            // 检查作业是否在调度器中存在
            var jobExists = await _scheduler.CheckExists(jobKey, cancellationToken);

            if (jobExists)
            {
                // 如果作业存在，先尝试恢复
                await _scheduler.ResumeJob(jobKey, cancellationToken);
                
                // 检查是否有触发器，如果没有则重新调度
                var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                if (!triggers.Any())
                {
                    _logger.LogInformation("作业 {JobKey} 恢复后没有触发器，重新调度作业", $"{jobGroup}.{jobName}");
                    await ScheduleJobAsync(jobInfo, cancellationToken);
                }
            }
            else
            {
                // 如果作业不存在，重新调度作业
                _logger.LogInformation("作业 {JobKey} 在调度器中不存在，重新调度作业", $"{jobGroup}.{jobName}");
                await ScheduleJobAsync(jobInfo, cancellationToken);
            }

            // 更新作业状态
            jobInfo.Status = JobStatus.Normal;
            jobInfo.UpdateTime = DateTime.Now;
            
            // 获取触发器信息，更新下次执行时间
            var updatedTriggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);
            if (updatedTriggers.Any())
            {
                var nextFireTimes = updatedTriggers.Select(t => t.GetNextFireTimeUtc())
                                                   .Where(t => t.HasValue)
                                                   .OrderBy(t => t.Value)
                                                   .ToList();

                if (nextFireTimes.Any())
                {
                    jobInfo.NextRunTime = nextFireTimes.First().Value.DateTime;
                }

                var previousFireTimes = updatedTriggers.Select(t => t.GetPreviousFireTimeUtc())
                                                       .Where(t => t.HasValue)
                                                       .OrderByDescending(t => t.Value)
                                                       .ToList();

                if (previousFireTimes.Any())
                {
                    jobInfo.PreviousRunTime = previousFireTimes.First().Value.DateTime;
                }
            }

            await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);

            _logger.LogInformation("作业恢复成功: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业恢复成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.ErrorResponse($"恢复作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> TriggerJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);

            // 检查作业是否在调度器中存在
            var jobExists = await _scheduler.CheckExists(jobKey, cancellationToken);

            // 如果作业不存在，先从存储中获取作业信息并注册到调度器
            if (!jobExists)
            {
                var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
                if (jobInfo == null)
                {
                    return ApiResponseDto<bool>.ErrorResponse("作业不存在");
                }

                // 注册作业到调度器
                await ScheduleJobAsync(jobInfo, cancellationToken);
                _logger.LogInformation("作业注册成功: {JobKey}", $"{jobGroup}.{jobName}");
            }

            // 获取作业信息，检查是否为暂停状态
            var jobInfoForTrigger = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            var wasPaused = false;
            
            if (jobInfoForTrigger != null && jobInfoForTrigger.Status == JobStatus.Paused)
            {
                // 临时将作业状态改为正常，允许手动触发执行
                wasPaused = true;
                jobInfoForTrigger.Status = JobStatus.Normal;
                await _jobStorage.UpdateJobAsync(jobInfoForTrigger, cancellationToken);
                _logger.LogInformation("临时修改暂停作业状态为正常，允许手动触发: {JobKey}", $"{jobGroup}.{jobName}");
            }

            // 触发作业
            await _scheduler.TriggerJob(jobKey, cancellationToken);

            // 如果原来是暂停状态，执行完毕后恢复为暂停状态
            if (wasPaused)
            {
                jobInfoForTrigger.Status = JobStatus.Paused;
                await _jobStorage.UpdateJobAsync(jobInfoForTrigger, cancellationToken);
                _logger.LogInformation("手动触发执行完成，恢复作业暂停状态: {JobKey}", $"{jobGroup}.{jobName}");
            }

            _logger.LogInformation("作业触发成功: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业触发成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发作业失败: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.ErrorResponse($"触发作业失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>> GetJobsAsync(QuartzJobQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobStorage.GetJobsAsync(queryDto, cancellationToken);

            var responseItems = new List<QuartzJobResponseDto>();

            foreach (var jobInfo in result.Items)
            {
                var jobResponse = MapToResponseDto(jobInfo);

                // 从调度器获取实时的触发信息
                try
                {
                    var jobKey = new JobKey(jobInfo.JobName, jobInfo.JobGroup);
                    var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                    _logger.LogInformation("作业 {JobKey} 找到 {TriggerCount} 个触发器", $"{jobInfo.JobGroup}.{jobInfo.JobName}", triggers.Count);

                    if (triggers.Any())
                    {
                        // 找到最早的下一次执行时间
                        var nextFireTimes = triggers.Select(t => t.GetNextFireTimeUtc())
                                                   .Where(t => t.HasValue)
                                                   .OrderBy(t => t.Value)
                                                   .ToList();

                        if (nextFireTimes.Any())
                        {
                            // 将UTC时间转换为北京时间（UTC+8）
                            jobResponse.NextRunTime = nextFireTimes.First().Value.DateTime.AddHours(8);
                            _logger.LogInformation("作业 {JobKey} 的下次执行时间: {NextRunTime}", $"{jobInfo.JobGroup}.{jobInfo.JobName}", jobResponse.NextRunTime);
                        }
                        else
                        {
                            _logger.LogInformation("作业 {JobKey} 的所有触发器都没有下一次执行时间", $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                        }

                        // 找到最近的上一次执行时间
                        var previousFireTimes = triggers.Select(t => t.GetPreviousFireTimeUtc())
                                                       .Where(t => t.HasValue)
                                                       .OrderByDescending(t => t.Value)
                                                       .ToList();

                        if (previousFireTimes.Any())
                        {
                            // 将UTC时间转换为北京时间（UTC+8）
                            jobResponse.PreviousRunTime = previousFireTimes.First().Value.DateTime.AddHours(8);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("作业 {JobKey} 没有找到触发器", $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取作业 {JobKey} 的触发信息失败", $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                    // 忽略单个作业的错误，继续处理其他作业
                }

                responseItems.Add(jobResponse);
            }

            var response = new PagedResponseDto<QuartzJobResponseDto>
            {
                Items = responseItems,
                TotalCount = result.TotalCount,
                PageIndex = result.PageIndex,
                PageSize = result.PageSize
            };

            return ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业列表失败");
            return ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.ErrorResponse($"获取作业列表失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<QuartzJobResponseDto>> GetJobDetailAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo == null)
            {
                return ApiResponseDto<QuartzJobResponseDto>.ErrorResponse("作业不存在");
            }

            var response = MapToResponseDto(jobInfo);

            // 获取调度器中的作业信息
            var jobKey = new JobKey(jobName, jobGroup);
            var jobDetail = await _scheduler.GetJobDetail(jobKey, cancellationToken);
            var triggers = await _scheduler.GetTriggersOfJob(jobKey, cancellationToken);

            if (triggers.Any())
            {
                var trigger = triggers.First();
                // 将UTC时间转换为北京时间（UTC+8）
                response.NextRunTime = trigger.GetNextFireTimeUtc()?.DateTime.AddHours(8);
                response.PreviousRunTime = trigger.GetPreviousFireTimeUtc()?.DateTime.AddHours(8);
            }

            return ApiResponseDto<QuartzJobResponseDto>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业详情失败: {JobKey}", $"{jobGroup}.{jobName}");
            return ApiResponseDto<QuartzJobResponseDto>.ErrorResponse($"获取作业详情失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<PagedResponseDto<QuartzJobLog>>> GetJobLogsAsync(string? jobName, string? jobGroup, LogStatus? status, DateTime? startTime, DateTime? endTime, int pageIndex = 1, int pageSize = 20, string? sortBy = null, string? sortOrder = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobStorage.GetJobLogsAsync(jobName, jobGroup, status, startTime, endTime, pageIndex, pageSize, sortBy, sortOrder, cancellationToken);
            return ApiResponseDto<PagedResponseDto<QuartzJobLog>>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业日志失败");
            return ApiResponseDto<PagedResponseDto<QuartzJobLog>>.ErrorResponse($"获取作业日志失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<SchedulerStatusDto>> GetSchedulerStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var status = new SchedulerStatusDto
            {
                SchedulerName = _scheduler.SchedulerName,
                SchedulerInstanceId = _scheduler.SchedulerInstanceId,
                IsStarted = _scheduler.IsStarted,
                IsShutdown = _scheduler.IsShutdown,
                InStandbyMode = _scheduler.InStandbyMode,
                Status = GetSchedulerStatus(),
                ThreadPoolSize = _options.ThreadPoolSize,
                Version = typeof(IScheduler).Assembly.GetName().Version?.ToString() ?? "Unknown",
                StartTime = _scheduler.GetType().GetProperty("StartTime")?.GetValue(_scheduler) as DateTime?
            };

            // 如果调度器已停止，避免调用会抛出异常的方法
            if (!_scheduler.IsShutdown)
            {
                var jobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
                var currentlyExecuting = await _scheduler.GetCurrentlyExecutingJobs(cancellationToken);
                status.JobCount = jobKeys.Count;
                status.ExecutingJobCount = currentlyExecuting.Count;
            }
            else
            {
                // 调度器已停止，设置默认值
                status.JobCount = 0;
                status.ExecutingJobCount = 0;
            }

            if (status.StartTime.HasValue)
            {
                status.RunningTime = (long)(DateTime.Now - status.StartTime.Value).TotalMilliseconds;
            }

            return ApiResponseDto<SchedulerStatusDto>.SuccessResponse(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取调度器状态失败");
            return ApiResponseDto<SchedulerStatusDto>.ErrorResponse($"获取调度器状态失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> StartSchedulerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_scheduler.IsShutdown)
            {
                // 如果调度器已关闭，重新创建调度器实例
                _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                _logger.LogInformation("重新创建调度器实例");
            }

            if (!_scheduler.IsStarted)
            {
                await _scheduler.Start(cancellationToken);
                _logger.LogInformation("调度器启动成功");
            }

            return ApiResponseDto<bool>.SuccessResponse(true, "调度器启动成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动调度器失败");
            return ApiResponseDto<bool>.ErrorResponse($"启动调度器失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> ShutdownSchedulerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_scheduler.IsShutdown)
            {
                await _scheduler.Shutdown(cancellationToken);
                _logger.LogInformation("调度器停止成功");
            }

            return ApiResponseDto<bool>.SuccessResponse(true, "调度器停止成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止调度器失败");
            return ApiResponseDto<bool>.ErrorResponse($"停止调度器失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> ClearAllJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

            foreach (var jobKey in jobKeys)
            {
                await _scheduler.DeleteJob(jobKey, cancellationToken);
            }

            _logger.LogInformation("清除所有作业成功");
            return ApiResponseDto<bool>.SuccessResponse(true, "清除所有作业成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清除所有作业失败");
            return ApiResponseDto<bool>.ErrorResponse($"清除所有作业失败: {ex.Message}");
        }
    }

    public ApiResponseDto<bool> ValidateCronExpression(string cronExpression)
    {
        try
        {
            var expression = new CronExpression(cronExpression);
            var isValid = expression.IsSatisfiedBy(DateTimeOffset.Now);
            return ApiResponseDto<bool>.SuccessResponse(isValid, "Cron表达式有效");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.ErrorResponse($"Cron表达式无效: {ex.Message}");
        }
    }

    public ApiResponseDto<List<DateTime>> GetNextRunTimes(string cronExpression, int count = 5)
    {
        try
        {
            var expression = new CronExpression(cronExpression);
            var nextRunTimes = new List<DateTime>();
            var currentTime = DateTimeOffset.Now;

            for (int i = 0; i < count; i++)
            {
                var nextTime = expression.GetNextValidTimeAfter(currentTime);
                if (nextTime.HasValue)
                {
                    nextRunTimes.Add(nextTime.Value.DateTime);
                    currentTime = nextTime.Value;
                }
                else
                {
                    break;
                }
            }

            return ApiResponseDto<List<DateTime>>.SuccessResponse(nextRunTimes);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<DateTime>>.ErrorResponse($"获取下次执行时间失败: {ex.Message}");
        }
    }

    private async Task ScheduleJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default)
    {
        // 确定作业类型
        Type jobType;

        if (jobInfo.JobTypeEnum == JobTypeEnum.API)
        {
            // API作业使用ApiJob类
            jobType = typeof(ApiJob);
        }
        else
        {
            // DLL作业使用指定的类型
            jobType = Type.GetType(jobInfo.JobType);

            // 如果没找到，搜索所有已加载的程序集
            if (jobType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    jobType = assembly.GetType(jobInfo.JobType);
                    if (jobType != null)
                    {
                        break;
                    }
                }
            }

            if (jobType == null)
            {
                throw new ArgumentException($"无法找到作业类型: {jobInfo.JobType}");
            }
        }

        // 创建作业
        var jobBuilder = JobBuilder.Create(jobType)
            .WithIdentity(jobInfo.JobName, jobInfo.JobGroup)
            .WithDescription(jobInfo.Description ?? string.Empty)
            .StoreDurably();

        // 设置作业数据
        if (!string.IsNullOrEmpty(jobInfo.JobData))
        {
            var jobDataMap = new JobDataMap();
            try
            {
                // 解析JSON数据并添加到jobDataMap
                var jobDataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jobInfo.JobData);
                if (jobDataDict != null)
                {
                    foreach (var kvp in jobDataDict)
                    {
                        jobDataMap.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "解析作业数据JSON失败: {JobData}", jobInfo.JobData);
            }
            jobBuilder.UsingJobData(jobDataMap);
        }

        var jobDetail = jobBuilder.Build();

        // 创建触发器
        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(jobInfo.TriggerName, jobInfo.TriggerGroup)
            .WithCronSchedule(jobInfo.CronExpression)
            .WithDescription(jobInfo.Description ?? string.Empty);

        if (jobInfo.StartTime.HasValue)
        {
            triggerBuilder.StartAt(jobInfo.StartTime.Value);
        }

        if (jobInfo.EndTime.HasValue)
        {
            triggerBuilder.EndAt(jobInfo.EndTime.Value);
        }

        var trigger = triggerBuilder.Build();

        // 调度作业 - 允许替换现有的作业和触发器
        await _scheduler.ScheduleJob(jobDetail, new List<ITrigger> { trigger }, true, cancellationToken);

        // 获取作业的下次执行时间并更新到存储
        var nextRunTime = trigger.GetNextFireTimeUtc()?.DateTime;
        var previousRunTime = trigger.GetPreviousFireTimeUtc()?.DateTime;

        // 更新作业信息
        jobInfo.NextRunTime = nextRunTime;
        jobInfo.PreviousRunTime = previousRunTime;
        await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
    }

    private bool ValidateJobType(string jobType, JobTypeEnum jobTypeEnum)
    {
        try
        {
            // 如果是API作业，验证URL格式
            if (jobTypeEnum == JobTypeEnum.API)
            {
                return Uri.TryCreate(jobType, UriKind.Absolute, out var uriResult) &&
                       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            }

            // 如果是DLL作业，验证是否为有效的IJob类型
            // 先尝试使用Type.GetType
            var type = Type.GetType(jobType);
            if (type != null && typeof(IJob).IsAssignableFrom(type))
            {
                return true;
            }

            // 如果没找到，搜索所有已加载的程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(jobType);
                if (type != null && typeof(IJob).IsAssignableFrom(type))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private QuartzJobResponseDto MapToResponseDto(QuartzJobInfo jobInfo)
    {
        return new QuartzJobResponseDto
        {
            JobName = jobInfo.JobName,
            JobGroup = jobInfo.JobGroup,
            TriggerName = jobInfo.TriggerName,
            TriggerGroup = jobInfo.TriggerGroup,
            CronExpression = jobInfo.CronExpression,
            Description = jobInfo.Description,
            JobTypeEnum = jobInfo.JobTypeEnum,
            JobType = jobInfo.JobType,
            JobData = jobInfo.JobData,
            ApiMethod = jobInfo.ApiMethod,
            ApiHeaders = jobInfo.ApiHeaders,
            ApiBody = jobInfo.ApiBody,
            ApiTimeout = jobInfo.ApiTimeout,
            SkipSslValidation = jobInfo.SkipSslValidation,
            StartTime = jobInfo.StartTime,
            EndTime = jobInfo.EndTime,
            Status = jobInfo.Status,
            IsEnabled = jobInfo.IsEnabled,
            CreateTime = jobInfo.CreateTime,
            UpdateTime = jobInfo.UpdateTime,
            CreateBy = jobInfo.CreateBy,
            UpdateBy = jobInfo.UpdateBy,
            NextRunTime = null, // 将在GetJobsAsync中设置
            PreviousRunTime = null // 将在GetJobsAsync中设置
        };
    }

    private string GetSchedulerStatus()
    {
        if (_scheduler.IsShutdown)
            return "已停止";
        if (_scheduler.InStandbyMode)
            return "待机";
        if (_scheduler.IsStarted)
            return "运行中";
        return "未知";
    }

    public async Task<ApiResponseDto<bool>> UpdateJobExecutionTimesAsync(string jobName, string jobGroup, Quartz.ITrigger trigger, CancellationToken cancellationToken = default)
    {
        try
        {
            // 从存储中获取作业信息
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在");
            }

            // 更新作业执行时间
            jobInfo.NextRunTime = trigger.GetNextFireTimeUtc()?.DateTime;
            jobInfo.PreviousRunTime = trigger.GetPreviousFireTimeUtc()?.DateTime;
            jobInfo.UpdateTime = DateTime.Now;

            // 保存到存储
            var updateResult = await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
            if (!updateResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("更新作业执行时间失败");
            }

            _logger.LogDebug("作业执行时间更新成功: {JobKey}", new JobKey(jobName, jobGroup));
            return ApiResponseDto<bool>.SuccessResponse(true, "作业执行时间更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业执行时间失败: {JobKey}", new JobKey(jobName, jobGroup));
            return ApiResponseDto<bool>.ErrorResponse($"更新作业执行时间失败: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<List<string>>> GetJobClassesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始获取作业类列表");
            var jobClasses = _jobClassScanner.ScanJobClasses();
            _logger.LogInformation("获取作业类列表成功，共找到 {Count} 个作业类", jobClasses.Count);
            return ApiResponseDto<List<string>>.SuccessResponse(jobClasses, "获取作业类列表成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业类列表失败");
            return ApiResponseDto<List<string>>.ErrorResponse($"获取作业类列表失败: {ex.Message}");
        }
    }
}