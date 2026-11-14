using Chet.QuartzNet.Core.Consts;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
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
        private readonly JobExecutionRegistry _registry;
        private readonly RuntimeInstance _instance;
        private readonly ILogger<QuartzJobListener> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scopeFactory">服务作用域工厂</param>
        /// <param name="registry">本进程执行登记表</param>
        /// <param name="instance">当前实例信息</param>
        /// <param name="logger">日志记录器</param>
        public QuartzJobListener(
            IServiceScopeFactory scopeFactory,
            JobExecutionRegistry registry,
            RuntimeInstance instance,
            ILogger<QuartzJobListener> logger
        )
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
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

                var triggerType = ResolveTriggerType(context);
                var logId = ResolveLogId(context);

                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

                // 作业在执行期间被删除：直接收尾，不再补写日志。
                // 否则删除后日志列表会重新出现该作业的「幽灵」记录，看起来像还在运行。
                // 读取失败时按「仍存在」处理，保持原有日志写入行为不被存储抖动影响。
                var jobStillExists = true;
                try
                {
                    jobStillExists =
                        await jobStorage.GetJobAsync(
                            context.JobDetail.Key.Name,
                            context.JobDetail.Key.Group,
                            cancellationToken
                        ) != null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarn("作业执行完成", $"校验作业是否仍存在失败: {ex.Message}");
                }

                if (!jobStillExists)
                {
                    if (logId.HasValue)
                    {
                        _registry.Unregister(logId.Value);
                    }

                    _logger.LogInfo(
                        "作业执行完成",
                        $"作业已删除，跳过日志记录: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                    );
                    return;
                }

                var claimedExisting = false;
                QuartzJobLog? jobLog = null;
                if (logId.HasValue)
                {
                    jobLog = await TryLoadJobLogAsync(
                        jobStorage,
                        logId.Value,
                        cancellationToken
                    );
                    claimedExisting = jobLog != null;
                }

                if (jobLog == null)
                {
                    // 认领失败时必须沿用本次执行的 LogId 走插入，
                    // 否则用全新的 LogId 去 UPDATE 会更新 0 行，运行中记录将永久停留为僵尸记录
                    jobLog = new QuartzJobLog
                    {
                        LogId = logId ?? Guid.NewGuid(),
                        JobName = context.JobDetail.Key.Name,
                        JobGroup = context.JobDetail.Key.Group,
                        TriggerName = context.Trigger.Key.Name,
                        TriggerGroup = context.Trigger.Key.Group,
                        StartTime = context.FireTimeUtc.LocalDateTime,
                        TriggerType = triggerType,
                        InstanceId = _instance.InstanceId,
                        BootId = _instance.BootId,
                    };
                }

                // 停机（立即停止/排水超时）时排水协调器已把该执行标记为「中断」，
                // 这里不要再改写成失败，否则日志会丢掉「下次启动可补偿」的状态
                if (
                    claimedExisting
                    && jobLog!.Status == LogStatus.Interrupted
                    && executionException != null
                    && context.CancellationToken.IsCancellationRequested
                )
                {
                    if (logId.HasValue)
                    {
                        _registry.Unregister(logId.Value);
                    }

                    _logger.LogInfo(
                        "作业执行",
                        "停机导致执行被中断，保留中断状态: {JobKey}",
                        $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                    );
                    return;
                }

                FillExecutionResult(jobLog, context, executionException, triggerType);

                if (claimedExisting)
                {
                    await jobStorage.UpdateJobLogAsync(jobLog, cancellationToken);
                }
                else
                {
                    await jobStorage.AddJobLogAsync(jobLog, cancellationToken);
                }

                if (logId.HasValue)
                {
                    _registry.Unregister(logId.Value);
                }

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
                        jobLog.Message ?? string.Empty,
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
                var currentLogId = ResolveLogId(context);
                if (currentLogId.HasValue)
                {
                    _registry.Unregister(currentLogId.Value);
                }

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
                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                var logId = ResolveLogId(context);

                if (logId.HasValue)
                {
                    var existing = await jobStorage.GetJobLogAsync(logId.Value, cancellationToken);
                    if (existing != null)
                    {
                        var now = DateTime.Now;
                        existing.Status = LogStatus.Failed;
                        existing.EndTime = now;
                        existing.Duration = Math.Max(
                            0,
                            (long)(now - existing.StartTime).TotalMilliseconds
                        );
                        existing.Message = "作业执行被否决";
                        existing.ErrorMessage = "作业执行被否决";
                        await jobStorage.UpdateJobLogAsync(existing, cancellationToken);
                        _registry.Unregister(logId.Value);
                        return;
                    }
                }

                var jobLog = new QuartzJobLog
                {
                    LogId = logId ?? Guid.NewGuid(),
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    TriggerName = context.Trigger.Key.Name,
                    TriggerGroup = context.Trigger.Key.Group,
                    StartTime = context.FireTimeUtc.LocalDateTime,
                    EndTime = DateTime.Now,
                    Status = LogStatus.Failed,
                    Message = "作业执行被否决",
                    TriggerType = ResolveTriggerType(context),
                    InstanceId = _instance.InstanceId,
                    BootId = _instance.BootId,
                };

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
                var triggerType = ResolveTriggerType(context);

                using var scope = _scopeFactory.CreateScope();
                var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                // 手动触发的执行资格已在触发时获得，跳过暂停/依赖预检；
                // 定时触发与中断补偿仍需校验暂停状态
                if (triggerType == TriggerType.Manual)
                {
                    _logger.LogInfo(
                        "作业即将执行",
                        "跳过暂停/依赖校验: {JobKey}, 来源: {TriggerType}",
                        $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}",
                        triggerType
                    );
                }
                else
                {
                    var jobInfo = await jobStorage.GetJobAsync(
                        context.JobDetail.Key.Name,
                        context.JobDetail.Key.Group,
                        cancellationToken
                    );

                    if (jobInfo != null && jobInfo.Status == JobStatus.Paused)
                    {
                        // 暂停作业不执行：记为「跳过」而不是「失败」，
                        // 避免污染失败率并触发失败通知
                        await WriteSkippedLogAsync(
                            jobStorage,
                            context,
                            triggerType,
                            $"作业已被暂停，本次执行已跳过 - {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}",
                            cancellationToken
                        );
                    }

                    // 前置依赖只在定时触发时校验（补偿重跑由策略/人工决定，不再拦截）
                    if (jobInfo != null && triggerType == TriggerType.Cron)
                    {
                        await CheckDependenciesAsync(
                            jobInfo,
                            jobStorage,
                            context,
                            cancellationToken
                        );
                    }
                }

                // 所有可能抛异常阻止执行的校验之后，才创建运行中记录
                await EnsureRunningLogAsync(
                    context,
                    jobStorage,
                    triggerType,
                    cancellationToken
                );
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
                    "准备作业执行日志时发生错误: {JobKey}",
                    $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
                );
            }
        }

        /// <summary>
        /// 创建运行中日志，或认领补偿预建的日志
        /// </summary>
        private async Task EnsureRunningLogAsync(
            IJobExecutionContext context,
            IJobStorage jobStorage,
            TriggerType triggerType,
            CancellationToken cancellationToken
        )
        {
            var now = DateTime.Now;
            var logId = ResolveLogId(context);

            if (logId.HasValue)
            {
                var existing = await jobStorage.GetJobLogAsync(logId.Value, cancellationToken);
                if (existing != null)
                {
                    existing.Status = LogStatus.Running;
                    existing.HeartbeatTime = now;
                    if (existing.StartTime == default)
                    {
                        existing.StartTime = now;
                    }

                    await jobStorage.UpdateJobLogAsync(existing, cancellationToken);
                    _registry.Register(
                        new JobExecutionRegistry.ExecutionEntry(
                            existing.LogId,
                            existing.JobName,
                            existing.JobGroup,
                            existing.StartTime
                        )
                    );
                    return;
                }
            }

            var jobLog = new QuartzJobLog
            {
                LogId = logId ?? Guid.NewGuid(),
                JobName = context.JobDetail.Key.Name,
                JobGroup = context.JobDetail.Key.Group,
                TriggerName = context.Trigger.Key.Name,
                TriggerGroup = context.Trigger.Key.Group,
                Status = LogStatus.Running,
                StartTime = now,
                HeartbeatTime = now,
                CreateTime = now,
                TriggerType = triggerType,
                InstanceId = _instance.InstanceId,
                BootId = _instance.BootId,
            };

            context.MergedJobDataMap.TryGetValue(
                QuartzJobConst.TriggeredBy,
                out var triggeredBy
            );
            jobLog.TriggeredBy = triggeredBy?.ToString();

            context.MergedJobDataMap.TryGetValue(
                QuartzJobConst.RecoverySourceLogId,
                out var sourceLogId
            );
            if (
                sourceLogId != null
                && Guid.TryParse(sourceLogId.ToString(), out var parsedSourceId)
            )
            {
                jobLog.RecoverySourceLogId = parsedSourceId;
            }

            context.MergedJobDataMap.TryGetValue(
                QuartzJobConst.RecoveryAttempt,
                out var attempt
            );
            if (attempt != null && int.TryParse(attempt.ToString(), out var parsedAttempt))
            {
                jobLog.RecoveryAttempt = Math.Max(1, parsedAttempt);
            }

            if (
                context.MergedJobDataMap.TryGetValue(
                    QuartzJobConst.JobData,
                    out var jobDataValue
                )
                && !string.IsNullOrEmpty(jobDataValue?.ToString())
            )
            {
                jobLog.JobData = jobDataValue.ToString();
            }

            await jobStorage.AddJobLogAsync(jobLog, cancellationToken);
            context.MergedJobDataMap[QuartzJobConst.LogId] = jobLog.LogId.ToString();

            _registry.Register(
                new JobExecutionRegistry.ExecutionEntry(
                    jobLog.LogId,
                    jobLog.JobName,
                    jobLog.JobGroup,
                    jobLog.StartTime
                )
            );
        }

        private static void FillExecutionResult(
            QuartzJobLog jobLog,
            IJobExecutionContext context,
            JobExecutionException? executionException,
            TriggerType triggerType
        )
        {
            var now = DateTime.Now;
            var triggerLabel = triggerType switch
            {
                TriggerType.Manual => "（手动触发）",
                TriggerType.Recovery => "（中断补偿重跑）",
                _ => string.Empty,
            };

            jobLog.EndTime = now;
            jobLog.Duration = Math.Max(0, (long)(now - jobLog.StartTime).TotalMilliseconds);
            jobLog.TriggerType = triggerType;

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

            jobLog.Result = context.Result?.ToString();

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
        }

        private static TriggerType ResolveTriggerType(IJobExecutionContext context)
        {
            if (
                context.MergedJobDataMap != null
                && context.MergedJobDataMap.TryGetValue(
                    QuartzJobConst.TriggerType,
                    out var value
                )
                && value != null
                && Enum.TryParse<TriggerType>(value.ToString(), true, out var parsed)
            )
            {
                return parsed;
            }

            // 兼容旧路径：没有 TriggerType 时按 IsManualTrigger 判断
            if (
                context.MergedJobDataMap != null
                && context.MergedJobDataMap.TryGetValue(
                    QuartzJobConst.IsManualTrigger,
                    out var manualFlag
                )
                && manualFlag is bool manual
                && manual
            )
            {
                return TriggerType.Manual;
            }

            return TriggerType.Cron;
        }

        private static Guid? ResolveLogId(IJobExecutionContext context)
        {
            if (
                context.MergedJobDataMap != null
                && context.MergedJobDataMap.TryGetValue(QuartzJobConst.LogId, out var value)
                && value != null
                && Guid.TryParse(value.ToString(), out var logId)
            )
            {
                return logId;
            }

            return null;
        }

        /// <summary>
        /// 读取本次执行对应的运行中日志，读取失败时重试一次。
        /// 连续失败返回 null，由调用方按原 LogId 重新插入，避免收尾彻底丢失。
        /// </summary>
        private async Task<QuartzJobLog?> TryLoadJobLogAsync(
            IJobStorage jobStorage,
            Guid logId,
            CancellationToken cancellationToken
        )
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var jobLog = await jobStorage.GetJobLogAsync(logId, cancellationToken);
                    if (jobLog != null)
                    {
                        return jobLog;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarn(
                        "作业执行完成",
                        $"读取运行中日志失败(第 {attempt + 1} 次): {logId}, {ex.Message}"
                    );
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarn(
                    "作业执行完成",
                    $"未找到运行中日志，将按原 LogId 重新写入收尾结果: {logId}"
                );
            }

            return null;
        }

        /// <summary>
        /// 检查前置依赖是否满足，未满足时记录跳过日志并阻止本次执行
        /// </summary>
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
            await WriteSkippedLogAsync(
                jobStorage,
                context,
                TriggerType.Cron,
                reason,
                cancellationToken
            );
        }

        /// <summary>
        /// 记录一条「跳过」日志并阻止本次执行。
        /// JobWasExecuted 会依据 SkipLogKey 跳过收尾，因此不会被记为失败、也不会触发失败通知。
        /// </summary>
        private async Task WriteSkippedLogAsync(
            IJobStorage jobStorage,
            IJobExecutionContext context,
            TriggerType triggerType,
            string reason,
            CancellationToken cancellationToken
        )
        {
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
                    TriggerType = triggerType,
                    InstanceId = _instance.InstanceId,
                    BootId = _instance.BootId,
                },
                cancellationToken
            );

            _logger.LogWarn(
                "作业即将执行",
                $"{reason} - {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}"
            );

            throw new JobExecutionException(reason);
        }
    }
}
