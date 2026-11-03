using Chet.QuartzNet.Core.Consts;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Chet.QuartzNet.Core.Services
{
    /// <summary>
    /// Quartz作业监听器，用于记录作业执行日志
    /// </summary>
    public class QuartzJobListener : IJobListener
    {
        /// <summary>
        /// 跳过执行原因在作业数据中的键（用于避免重复记录日志）
        /// </summary>
        private const string SkipLogKey = "__DependencySkipReason";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QuartzJobListener> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scopeFactory">服务作用域工厂</param>
        /// <param name="logger">日志记录器</param>
        public QuartzJobListener(
            IServiceScopeFactory scopeFactory,
            ILogger<QuartzJobListener> logger
        )
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
        /// <param name="executionException">作业执行结果</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobWasExecuted(
            IJobExecutionContext context,
            JobExecutionException? executionException,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                // 前置依赖未满足时已在 JobToBeExecuted 中记录跳过日志，此处不再重复记录
                if (
                    executionException != null
                    && context.MergedJobDataMap != null
                    && context.MergedJobDataMap.ContainsKey(SkipLogKey)
                )
                {
                    return;
                }

                var jobLog = new QuartzJobLog
                {
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    TriggerName = context.Trigger.Key.Name,
                    TriggerGroup = context.Trigger.Key.Group,
                    StartTime = context.FireTimeUtc.LocalDateTime,
                    EndTime = DateTime.Now,
                    Duration = (long)
                        (DateTime.Now - context.FireTimeUtc.LocalDateTime).TotalMilliseconds,
                    Result = context.Result?.ToString(),
                };

                // 判断是否手动触发
                var isManualTrigger =
                    context.MergedJobDataMap != null
                    && context.MergedJobDataMap.TryGetValue("IsManualTrigger", out var manualFlag)
                    && manualFlag is bool
                    && (bool)manualFlag;
                var triggerLabel = isManualTrigger ? "（手动触发）" : string.Empty;

                // 处理执行结果
                if (executionException == null)
                {
                    jobLog.Status = LogStatus.Success;
                    jobLog.Message = $"作业执行成功{triggerLabel}";
                }
                else
                {
                    jobLog.Status = LogStatus.Failed;
                    jobLog.Message = $"作业执行失败{triggerLabel}";
                    jobLog.Exception = executionException.ToString();
                    jobLog.ErrorMessage = executionException.Message;
                    jobLog.ErrorStackTrace = executionException.StackTrace;
                }

                // 记录作业数据：保存用户配置的原始 JSON，保证日志展示与配置一致
                if (
                    context.MergedJobDataMap != null
                    && context.MergedJobDataMap.TryGetValue(
                        QuartzJobConst.JobData,
                        out var jobDataValue
                    )
                    && !string.IsNullOrEmpty(jobDataValue?.ToString())
                )
                {
                    jobLog.JobData = jobDataValue.ToString();
                }

                // 创建作用域来解析IJobStorage
                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

                // 记录作业日志
                await jobStorage.AddJobLogAsync(jobLog, cancellationToken);

                // 更新作业的下次执行时间和上次执行时间
                await jobService.UpdateJobExecutionTimesAsync(
                    context.JobDetail.Key.Name,
                    context.JobDetail.Key.Group,
                    context.Trigger,
                    cancellationToken
                );

                // 发送推送通知
                var notificationService = scope.ServiceProvider.GetService<INotificationService>();
                if (notificationService != null)
                {
                    await notificationService.SendJobExecutionNotificationAsync(
                        context.JobDetail.Key.Name,
                        context.JobDetail.Key.Group,
                        executionException == null,
                        jobLog.Message,
                        jobLog.Duration ?? 0,
                        jobLog.ErrorMessage,
                        cancellationToken
                    );
                }

                _logger.LogSuccess(
                    "作业执行完成",
                    $"日志记录成功: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}, 状态: {jobLog.Status}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogFailure(
                    "作业执行完成",
                    $"记录作业执行日志失败: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}",
                    ex
                );
            }
        }

        /// <summary>
        /// 作业执行被否决时调用
        /// </summary>
        /// <param name="context">作业执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobExecutionVetoed(
            IJobExecutionContext context,
            CancellationToken cancellationToken = default
        )
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
                    Message = "作业执行被否决",
                };

                // 创建作用域来解析IJobStorage
                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                await jobStorage.AddJobLogAsync(jobLog, cancellationToken);

                _logger.LogInfo(
                    "作业执行",
                    "作业被否决，日志记录成功: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogFailure(
                    "作业执行被否决",
                    $"记录作业执行否决日志失败: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}",
                    ex
                );
            }
        }

        /// <summary>
        /// 作业即将执行前调用
        /// </summary>
        /// <param name="context">作业执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task JobToBeExecuted(
            IJobExecutionContext context,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                // 检查是否是手动触发的作业
                bool isManualTrigger =
                    context.MergedJobDataMap.TryGetValue(
                        "IsManualTrigger",
                        out var manualTriggerValue
                    )
                    && manualTriggerValue is bool
                    && (bool)manualTriggerValue;

                if (!isManualTrigger)
                {
                    // 不是手动触发，检查作业状态
                    using var scope = _scopeFactory.CreateScope();
                    var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                    // 获取作业信息
                    var jobInfo = await jobStorage.GetJobAsync(
                        context.JobDetail.Key.Name,
                        context.JobDetail.Key.Group,
                        cancellationToken
                    );

                    if (jobInfo != null && jobInfo.Status == JobStatus.Paused)
                    {
                        // 作业被暂停，记录日志并抛出异常阻止执行
                        _logger.LogWarn(
                            "作业即将执行",
                            $"作业执行被阻止: 作业已被暂停 - {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                        );

                        // 抛出JobExecutionException来阻止作业执行
                        throw new JobExecutionException(
                            $"作业 {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name} 已被暂停，执行被阻止"
                        );
                    }

                    // 检查前置依赖：依赖作业最近一次执行成功才允许执行
                    if (jobInfo != null)
                    {
                        await CheckDependenciesAsync(
                            jobInfo,
                            jobStorage,
                            context,
                            cancellationToken
                        );
                    }
                }
                else
                {
                    // 是手动触发的作业，即使处于暂停状态也允许执行
                    _logger.LogInfo(
                        "手动触发作业",
                        "忽略暂停状态检查: {JobKey}",
                        $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                    );
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
                _logger.LogError(
                    ex,
                    "检查作业暂停状态时发生错误: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                );
            }
        }

        /// <summary>
        /// 检查前置依赖是否满足，未满足时记录跳过日志并阻止本次执行
        /// </summary>
        /// <param name="jobInfo">当前作业信息</param>
        /// <param name="jobStorage">作业存储</param>
        /// <param name="context">作业执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task CheckDependenciesAsync(
            QuartzJobInfo jobInfo,
            IJobStorage jobStorage,
            IJobExecutionContext context,
            CancellationToken cancellationToken
        )
        {
            var dependencies = JobDependencyHelper.Deserialize(jobInfo.Dependencies);
            if (dependencies.Count == 0)
            {
                return;
            }

            var unmetReasons = new List<string>();
            foreach (var dependency in dependencies)
            {
                var latestLog = await jobStorage.GetLatestJobLogAsync(
                    dependency.JobName,
                    dependency.JobGroup,
                    cancellationToken
                );

                if (latestLog == null)
                {
                    unmetReasons.Add($"{dependency.JobGroup}.{dependency.JobName} 尚未执行");
                    continue;
                }

                if (latestLog.Status != LogStatus.Success)
                {
                    unmetReasons.Add(
                        $"{dependency.JobGroup}.{dependency.JobName} 最近一次执行状态为 {latestLog.Status}"
                    );
                }
            }

            if (unmetReasons.Count == 0)
            {
                return;
            }

            var reason = $"前置依赖未满足，本次执行已跳过：{string.Join("；", unmetReasons)}";

            // 标记跳过原因，JobWasExecuted 中据此避免重复记录失败日志
            context.MergedJobDataMap[SkipLogKey] = reason;

            await jobStorage.AddJobLogAsync(
                new QuartzJobLog
                {
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    TriggerName = context.Trigger.Key.Name,
                    TriggerGroup = context.Trigger.Key.Group,
                    StartTime = context.FireTimeUtc.LocalDateTime,
                    EndTime = DateTime.Now,
                    Duration = 0,
                    Status = LogStatus.Skipped,
                    Message = reason,
                },
                cancellationToken
            );

            _logger.LogWarn(
                "作业即将执行",
                $"前置依赖未满足，跳过执行: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
            );

            throw new JobExecutionException(reason);
        }
    }
}
