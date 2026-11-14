using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Scheduling;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 中断恢复协调器：扫描过期运行记录、原子认领、按策略补偿。
/// </summary>
public sealed class RecoveryCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly SchedulerInstance _schedulerInstance;
    private readonly RecoveryOptionsProvider _options;
    private readonly RuntimeInstance _instance;
    private readonly RecoverySummaryStore _summary;
    private readonly ILogger<RecoveryCoordinator> _logger;

    /// <summary>
    /// 初始化协调器
    /// </summary>
    public RecoveryCoordinator(
        IServiceScopeFactory scopeFactory,
        ISchedulerFactory schedulerFactory,
        SchedulerInstance schedulerInstance,
        RecoveryOptionsProvider options,
        RuntimeInstance instance,
        RecoverySummaryStore summary,
        ILogger<RecoveryCoordinator> logger
    )
    {
        _scopeFactory = scopeFactory;
        _schedulerFactory = schedulerFactory;
        _schedulerInstance = schedulerInstance;
        _options = options;
        _instance = instance;
        _summary = summary;
        _logger = logger;
    }

    /// <summary>
    /// 取当前调度器实例（优先复用进程内缓存，避免隐式创建未启动的新实例）
    /// </summary>
    private async Task<IScheduler> GetSchedulerAsync(CancellationToken cancellationToken)
    {
        return _schedulerInstance.Current
            ?? await _schedulerFactory.GetScheduler(cancellationToken);
    }

    /// <summary>
    /// 执行一次启动恢复（延迟 + 扫描 + 补偿）
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsInitialized)
        {
            _logger.LogWarning("恢复配置尚未加载，跳过启动补偿");
            return;
        }

        _summary.Begin();
        try
        {
            var options = _options.Current;
            if (options.CompensationDelaySeconds > 0)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.CompensationDelaySeconds),
                    cancellationToken
                );
            }

            await RecoverAsync(options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 应用正在停止，保留 Pending 由下次启动继续
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动中断补偿失败");
        }
        finally
        {
            _summary.Complete();
        }
    }

    /// <summary>
    /// 补偿遗留的待处理中断记录（只处理已判定中断且仍在等待补偿的记录）。
    /// 启动扫描只跑一次，而进程被强杀留下的 Running 记录要等心跳过期后
    /// 才会被心跳服务标记为中断；若此时没人触发补偿，就要等到下次启动才能补上。
    /// 这里提供给周期任务调用，把「标记中断」与「补偿」接起来。
    /// </summary>
    public async Task RunPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsInitialized)
        {
            return;
        }

        var options = _options.Current;
        if (!options.RecoveryEnabled || options.CatchUpStrategy == CatchUpStrategy.None)
        {
            return;
        }

        try
        {
            var scheduler = await GetSchedulerAsync(cancellationToken);
            if (scheduler.IsShutdown || !scheduler.IsStarted || scheduler.InStandbyMode)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
            var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

            var staleBefore = DateTime.Now.AddSeconds(-options.LeaseDurationSeconds);
            var candidates = await storage.GetRecoverableLogsAsync(
                staleBefore,
                200,
                cancellationToken
            );

            var pending = candidates
                .Where(l =>
                    l.Status == LogStatus.Interrupted
                    && l.CompensationStatus == CompensationStatus.Pending
                )
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            var selected = await SelectByCatchUpStrategyAsync(
                pending,
                options,
                storage,
                cancellationToken
            );

            foreach (var log in selected)
            {
                await HandleCandidateAsync(
                    log,
                    options,
                    storage,
                    jobService,
                    scheduler,
                    cancellationToken
                );
            }

            _logger.LogInformation("已处理 {Count} 条待补偿的中断记录", selected.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 应用正在停止，留下 Pending 由下次继续
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理待补偿中断记录失败");
        }
    }

    /// <summary>
    /// 手动重新执行一条中断记录
    /// </summary>
    public async Task<ApiResponseDto<bool>> RerunAsync(
        Guid logId,
        string? triggeredBy,
        CancellationToken cancellationToken = default
    )
    {
        using var scope = _scopeFactory.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
        var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

        var log = await storage.GetJobLogAsync(logId, cancellationToken);
        if (log == null)
        {
            return ApiResponseDto<bool>.ErrorResponse("日志不存在", "LOG_NOT_FOUND");
        }

        if (log.Status != LogStatus.Interrupted)
        {
            return ApiResponseDto<bool>.ErrorResponse(
                "只有中断记录可以重新执行",
                "LOG_NOT_INTERRUPTED"
            );
        }

        if (log.CompensationStatus == CompensationStatus.Triggered)
        {
            return ApiResponseDto<bool>.ErrorResponse(
                "该记录已补偿，不能重复执行",
                "LOG_ALREADY_COMPENSATED"
            );
        }

        // 必须在认领补偿权、创建 Running 预建日志之前校验：
        // 调度器未运行/待机时 TriggerJob 会「成功」却不执行，会留下永不收尾的运行中记录，
        // 且原中断记录已被标记为已补偿，补偿资格白白丢失。
        var scheduler = await GetSchedulerAsync(cancellationToken);
        if (scheduler.IsShutdown || !scheduler.IsStarted || scheduler.InStandbyMode)
        {
            return ApiResponseDto<bool>.ErrorResponse(
                "调度器未运行（已停止或处于排水待机），请先到作业管理页启动调度器再重新执行",
                "SCHEDULER_NOT_RUNNING"
            );
        }

        var job = await storage.GetJobAsync(log.JobName, log.JobGroup, cancellationToken);
        if (job == null)
        {
            return ApiResponseDto<bool>.ErrorResponse("作业不存在", "JOB_NOT_FOUND");
        }

        // 暂停/禁用的作业执行会被监听器跳过，若先认领补偿再触发，
        // 补偿资格会被白白消耗且记录停在 Triggered（前端不再允许重跑），因此提前拒绝
        if (!job.IsEnabled || job.Status == JobStatus.Paused)
        {
            return ApiResponseDto<bool>.ErrorResponse(
                job.IsEnabled
                    ? "作业已暂停，请先恢复作业后再重新执行"
                    : "作业已禁用，请先启用作业后再重新执行",
                "JOB_NOT_RUNNABLE"
            );
        }

        if (!await storage.TryClaimCompensationAsync(log.LogId, cancellationToken))
        {
            return ApiResponseDto<bool>.ErrorResponse(
                "该记录已被其他操作补偿",
                "LOG_ALREADY_COMPENSATED"
            );
        }

        var now = DateTime.Now;
        var compensationLog = new QuartzJobLog
        {
            LogId = Guid.NewGuid(),
            JobName = log.JobName,
            JobGroup = log.JobGroup,
            TriggerName = job.TriggerName,
            TriggerGroup = job.TriggerGroup,
            Status = LogStatus.Running,
            StartTime = now,
            CreateTime = now,
            HeartbeatTime = now,
            TriggerType = TriggerType.Recovery,
            TriggeredBy = triggeredBy,
            RecoverySourceLogId = log.LogId,
            RecoveryAttempt = log.RecoveryAttempt + 1,
            InstanceId = _instance.InstanceId,
            BootId = _instance.BootId,
            Message = "手动重新执行已提交",
        };
        await storage.AddJobLogAsync(compensationLog, cancellationToken);

        var triggerResult = await jobService.TriggerJobWithContextAsync(
            job.JobName,
            job.JobGroup,
            TriggerType.Recovery,
            compensationLog.LogId,
            log.LogId,
            compensationLog.RecoveryAttempt,
            triggeredBy,
            cancellationToken
        );

        if (!triggerResult.Success)
        {
            compensationLog.Status = LogStatus.Failed;
            compensationLog.EndTime = DateTime.Now;
            compensationLog.Duration = 0;
            compensationLog.Message = "手动重新执行触发失败";
            compensationLog.ErrorMessage = triggerResult.Message;
            await storage.UpdateJobLogAsync(compensationLog, cancellationToken);
            await storage.MarkCompensationResultAsync(
                log.LogId,
                CompensationStatus.Failed,
                compensationLog.LogId,
                triggerResult.Message,
                cancellationToken
            );
            return ApiResponseDto<bool>.ErrorResponse(
                triggerResult.Message,
                "TRIGGER_FAILED"
            );
        }

        await storage.MarkCompensationResultAsync(
            log.LogId,
            CompensationStatus.Triggered,
            compensationLog.LogId,
            null,
            cancellationToken
        );

        return ApiResponseDto<bool>.SuccessResponse(true, "已提交重新执行");
    }

    private async Task RecoverAsync(SystemConfigDto options, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
        var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();
        var scheduler = await GetSchedulerAsync(cancellationToken);

        var staleBefore = DateTime.Now.AddSeconds(-options.LeaseDurationSeconds);
        var candidates = await storage.GetRecoverableLogsAsync(
            staleBefore,
            1000,
            cancellationToken
        );

        if (candidates.Count == 0)
        {
            return;
        }

        var pending = new List<QuartzJobLog>();
        foreach (var candidate in candidates)
        {
            if (candidate.Status == LogStatus.Running)
            {
                var marked = await storage.TryMarkInterruptedAsync(
                    candidate.LogId,
                    _instance.InstanceId,
                    DateTime.Now,
                    "进程中断，未捕获到执行结束事件",
                    cancellationToken
                );
                if (!marked)
                {
                    continue;
                }

                _summary.AddInterrupted();
                pending.Add(
                    await storage.GetJobLogAsync(candidate.LogId, cancellationToken) ?? candidate
                );
            }
            else
            {
                // 上次补偿中途退出，接手遗留的 Pending
                _summary.AddInterrupted();
                pending.Add(candidate);
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        if (!scheduler.IsStarted || scheduler.IsShutdown)
        {
            _logger.LogWarning(
                "调度器未启动，{Count} 条中断记录保留为待补偿",
                pending.Count
            );
            return;
        }

        var selected = await SelectByCatchUpStrategyAsync(
            pending,
            options,
            storage,
            cancellationToken
        );
        foreach (var log in selected)
        {
            await HandleCandidateAsync(
                log,
                options,
                storage,
                jobService,
                scheduler,
                cancellationToken
            );
        }
    }

    private async Task<List<QuartzJobLog>> SelectByCatchUpStrategyAsync(
        List<QuartzJobLog> pending,
        SystemConfigDto options,
        IJobStorage storage,
        CancellationToken cancellationToken
    )
    {
        if (options.CatchUpStrategy != CatchUpStrategy.Latest || pending.Count <= 1)
        {
            return pending;
        }

        var latestIds = pending
            .GroupBy(l => new { l.JobName, l.JobGroup })
            .Select(g => g.OrderByDescending(l => l.StartTime).First().LogId)
            .ToHashSet();

        foreach (var older in pending.Where(l => !latestIds.Contains(l.LogId)))
        {
            await storage.MarkCompensationResultAsync(
                older.LogId,
                CompensationStatus.Skipped,
                null,
                "已有更新的中断记录",
                cancellationToken
            );
            _summary.Record(
                older.LogId,
                older.JobName,
                older.JobGroup,
                older.RecoveryAttempt,
                RecoveryOutcome.Skipped,
                "已有更新的中断记录"
            );
        }

        return pending.Where(l => latestIds.Contains(l.LogId)).ToList();
    }

    private async Task HandleCandidateAsync(
        QuartzJobLog log,
        SystemConfigDto options,
        IJobStorage storage,
        IQuartzJobService jobService,
        IScheduler scheduler,
        CancellationToken cancellationToken
    )
    {
        var job = await storage.GetJobAsync(log.JobName, log.JobGroup, cancellationToken);
        var nextFireTime = await GetNextFireTimeAsync(scheduler, log, cancellationToken);
        var (decision, reason) = RecoveryPolicy.Evaluate(
            log,
            job,
            options,
            nextFireTime,
            DateTime.Now
        );

        if (decision == RecoveryDecision.Skip)
        {
            await storage.MarkCompensationResultAsync(
                log.LogId,
                CompensationStatus.Skipped,
                null,
                reason,
                cancellationToken
            );
            _summary.Record(
                log.LogId,
                log.JobName,
                log.JobGroup,
                log.RecoveryAttempt,
                RecoveryOutcome.Skipped,
                reason
            );
            return;
        }

        await TriggerCompensationAsync(
            log,
            job!,
            storage,
            jobService,
            cancellationToken
        );
    }

    private async Task TriggerCompensationAsync(
        QuartzJobLog source,
        QuartzJobInfo job,
        IJobStorage storage,
        IQuartzJobService jobService,
        CancellationToken cancellationToken
    )
    {
        if (!await storage.TryClaimCompensationAsync(source.LogId, cancellationToken))
        {
            _summary.Record(
                source.LogId,
                source.JobName,
                source.JobGroup,
                source.RecoveryAttempt,
                RecoveryOutcome.Skipped,
                "补偿已被其他实例抢占"
            );
            return;
        }

        var now = DateTime.Now;
        var compensationLog = new QuartzJobLog
        {
            LogId = Guid.NewGuid(),
            JobName = job.JobName,
            JobGroup = job.JobGroup,
            TriggerName = job.TriggerName,
            TriggerGroup = job.TriggerGroup,
            Status = LogStatus.Running,
            StartTime = now,
            CreateTime = now,
            HeartbeatTime = now,
            TriggerType = TriggerType.Recovery,
            RecoverySourceLogId = source.LogId,
            RecoveryAttempt = source.RecoveryAttempt + 1,
            InstanceId = _instance.InstanceId,
            BootId = _instance.BootId,
            Message = "中断补偿重跑已提交",
        };
        await storage.AddJobLogAsync(compensationLog, cancellationToken);

        var result = await jobService.TriggerJobWithContextAsync(
            job.JobName,
            job.JobGroup,
            TriggerType.Recovery,
            compensationLog.LogId,
            source.LogId,
            compensationLog.RecoveryAttempt,
            "system",
            cancellationToken
        );

        if (result.Success)
        {
            await storage.MarkCompensationResultAsync(
                source.LogId,
                CompensationStatus.Triggered,
                compensationLog.LogId,
                null,
                cancellationToken
            );
            _summary.Record(
                source.LogId,
                source.JobName,
                source.JobGroup,
                source.RecoveryAttempt,
                RecoveryOutcome.Rerun,
                null
            );
            return;
        }

        compensationLog.Status = LogStatus.Failed;
        compensationLog.EndTime = DateTime.Now;
        compensationLog.Duration = 0;
        compensationLog.Message = "中断补偿触发失败";
        compensationLog.ErrorMessage = result.Message;
        await storage.UpdateJobLogAsync(compensationLog, cancellationToken);
        await storage.MarkCompensationResultAsync(
            source.LogId,
            CompensationStatus.Failed,
            compensationLog.LogId,
            result.Message,
            cancellationToken
        );
        _summary.Record(
            source.LogId,
            source.JobName,
            source.JobGroup,
            source.RecoveryAttempt,
            RecoveryOutcome.Failed,
            result.Message
        );
    }

    private static async Task<DateTime?> GetNextFireTimeAsync(
        IScheduler scheduler,
        QuartzJobLog log,
        CancellationToken cancellationToken
    )
    {
        var jobKey = new JobKey(log.JobName, log.JobGroup);
        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            return null;
        }

        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        return triggers
            .Select(t => t.GetNextFireTimeUtc()?.LocalDateTime)
            .Where(t => t.HasValue)
            .Min();
    }
}
