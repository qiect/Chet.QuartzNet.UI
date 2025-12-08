using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Services
{
    /// <summary>
    /// Quartz作业监听器，用于记录作业执行日志
    /// </summary>
    public class QuartzJobListener : IJobListener
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QuartzJobListener> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scopeFactory">服务作用域工厂</param>
        /// <param name="logger">日志记录器</param>
        public QuartzJobListener(IServiceScopeFactory scopeFactory, ILogger<QuartzJobListener> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 监听器名称
        /// </summary>
        public string Name => "QuartzJobListener";

        /// <summary>
        /// 作业执行完成后调用
        /// </summary>
        /// <param name="context">作业执行上下文</param>
        /// <param name="result">作业执行结果</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? result, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = new QuartzJobLog
                {
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    TriggerName = context.Trigger.Key.Name,
                    TriggerGroup = context.Trigger.Key.Group,
                    StartTime = context.FireTimeUtc.LocalDateTime,
                    EndTime = DateTime.Now,
                    Duration = (long)(DateTime.Now - context.FireTimeUtc.LocalDateTime).TotalMilliseconds
                };

                // 处理执行结果
                if (result == null)
                {
                    jobLog.Status = LogStatus.Success;
                    jobLog.Message = "作业执行成功";
                }
                else
                {
                    jobLog.Status = LogStatus.Failed;
                    jobLog.Message = "作业执行失败";
                    jobLog.Exception = result.ToString();
                    jobLog.ErrorMessage = result.Message;
                    jobLog.ErrorStackTrace = result.StackTrace;
                }

                // 记录作业数据
                if (context.MergedJobDataMap != null && context.MergedJobDataMap.Count > 0)
                {
                    var jobDataDict = context.MergedJobDataMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());
                    jobLog.JobData = JsonSerializer.Serialize(jobDataDict);
                }

                // 创建作用域来解析IJobStorage
                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

                // 记录作业日志
                await jobStorage.AddJobLogAsync(jobLog, cancellationToken);

                // 更新作业的下次执行时间和上次执行时间
                await jobService.UpdateJobExecutionTimesAsync(context.JobDetail.Key.Name, context.JobDetail.Key.Group, context.Trigger, cancellationToken);

                // 发送推送通知
                var notificationService = scope.ServiceProvider.GetService<INotificationService>();
                if (notificationService != null)
                {
                    await notificationService.SendJobExecutionNotificationAsync(
                        context.JobDetail.Key.Name,
                        context.JobDetail.Key.Group,
                        result == null,
                        jobLog.Message,
                        jobLog.Duration ?? 0,
                        jobLog.ErrorMessage,
                        cancellationToken);
                }

                _logger.LogInformation("作业执行日志记录成功: {JobKey}, 状态: {Status}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}", jobLog.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录作业执行日志失败: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
            }
        }

        /// <summary>
        /// 作业执行被否决时调用
        /// </summary>
        /// <param name="context">作业执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var jobLog = new QuartzJobLog
                {
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    TriggerName = context.Trigger.Key.Name,
                    TriggerGroup = context.Trigger.Key.Group,
                    StartTime = context.FireTimeUtc.LocalDateTime,
                    Status = LogStatus.Failed,
                    Message = "作业执行被否决"
                };

                // 创建作用域来解析IJobStorage
                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                await jobStorage.AddJobLogAsync(jobLog, cancellationToken);

                _logger.LogInformation("作业执行被否决，日志记录成功: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录作业执行否决日志失败: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
            }
        }

        /// <summary>
        /// 作业即将执行前调用
        /// </summary>
        /// <param name="context">作业执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // 检查是否是手动触发的作业
                bool isManualTrigger = context.MergedJobDataMap.TryGetValue("IsManualTrigger", out var manualTriggerValue) && 
                                      manualTriggerValue is bool && (bool)manualTriggerValue;
                
                if (!isManualTrigger)
                {
                    // 不是手动触发，检查作业状态
                    using var scope = _scopeFactory.CreateScope();
                    var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                    
                    // 获取作业信息
                    var jobInfo = await jobStorage.GetJobAsync(context.JobDetail.Key.Name, context.JobDetail.Key.Group, cancellationToken);
                    
                    if (jobInfo != null && jobInfo.Status == JobStatus.Paused)
                    {
                        // 作业被暂停，记录日志并抛出异常阻止执行
                        _logger.LogWarning("作业执行被阻止: 作业已被暂停 - {JobKey}", 
                            $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
                        
                        // 抛出JobExecutionException来阻止作业执行
                        throw new JobExecutionException($"作业 {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name} 已被暂停，执行被阻止");
                    }
                }
                else
                {
                    // 是手动触发的作业，即使处于暂停状态也允许执行
                    _logger.LogInformation("手动触发作业，忽略暂停状态检查: {JobKey}", 
                        $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
                }
            }
            catch (JobExecutionException)
            {
                // 重新抛出JobExecutionException，这是期望的行为
                throw;
            }
            catch (Exception ex)
            {
                // 记录检查过程中的错误，但不阻止作业执行
                _logger.LogError(ex, "检查作业暂停状态时发生错误: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}");
            }
        }
    }
}