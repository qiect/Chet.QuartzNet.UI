using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Consts;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Jobs;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Core.Scheduling;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// Quartz作业服务实现
/// </summary>
public class QuartzJobService : IQuartzJobService
{
    /// <summary>
    /// 当前调度器实例（首次使用时从进程内缓存取，必要时才向工厂申请新实例）
    /// </summary>
    private IScheduler? _scheduler;

    /// <summary>
    /// 调度器实例。
    /// Quartz 关闭调度器后会把它从实例仓库移除，此时直接调用
    /// ISchedulerFactory.GetScheduler 会隐式创建一个「从未启动」的新实例，
    /// 使已停止的调度器显示成「待机」。这里统一复用进程内缓存的当前实例。
    /// </summary>
    private IScheduler Scheduler =>
        _scheduler
        ?? _schedulerInstance.Current
        ?? throw new InvalidOperationException("调度器实例尚未初始化，请先启动调度器");

    /// <summary>
    /// 确保调度器实例已创建。
    /// 只用于启动等确实需要实例的异步流程，
    /// 避免在属性 getter 中通过 GetAwaiter().GetResult() 同步等待异步创建。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度器实例</returns>
    private async Task<IScheduler> EnsureSchedulerAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is not null)
        {
            return _scheduler;
        }

        var current = _schedulerInstance.Current;
        if (current is not null)
        {
            _scheduler = current;
            return current;
        }

        // 从未创建过实例（例如启动早期），这里按需创建一次并缓存
        var created = await _schedulerInstance.GetSchedulerAsync(
            _schedulerFactory,
            cancellationToken
        );
        _scheduler = created;
        return created;
    }

    /// <summary>
    /// 调度器是否可用（未关闭）。
    /// 调度器关闭后 Quartz 的所有调度 API 都会抛 <see cref="SchedulerException"/>，
    /// 因此作业管理类操作必须先判断，再决定是否操作调度器，避免存储侧操作被连带阻断。
    /// </summary>
    private bool IsSchedulerAvailable
    {
        get
        {
            var scheduler = _schedulerInstance.Current ?? _scheduler;
            return scheduler is not null && !scheduler.IsShutdown;
        }
    }

    /// <summary>
    /// 调度器工厂
    /// </summary>
    private readonly ISchedulerFactory _schedulerFactory;

    /// <summary>
    /// 进程内当前调度器实例持有者
    /// </summary>
    private readonly SchedulerInstance _schedulerInstance;

    /// <summary>
    /// 作业存储接口
    /// </summary>
    private readonly IJobStorage _jobStorage;

    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<QuartzJobService> _logger;

    /// <summary>
    /// Quartz UI配置选项
    /// </summary>
    private readonly QuartzUIOptions _options;

    /// <summary>
    /// 作业类扫描器
    /// </summary>
    private readonly JobClassScanner _jobClassScanner;

    /// <summary>
    /// 通知服务
    /// </summary>
    private readonly INotificationService _notificationService;

    private readonly DrainCoordinator _drainCoordinator;
    private readonly RecoveryCoordinator _recoveryCoordinator;
    private readonly RecoverySummaryStore _recoverySummary;
    private readonly RecoveryOptionsProvider _recoveryOptions;
    private readonly RuntimeInstance _runtimeInstance;

    /// <summary>
    /// 初始化QuartzJobService实例
    /// </summary>
    /// <param name="schedulerFactory">调度器工厂</param>
    /// <param name="jobStorage">作业存储接口</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">Quartz UI配置选项</param>
    /// <param name="jobClassScanner">作业类扫描器</param>
    /// <param name="notificationService">通知服务</param>
    /// <param name="drainCoordinator">排水协调器</param>
    /// <param name="recoveryCoordinator">恢复协调器</param>
    /// <param name="recoverySummary">恢复汇总</param>
    /// <param name="recoveryOptions">启动期配置快照</param>
    /// <param name="runtimeInstance">运行实例信息</param>
    /// <param name="schedulerInstance">进程内当前调度器实例持有者</param>
    public QuartzJobService(
        ISchedulerFactory schedulerFactory,
        IJobStorage jobStorage,
        ILogger<QuartzJobService> logger,
        IOptions<QuartzUIOptions> options,
        JobClassScanner jobClassScanner,
        INotificationService notificationService,
        DrainCoordinator drainCoordinator,
        RecoveryCoordinator recoveryCoordinator,
        RecoverySummaryStore recoverySummary,
        RecoveryOptionsProvider recoveryOptions,
        RuntimeInstance runtimeInstance,
        SchedulerInstance schedulerInstance
    )
    {
        _schedulerFactory = schedulerFactory;
        _schedulerInstance = schedulerInstance;
        // 复用进程内已有实例：状态查询等只读场景不能因为工厂隐式创建而"复活"已关闭的调度器
        _scheduler = schedulerInstance.Current;
        _jobStorage = jobStorage;
        _logger = logger;
        _options = options.Value;
        _jobClassScanner = jobClassScanner;
        _notificationService = notificationService;
        _drainCoordinator = drainCoordinator;
        _recoveryCoordinator = recoveryCoordinator;
        _recoverySummary = recoverySummary;
        _recoveryOptions = recoveryOptions;
        _runtimeInstance = runtimeInstance;
    }

    #region 调度器
    /// <summary>
    /// 获取调度器状态信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度器状态信息</returns>
    public async Task<ApiResponseDto<SchedulerStatusDto>> GetSchedulerStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 只读状态必须使用进程内缓存的实例：调度器关闭后 Quartz 会移除该实例，
            // 此时向工厂索取会静默创建「从未启动」的新实例，把「已停止」显示成「待机」。
            // 只有在从未创建过实例时才按需创建一次（异步创建，不再同步阻塞）
            var scheduler = _schedulerInstance.Current ?? await EnsureSchedulerAsync(cancellationToken);

            // Quartz 关闭后 IsStarted 仍可能为 true，这里统一以「已关闭」为准，
            // 否则前端会既不显示「启动调度器」又禁用了排水/停止，陷入无法操作的死状态
            var isShutdown = scheduler.IsShutdown;

            var status = new SchedulerStatusDto
            {
                SchedulerName = scheduler.SchedulerName,
                SchedulerInstanceId = scheduler.SchedulerInstanceId,
                RuntimeInstanceId = _runtimeInstance.InstanceId,
                IsStarted = !isShutdown && scheduler.IsStarted,
                IsShutdown = isShutdown,
                InStandbyMode = !isShutdown && scheduler.InStandbyMode,
                Status = GetStatusText(scheduler),
                ShutdownMode = _recoveryOptions.Current.ShutdownMode,
                // 线程池容量：优先取调度器元数据中的真实值（配置可能被宿主覆盖）
                ThreadPoolSize = _options.ThreadPoolSize,
                Version = typeof(IScheduler).Assembly.GetName().Version?.ToString() ?? "Unknown",
            };

            // 如果调度器已停止，避免调用会抛出异常的方法
            if (!scheduler.IsShutdown)
            {
                // 调度器可能正处于待机/关闭过程中，单条查询失败不应让整个状态接口失败，
                // 否则前端会一直停留在上一次的「排水」状态上刷新不出来
                try
                {
                    // 从调度器元数据获取真实启动时间，IScheduler 上没有 StartTime 属性
                    var metadata = await scheduler.GetMetaData(cancellationToken);
                    status.StartTime = metadata.RunningSince?.LocalDateTime;

                    // 以调度器实际线程池容量为准，配置项只作为兜底
                    if (metadata.ThreadPoolSize > 0)
                    {
                        status.ThreadPoolSize = metadata.ThreadPoolSize;
                    }

                    var jobKeys = await scheduler.GetJobKeys(
                        GroupMatcher<JobKey>.AnyGroup(),
                        cancellationToken
                    );
                    var currentlyExecuting = await scheduler.GetCurrentlyExecutingJobs(
                        cancellationToken
                    );
                    status.JobCount = jobKeys.Count;
                    status.ExecutingJobCount = currentlyExecuting.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarn("获取调度器实时状态", $"部分状态获取失败: {ex.Message}");
                }
            }
            else
            {
                // 调度器已停止，设置默认值
                status.JobCount = 0;
                status.ExecutingJobCount = 0;
            }

            if (status.StartTime.HasValue)
            {
                status.RunningTime = (long)
                    (DateTime.Now - status.StartTime.Value).TotalMilliseconds;
            }

            status.IsDraining = _drainCoordinator.IsDraining;
            status.IsStopping = _drainCoordinator.IsStopping;
            status.DrainStartedAt = _drainCoordinator.StartedAt;
            status.DrainDeadline = _drainCoordinator.Deadline;
            status.RemainingExecutingCount = status.IsDraining
                ? _drainCoordinator.RemainingExecutingCount
                : 0;
            status.PendingCompensationCount = _recoverySummary.Snapshot().PendingCount;

            // 排水流程优先描述状态：否则关闭调度器的瞬间会显示成「待机」，
            // 与正在进行中的排水相互矛盾
            if (status.IsDraining)
            {
                status.Status = status.IsStopping ? "正在停止" : "排水";
            }

            return ApiResponseDto<SchedulerStatusDto>.SuccessResponse(status);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetSchedulerStatus", ex);
            return ApiResponseDto<SchedulerStatusDto>.ErrorResponse(
                $"获取调度器状态失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 启动调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> StartSchedulerAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 调度器实例可能尚未创建（例如启动早期或初始化失败），先按需创建
            await EnsureSchedulerAsync(cancellationToken);

            if (Scheduler.IsShutdown)
            {
                // 如果调度器已关闭，重新创建调度器实例（关闭后原实例已从 Quartz 仓库移除）
                var recreated = await _schedulerFactory.GetScheduler(cancellationToken);
                _scheduler = recreated;
                _schedulerInstance.Set(recreated);
                _logger.LogInfoStructured("重新创建调度器实例");
            }

            // Quartz 的 IsStarted 在「待机」状态下依然为 true，
            // 因此必须同时判断 InStandbyMode，否则从待机恢复时不会真正调用 Start
            if (!Scheduler.IsStarted || Scheduler.InStandbyMode)
            {
                await Scheduler.Start(cancellationToken);
                _logger.LogSuccess("启动调度器");
            }

            // 调度器已重新运行，清空上一次排水留下的阶段，避免状态卡片停留在「排水」语义上
            _drainCoordinator.Reset();

            // 重新加载所有正常状态的作业到调度器
            await ReloadNormalJobsAsync(cancellationToken);

            // 启动前若还有待补偿记录（上次调度器未启动导致跳过），补跑一次恢复流程
            if (_recoverySummary.Snapshot().PendingCount > 0)
            {
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await _recoveryCoordinator.RunAsync(CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "调度器启动后的补偿流程失败");
                        }
                    },
                    CancellationToken.None
                );
            }

            return ApiResponseDto<bool>.SuccessResponse(true, "调度器启动成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("启动调度器", ex);
            return ApiResponseDto<bool>.ErrorResponse($"启动调度器失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新加载所有正常状态的作业到调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功与失败的作业数量</returns>
    private async Task<(int Loaded, int Failed)> ReloadNormalJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var loaded = 0;
        var failed = 0;
        try
        {
            // 获取所有存储的作业
            var allJobs = await _jobStorage.GetAllJobsAsync(cancellationToken);
            _logger.LogInfo("重新加载作业到调度器", "共计 {JobCount} 个", allJobs.Count);

            foreach (var jobInfo in allJobs)
            {
                try
                {
                    // 检查作业是否已经有触发器
                    var jobKey = new JobKey(jobInfo.JobName, jobInfo.JobGroup);
                    var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                    if (triggers.Any())
                    {
                        _logger.LogInfo(
                            "作业 {JobKey} 已存在 {TriggerCount} 个触发器，跳过重新加载",
                            $"{jobInfo.JobGroup}.{jobInfo.JobName}",
                            triggers.Count
                        );
                        continue;
                    }

                    // 检查作业是否被禁用，如果是禁用状态，不重新加载
                    if (!jobInfo.IsEnabled)
                    {
                        _logger.LogInfo(
                            "作业 {JobKey} 处于禁用状态，跳过重新加载",
                            $"{jobInfo.JobGroup}.{jobInfo.JobName}"
                        );
                        continue;
                    }

                    // 检查作业状态，如果是暂停状态，不重新加载
                    if (jobInfo.Status == Chet.QuartzNet.Models.Entities.JobStatus.Paused)
                    {
                        _logger.LogInfo(
                            "作业 {JobKey} 处于暂停状态，跳过重新加载",
                            $"{jobInfo.JobGroup}.{jobInfo.JobName}"
                        );
                        continue;
                    }

                    // 重新调度作业
                    await ScheduleJobAsync(jobInfo, cancellationToken);
                    loaded++;
                    _logger.LogSuccess(
                        "ReloadNormalJobs",
                        $"作业 {jobInfo.JobGroup}.{jobInfo.JobName} 重新加载成功"
                    );
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(
                        ex,
                        "重新加载作业失败: {JobKey}",
                        $"{jobInfo.JobGroup}.{jobInfo.JobName}"
                    );
                    // 忽略单个作业的错误，继续处理其他作业
                }
            }

            _logger.LogSuccess(
                "ReloadNormalJobs",
                $"作业重新加载完成：成功 {loaded} 个，失败 {failed} 个"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ReloadNormalJobs", ex);
            return (loaded, Math.Max(failed, 1));
        }

        return (loaded, failed);
    }

    /// <summary>
    /// 把存储中启用且未暂停的作业重新加载到调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> ReloadJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        // 允许在从未创建过实例时按需创建（启动早期），但已关闭的调度器不再复活
        var scheduler = await EnsureSchedulerAsync(cancellationToken);
        if (scheduler.IsShutdown)
        {
            return ApiResponseDto<bool>.ErrorResponse(
                "调度器已停止，无法重新加载作业，请先启动调度器",
                "SCHEDULER_NOT_RUNNING"
            );
        }

        var (loaded, failed) = await ReloadNormalJobsAsync(cancellationToken);
        return failed > 0
            ? ApiResponseDto<bool>.SuccessResponse(
                false,
                $"作业重新加载完成：成功 {loaded} 个，失败 {failed} 个"
            )
            : ApiResponseDto<bool>.SuccessResponse(true, $"作业重新加载完成：成功 {loaded} 个");
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> ShutdownSchedulerAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!Scheduler.IsShutdown)
            {
                // 直接停止等同于立即停止：Shutdown 会中断执行中的任务，
                // 必须在关闭前把残留执行标记为中断，否则监听器来不及收尾，
                // 日志会永久停留在「运行中」并一直读秒
                await _drainCoordinator.MarkActiveExecutionsInterruptedAsync(
                    "调度器被手动停止，执行未正常完成"
                );

                await Scheduler.Shutdown(cancellationToken);
                _logger.LogSuccess("ShutdownScheduler");
            }

            return ApiResponseDto<bool>.SuccessResponse(true, "调度器停止成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ShutdownScheduler", ex);
            return ApiResponseDto<bool>.ErrorResponse($"停止调度器失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 排水：停止接收新触发，等待执行中任务收尾后自动停止
    /// </summary>
    public Task<ApiResponseDto<bool>> DrainSchedulerAsync(
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default
    )
    {
        return _drainCoordinator.BeginAsync(timeoutSeconds, cancellationToken);
    }

    /// <summary>
    /// 取消排水并恢复调度
    /// </summary>
    public Task<ApiResponseDto<bool>> ResumeSchedulerAsync(
        CancellationToken cancellationToken = default
    )
    {
        return _drainCoordinator.ResumeAsync(cancellationToken);
    }

    /// <summary>
    /// 获取本次启动的中断恢复汇总
    /// </summary>
    public Task<ApiResponseDto<RecoverySummaryDto>> GetRecoverySummaryAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(
            ApiResponseDto<RecoverySummaryDto>.SuccessResponse(_recoverySummary.Snapshot())
        );
    }

    /// <summary>
    /// 手动重新执行一条中断日志
    /// </summary>
    public Task<ApiResponseDto<bool>> RerunInterruptedLogAsync(
        Guid logId,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default
    )
    {
        return _recoveryCoordinator.RerunAsync(logId, triggeredBy, cancellationToken);
    }

    /// <summary>
    /// 手动中断在 SkipReason 中的固定前缀。
    /// 手动中断与不可预期中断共用「中断」状态，靠此前缀在展示层区分，不做为查询/统计依据。
    /// </summary>
    public const string ManualInterruptPrefix = "【手动中断】";

    /// <summary>
    /// 手动中断一条运行中日志对应的执行实例。
    /// 仅对当前节点内、且会观察 <see cref="IJobExecutionContext.CancellationToken"/> 的作业生效：
    /// Quartz 的 Interrupt 是协作式取消，不会强行终止线程。
    /// </summary>
    /// <param name="logId">运行中日志ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> InterruptRunningJobAsync(
        Guid logId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var log = await _jobStorage.GetJobLogAsync(logId, cancellationToken);
            if (log == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("日志不存在", "LOG_NOT_FOUND");
            }

            if (log.Status != LogStatus.Running)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "只有运行中的作业可以中断",
                    "LOG_NOT_RUNNING"
                );
            }

            // 停机/重启后残留的 Running 记录，本进程里并不存在这个执行，不能误标中断
            if (
                log.InstanceId != _runtimeInstance.InstanceId
                || (log.BootId.HasValue && log.BootId != _runtimeInstance.BootId)
            )
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "该执行不在当前节点，或已随进程重启结束，无法中断",
                    "LOG_NOT_ON_THIS_NODE"
                );
            }

            if (!IsSchedulerAvailable)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器已停止，无法中断作业",
                    "SCHEDULER_NOT_RUNNING"
                );
            }

            var jobKey = new JobKey(log.JobName, log.JobGroup);

            // 同一 JobKey 允许并发时，按 FireTime 找最接近本次日志的一条，
            // 精确到 fireInstanceId，避免把该作业的其它并发实例一起中断
            var target = (await Scheduler.GetCurrentlyExecutingJobs(cancellationToken))
                .Where(c => c.JobDetail.Key.Equals(jobKey))
                .OrderBy(c =>
                    Math.Abs((c.FireTimeUtc.LocalDateTime - log.StartTime).TotalMilliseconds)
                )
                .FirstOrDefault();

            if (target == null)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "未找到正在执行的实例，可能已结束",
                    "EXECUTION_NOT_FOUND"
                );
            }

            const string reason = ManualInterruptPrefix + "用户手动中断作业";

            // 必须先标记再中断：中断会让作业立刻抛取消异常，
            // 若监听器抢先写入就会把状态改成「失败」；而监听器对
            // 「已标记中断 + 令牌已取消」的记录会保留中断状态
            if (
                !await _jobStorage.TryMarkInterruptedAsync(
                    logId,
                    _runtimeInstance.InstanceId,
                    DateTime.Now,
                    reason,
                    cancellationToken
                )
            )
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "作业已结束，无法中断",
                    "EXECUTION_FINISHED"
                );
            }

            // TryMarkInterrupted 会把补偿状态置为待补偿，手动中断不应进入自动补偿
            await _jobStorage.MarkCompensationResultAsync(
                logId,
                CompensationStatus.Skipped,
                null,
                reason,
                cancellationToken
            );

            // 再中断：优先精确到本次执行，失败时回退到 JobKey 维度
            var interrupted =
                await Scheduler.Interrupt(target.FireInstanceId, cancellationToken)
                || await Scheduler.Interrupt(jobKey, cancellationToken);

            if (!interrupted)
            {
                // 极端情况：标记后发现执行已结束，状态由监听器按真实结果覆盖
                return ApiResponseDto<bool>.ErrorResponse(
                    "作业已结束，无法中断",
                    "EXECUTION_FINISHED"
                );
            }

            _logger.LogSuccess("中断作业", $"{log.JobGroup}.{log.JobName}, 日志: {logId}");
            return ApiResponseDto<bool>.SuccessResponse(true, "已请求中断作业");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("中断作业", ex);
            return ApiResponseDto<bool>.ErrorResponse($"中断作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取调度器状态的中文描述
    /// </summary>
    /// <param name="scheduler">调度器实例</param>
    /// <returns>调度器状态描述</returns>
    private static string GetStatusText(IScheduler scheduler)
    {
        if (scheduler.IsShutdown)
            return "已停止";
        if (scheduler.InStandbyMode)
            return "待机";
        if (scheduler.IsStarted)
            return "运行中";
        return "未知";
    }

    #endregion

    #region 作业管理

    /// <summary>
    /// 添加新的作业
    /// </summary>
    /// <param name="jobDto">作业数据传输对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> AddJobAsync(
        QuartzJobDto jobDto,
        CancellationToken cancellationToken = default
    )
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
            var existingJob = await _jobStorage.GetJobAsync(
                jobDto.JobName,
                jobDto.JobGroup,
                cancellationToken
            );
            if (existingJob != null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业已存在");
            }

            // 验证作业类型
            if (!ValidateJobType(jobDto.JobClassOrApi, jobDto.JobType))
            {
                return ApiResponseDto<bool>.ErrorResponse("无效的作业类型");
            }

            // 验证前置依赖（存在性、自依赖、循环依赖）
            var dependencyValidation = await ValidateDependenciesAsync(
                jobDto.JobName,
                jobDto.JobGroup,
                jobDto.Dependencies,
                cancellationToken
            );
            if (!dependencyValidation.Success)
            {
                return ApiResponseDto<bool>.ErrorResponse(dependencyValidation.Message);
            }

            // 自动生成触发器信息
            var triggerName = string.IsNullOrWhiteSpace(jobDto.TriggerName)
                ? $"{jobDto.JobName}_Trigger"
                : jobDto.TriggerName;
            var triggerGroup = string.IsNullOrWhiteSpace(jobDto.TriggerGroup)
                ? jobDto.JobGroup
                : jobDto.TriggerGroup;

            // 创建作业信息
            var jobInfo = new QuartzJobInfo
            {
                JobName = jobDto.JobName,
                JobGroup = jobDto.JobGroup,
                TriggerName = triggerName,
                TriggerGroup = triggerGroup,
                CronExpression = jobDto.CronExpression,
                Description = jobDto.Description,
                JobType = jobDto.JobType,
                JobClassOrApi = jobDto.JobClassOrApi,
                JobData = jobDto.JobData,
                ApiMethod = jobDto.ApiMethod,
                ApiHeaders = jobDto.ApiHeaders,
                ApiBody = jobDto.ApiBody,
                ApiTimeout = jobDto.ApiTimeout,
                RetryCount = jobDto.RetryCount,
                RetryIntervalSeconds = jobDto.RetryIntervalSeconds,
                SkipSslValidation = jobDto.SkipSslValidation,
                DisallowConcurrentExecution = jobDto.DisallowConcurrentExecution,
                RerunOnRecovery = jobDto.RerunOnRecovery,
                Dependencies = JobDependencyHelper.Serialize(jobDto.Dependencies),
                StartTime = jobDto.StartTime,
                EndTime = jobDto.EndTime,
                IsEnabled = jobDto.IsEnabled,
                Status = JobStatus.Normal,
                CreateTime = DateTime.Now,
            };

            // 保存到存储
            var saveResult = await _jobStorage.AddJobAsync(jobInfo, cancellationToken);
            if (!saveResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("保存作业信息失败");
            }

            // 调度器已停止：只保存配置，启动调度器后会由重新加载流程接管
            if (!IsSchedulerAvailable)
            {
                _logger.LogWarn("AddJob", $"调度器未运行，作业仅保存未调度: {jobInfo.GetJobKey()}");
                return ApiResponseDto<bool>.SuccessResponse(
                    true,
                    "作业已保存，调度器未运行，启动调度器后将自动加载"
                );
            }

            // 如果启用，则立即调度作业；调度失败必须回滚存储，
            // 否则会留下「已保存但永远不会触发」的脏数据，用户重试还会提示作业已存在
            if (jobDto.IsEnabled)
            {
                try
                {
                    await ScheduleJobAsync(jobInfo, cancellationToken);
                }
                catch (Exception scheduleEx)
                {
                    await _jobStorage.DeleteJobAsync(
                        jobInfo.JobName,
                        jobInfo.JobGroup,
                        CancellationToken.None
                    );
                    _logger.LogFailure("AddJob", scheduleEx);
                    return ApiResponseDto<bool>.ErrorResponse(
                        $"添加作业失败，作业未能加入调度器: {scheduleEx.Message}"
                    );
                }
            }

            _logger.LogSuccess("AddJob", jobInfo.GetJobKey());
            return ApiResponseDto<bool>.SuccessResponse(true, "作业添加成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("AddJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"添加作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新现有作业
    /// </summary>
    /// <param name="jobDto">作业数据传输对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> UpdateJobAsync(
        QuartzJobDto jobDto,
        CancellationToken cancellationToken = default
    )
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
            var existingJob = await _jobStorage.GetJobAsync(
                jobDto.JobName,
                jobDto.JobGroup,
                cancellationToken
            );
            if (existingJob == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在");
            }

            // 验证作业类型
            if (!ValidateJobType(jobDto.JobClassOrApi, jobDto.JobType))
            {
                return ApiResponseDto<bool>.ErrorResponse("无效的作业类型");
            }

            // 验证前置依赖（存在性、自依赖、循环依赖）
            var dependencyValidation = await ValidateDependenciesAsync(
                jobDto.JobName,
                jobDto.JobGroup,
                jobDto.Dependencies,
                cancellationToken
            );
            if (!dependencyValidation.Success)
            {
                return ApiResponseDto<bool>.ErrorResponse(dependencyValidation.Message);
            }

            // 保存原作业状态
            var originalStatus = existingJob.Status;

            // 调度器已停止：只更新存储，启动调度器后会由重新加载流程接管
            if (!IsSchedulerAvailable)
            {
                ApplyJobDto(existingJob, jobDto);
                if (!jobDto.IsEnabled)
                {
                    // 禁用后不再有下次执行时间，避免列表显示无效的「下次执行时间」
                    existingJob.NextRunTime = null;
                }

                if (!await _jobStorage.UpdateJobAsync(existingJob, cancellationToken))
                {
                    return ApiResponseDto<bool>.ErrorResponse("更新作业信息失败");
                }

                _logger.LogWarn(
                    "UpdateJob",
                    $"调度器未运行，作业配置已保存: {existingJob.GetJobKey()}"
                );
                return ApiResponseDto<bool>.SuccessResponse(
                    true,
                    "作业已更新，调度器未运行，启动调度器后将自动生效"
                );
            }

            // 删除现有的作业调度
            var jobKey = new JobKey(jobDto.JobName, jobDto.JobGroup);
            var triggerKey = new TriggerKey(existingJob.TriggerName, existingJob.TriggerGroup);

            await Scheduler.PauseJob(jobKey, cancellationToken);
            await Scheduler.UnscheduleJob(triggerKey, cancellationToken);
            await Scheduler.DeleteJob(jobKey, cancellationToken);

            ApplyJobDto(existingJob, jobDto);

            try
            {
                // 如果启用，则重新调度作业（ScheduleJobAsync 内部会持久化最新配置）
                if (jobDto.IsEnabled)
                {
                    await ScheduleJobAsync(existingJob, cancellationToken);

                    // 如果原作业是暂停状态，重新调度后保持暂停状态
                    if (originalStatus == JobStatus.Paused)
                    {
                        await Scheduler.PauseJob(jobKey, cancellationToken);
                        existingJob.Status = JobStatus.Paused;
                        existingJob.NextRunTime = null;
                        await _jobStorage.UpdateJobAsync(existingJob, cancellationToken);
                    }
                }
                else
                {
                    // 禁用后不再有下次执行时间，保持与 BatchSetEnabled 一致
                    existingJob.NextRunTime = null;
                    if (!await _jobStorage.UpdateJobAsync(existingJob, cancellationToken))
                    {
                        return ApiResponseDto<bool>.ErrorResponse("更新作业信息失败");
                    }
                }
            }
            catch (Exception scheduleEx)
            {
                // 重新调度失败：配置仍然落库，但置为禁用，
                // 避免出现「存储中是启用状态、调度器里却没有触发器」的幽灵作业
                existingJob.IsEnabled = false;
                existingJob.NextRunTime = null;
                existingJob.UpdateTime = DateTime.Now;
                await _jobStorage.UpdateJobAsync(existingJob, CancellationToken.None);

                _logger.LogFailure("UpdateJob", scheduleEx);
                return ApiResponseDto<bool>.ErrorResponse(
                    $"作业配置已保存，但重新调度失败，已自动置为禁用: {scheduleEx.Message}"
                );
            }

            _logger.LogSuccess("UpdateJob", existingJob.GetJobKey());
            return ApiResponseDto<bool>.SuccessResponse(true, "作业更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("UpdateJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"更新作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 把 DTO 中的可编辑字段写入作业实体（不落库，由调用方决定持久化时机）
    /// </summary>
    /// <param name="job">作业实体</param>
    /// <param name="jobDto">作业数据传输对象</param>
    private static void ApplyJobDto(QuartzJobInfo job, QuartzJobDto jobDto)
    {
        // 保留现有的触发器信息（如果 DTO 中未提供）
        job.TriggerName = string.IsNullOrWhiteSpace(jobDto.TriggerName)
            ? job.TriggerName
            : jobDto.TriggerName;
        job.TriggerGroup = string.IsNullOrWhiteSpace(jobDto.TriggerGroup)
            ? job.TriggerGroup
            : jobDto.TriggerGroup;
        job.CronExpression = jobDto.CronExpression;
        job.Description = jobDto.Description;
        job.JobType = jobDto.JobType;
        job.JobClassOrApi = jobDto.JobClassOrApi;
        job.JobData = jobDto.JobData;
        job.ApiMethod = jobDto.ApiMethod;
        job.ApiHeaders = jobDto.ApiHeaders;
        job.ApiBody = jobDto.ApiBody;
        job.ApiTimeout = jobDto.ApiTimeout;
        job.RetryCount = jobDto.RetryCount;
        job.RetryIntervalSeconds = jobDto.RetryIntervalSeconds;
        job.SkipSslValidation = jobDto.SkipSslValidation;
        job.DisallowConcurrentExecution = jobDto.DisallowConcurrentExecution;
        job.RerunOnRecovery = jobDto.RerunOnRecovery;
        job.Dependencies = JobDependencyHelper.Serialize(jobDto.Dependencies);
        job.StartTime = jobDto.StartTime;
        job.EndTime = jobDto.EndTime;
        job.IsEnabled = jobDto.IsEnabled;
        job.UpdateTime = DateTime.Now;
    }

    /// <summary>
    /// 请求中断作业正在执行的实例（仅对实现 IInterruptableJob 的作业生效）。
    /// 删除/禁用作业时必须调用：Quartz 的 DeleteJob 只移除作业定义，
    /// 已在执行的实例会继续跑完，导致「执行中」计数和线程占用迟迟不释放。
    /// </summary>
    private async Task TryInterruptJobAsync(JobKey jobKey, CancellationToken cancellationToken)
    {
        try
        {
            if (!await Scheduler.CheckExists(jobKey, cancellationToken))
            {
                return;
            }

            if (await Scheduler.Interrupt(jobKey, cancellationToken))
            {
                _logger.LogInfo("中断作业", $"已请求中断执行中的作业: {jobKey.Group}.{jobKey.Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarn(
                "中断作业",
                $"请求中断作业 {jobKey.Group}.{jobKey.Name} 失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 删除指定作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> DeleteJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);

            // 删除调度器中的作业。
            // 调度器已停止时 Quartz 会抛异常，必须跳过，否则后面的存储删除永远不会执行，
            // 作业会变成「删不掉」的状态；单个作业删除失败也不应阻断存储清理
            if (IsSchedulerAvailable)
            {
                // 先中断正在执行的实例：DeleteJob 只会移除作业定义，已在跑的实例不会停止，
                // 否则删除后「执行中」计数会一直停在删除前的数量，线程池也被长期占满
                await TryInterruptJobAsync(jobKey, cancellationToken);

                try
                {
                    if (!await Scheduler.DeleteJob(jobKey, cancellationToken))
                    {
                        _logger.LogWarn("DeleteJob", $"从调度器删除作业失败: {jobGroup}.{jobName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarn(
                        "DeleteJob",
                        $"从调度器删除作业异常: {jobGroup}.{jobName}, {ex.Message}"
                    );
                }
            }

            // 删除存储中的作业信息
            var storageResult = await _jobStorage.DeleteJobAsync(
                jobName,
                jobGroup,
                cancellationToken
            );

            // 删除该作业的日志
            await _jobStorage.ClearJobLogsAsync(
                new QuartzJobLogQueryDto { JobName = jobName, JobGroup = jobGroup },
                cancellationToken
            );

            if (!storageResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在或删除失败");
            }

            _logger.LogSuccess("DeleteJob", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业删除成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("DeleteJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"删除作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量删除指定作业
    /// </summary>
    /// <param name="jobs">作业名称和分组的列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> BatchDeleteJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            int successCount = 0;
            int failureCount = 0;

            foreach (var (jobName, jobGroup) in jobs)
            {
                try
                {
                    // 复用单条删除逻辑：调度器已停止时会自动跳过调度器侧操作，只清理存储
                    var result = await DeleteJobAsync(jobName, jobGroup, cancellationToken);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarn(
                            "BatchDeleteJobs",
                            $"删除作业失败: {jobGroup}.{jobName}, 原因: {result.Message}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogFailure("BatchDeleteJobs", ex);
                }
            }

            string message =
                $"批量删除作业完成，成功删除 {successCount} 个作业，失败 {failureCount} 个作业";
            return ApiResponseDto<bool>.SuccessResponse(true, message);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("BatchDeleteJobs", ex);
            return ApiResponseDto<bool>.ErrorResponse($"批量删除作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 暂停指定作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> PauseJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobKey = new JobKey(jobName, jobGroup);
            var schedulerAvailable = IsSchedulerAvailable;

            // 调度器已停止时跳过 Quartz 侧暂停：Quartz 关闭后调用会抛异常，
            // 导致连存储状态都更新不了
            if (schedulerAvailable)
            {
                await Scheduler.PauseJob(jobKey, cancellationToken);
            }

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

            _logger.LogSuccess("PauseJob", $"{jobGroup}.{jobName}");
            return schedulerAvailable
                ? ApiResponseDto<bool>.SuccessResponse(true, "作业暂停成功")
                : ApiResponseDto<bool>.SuccessResponse(
                    true,
                    "作业已暂停，调度器未运行，启动调度器后将保持暂停"
                );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PauseJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"暂停作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 恢复指定作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> ResumeJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
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

            // 调度器已停止：只恢复存储中的状态，启动调度器后会由重新加载流程接管
            if (!IsSchedulerAvailable)
            {
                jobInfo.Status = JobStatus.Normal;
                jobInfo.UpdateTime = DateTime.Now;
                await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);

                _logger.LogWarn(
                    "ResumeJob",
                    $"调度器未运行，作业状态已恢复: {jobGroup}.{jobName}"
                );
                return ApiResponseDto<bool>.SuccessResponse(
                    true,
                    "作业状态已恢复，调度器未运行，启动调度器后将自动加载"
                );
            }

            // 检查作业是否在调度器中存在
            var jobExists = await Scheduler.CheckExists(jobKey, cancellationToken);

            if (jobExists)
            {
                // 如果作业存在，先尝试恢复
                await Scheduler.ResumeJob(jobKey, cancellationToken);

                // 暂停的作业在调度器重建后可能没有任何触发器，需要重新调度
                var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                if (!triggers.Any())
                {
                    _logger.LogInfo(
                        "作业 {JobKey} 恢复后没有触发器，重新调度作业",
                        $"{jobGroup}.{jobName}"
                    );
                    await ScheduleJobAsync(jobInfo, cancellationToken);
                }
            }
            else
            {
                // 如果作业不存在，重新调度作业
                _logger.LogInfo(
                    "作业 {JobKey} 在调度器中不存在，重新调度作业",
                    $"{jobGroup}.{jobName}"
                );
                await ScheduleJobAsync(jobInfo, cancellationToken);
            }

            // 更新作业状态
            jobInfo.Status = JobStatus.Normal;
            jobInfo.UpdateTime = DateTime.Now;

            // 用调度器中触发器的实时时间刷新下次/上次执行时间
            await FillExecutionTimesFromSchedulerAsync(jobInfo, jobKey, cancellationToken);

            await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);

            _logger.LogSuccess("ResumeJob", $"{jobGroup}.{jobName}");
            return ApiResponseDto<bool>.SuccessResponse(true, "作业恢复成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ResumeJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"恢复作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 用调度器中触发器的时间刷新作业的下次/上次执行时间（仅在字段为空时填充）
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="jobKey">作业键</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task FillExecutionTimesFromSchedulerAsync(
        QuartzJobInfo jobInfo,
        JobKey jobKey,
        CancellationToken cancellationToken
    )
    {
        var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        if (!triggers.Any())
        {
            return;
        }

        if (jobInfo.NextRunTime == null)
        {
            var nextFireTime = triggers
                .Select(t => t.GetNextFireTimeUtc())
                .Where(t => t.HasValue)
                .Select(t => t.GetValueOrDefault())
                .OrderBy(t => t)
                .FirstOrDefault();

            if (nextFireTime != default)
            {
                jobInfo.NextRunTime = nextFireTime.LocalDateTime;
            }
        }

        if (jobInfo.PreviousRunTime == null)
        {
            var previousFireTime = triggers
                .Select(t => t.GetPreviousFireTimeUtc())
                .Where(t => t.HasValue)
                .Select(t => t.GetValueOrDefault())
                .OrderByDescending(t => t)
                .FirstOrDefault();

            if (previousFireTime != default)
            {
                jobInfo.PreviousRunTime = previousFireTime.LocalDateTime;
            }
        }
    }

    /// <summary>
    /// 批量暂停作业
    /// </summary>
    /// <param name="jobs">作业列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> BatchPauseJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    )
    {
        int successCount = 0;
        int failureCount = 0;

        foreach (var (jobName, jobGroup) in jobs)
        {
            try
            {
                var result = await PauseJobAsync(jobName, jobGroup, cancellationToken);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogFailure("BatchPauseJobs", ex);
            }
        }

        return ApiResponseDto<bool>.SuccessResponse(
            true,
            $"批量暂停完成，成功 {successCount} 个，失败 {failureCount} 个"
        );
    }

    /// <summary>
    /// 批量恢复作业
    /// </summary>
    /// <param name="jobs">作业列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> BatchResumeJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    )
    {
        int successCount = 0;
        int failureCount = 0;

        foreach (var (jobName, jobGroup) in jobs)
        {
            try
            {
                var result = await ResumeJobAsync(jobName, jobGroup, cancellationToken);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogFailure("BatchResumeJobs", ex);
            }
        }

        return ApiResponseDto<bool>.SuccessResponse(
            true,
            $"批量恢复完成，成功 {successCount} 个，失败 {failureCount} 个"
        );
    }

    /// <summary>
    /// 批量启用/禁用作业
    /// </summary>
    /// <param name="jobs">作业列表</param>
    /// <param name="enable">true=启用，false=禁用</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> BatchSetEnabledAsync(
        List<(string JobName, string JobGroup)> jobs,
        bool enable,
        CancellationToken cancellationToken = default
    )
    {
        int successCount = 0;
        int failureCount = 0;

        foreach (var (jobName, jobGroup) in jobs)
        {
            try
            {
                var jobInfo = await _jobStorage.GetJobAsync(
                    jobName,
                    jobGroup,
                    cancellationToken
                );
                if (jobInfo == null)
                {
                    failureCount++;
                    continue;
                }

                var jobKey = new JobKey(jobName, jobGroup);
                var schedulerAvailable = IsSchedulerAvailable;

                if (!schedulerAvailable)
                {
                    // 调度器未运行：只更新存储中的启用状态，启动后由重新加载流程接管
                    jobInfo.IsEnabled = enable;
                    if (!enable)
                    {
                        jobInfo.NextRunTime = null;
                    }
                }
                else if (enable)
                {
                    jobInfo.IsEnabled = true;
                    if (jobInfo.Status == JobStatus.Paused)
                    {
                        // 启用时保持原有的暂停状态：先调度再暂停
                        await ScheduleJobAsync(jobInfo, cancellationToken);
                        await Scheduler.PauseJob(jobKey, cancellationToken);
                        jobInfo.Status = JobStatus.Paused;
                        jobInfo.NextRunTime = null;
                    }
                    else
                    {
                        await ScheduleJobAsync(jobInfo, cancellationToken);
                    }
                }
                else
                {
                    jobInfo.IsEnabled = false;
                    jobInfo.NextRunTime = null;
                    if (await Scheduler.CheckExists(jobKey, cancellationToken))
                    {
                        // 禁用即停止：先中断执行中的实例，避免线程被占用到本次执行结束
                        await TryInterruptJobAsync(jobKey, cancellationToken);
                        await Scheduler.DeleteJob(jobKey, cancellationToken);
                    }
                }

                jobInfo.UpdateTime = DateTime.Now;
                await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogFailure("BatchSetEnabled", ex);
            }
        }

        var action = enable ? "启用" : "禁用";
        return ApiResponseDto<bool>.SuccessResponse(
            true,
            $"批量{action}完成，成功 {successCount} 个，失败 {failureCount} 个"
        );
    }

    /// <summary>
    /// 手动触发指定作业执行
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public Task<ApiResponseDto<bool>> TriggerJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        return TriggerJobWithContextAsync(
            jobName,
            jobGroup,
            TriggerType.Manual,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// 带执行上下文触发作业（手动/补偿共用）。
    /// 执行级标记只写入触发器，不写入 JobDetail，避免被后续 Cron 触发复用。
    /// </summary>
    public async Task<ApiResponseDto<bool>> TriggerJobWithContextAsync(
        string jobName,
        string jobGroup,
        TriggerType triggerType,
        Guid? logId = null,
        Guid? recoverySourceLogId = null,
        int recoveryAttempt = 1,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default
    )
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

            // 调度器已停止时 TriggerJob 会抛 SchedulerException，这里提前给出明确提示
            if (!IsSchedulerAvailable)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器已停止，无法触发作业，请先启动调度器",
                    "SCHEDULER_NOT_RUNNING"
                );
            }

            // 持久化数据：仅作业自身配置，允许写入 JobDetail
            var persistentMap = CreatePersistentJobDataMap(jobInfo);
            // 执行级数据：每次触发独立，绝不能写入 JobDetail
            var executionMap = new JobDataMap();
            foreach (var pair in persistentMap)
            {
                executionMap[pair.Key] = pair.Value;
            }
            executionMap[QuartzJobConst.TriggerType] = triggerType.ToString();
            if (triggerType == TriggerType.Manual)
            {
                executionMap[QuartzJobConst.IsManualTrigger] = true;
            }
            if (logId.HasValue)
            {
                executionMap[QuartzJobConst.LogId] = logId.Value.ToString();
            }
            if (recoverySourceLogId.HasValue)
            {
                executionMap[QuartzJobConst.RecoverySourceLogId] =
                    recoverySourceLogId.Value.ToString();
            }
            if (recoveryAttempt > 1)
            {
                executionMap[QuartzJobConst.RecoveryAttempt] = recoveryAttempt;
            }
            if (!string.IsNullOrWhiteSpace(triggeredBy))
            {
                executionMap[QuartzJobConst.TriggeredBy] = triggeredBy;
            }

            // 检查作业是否在调度器中存在
            var jobExists = await Scheduler.CheckExists(jobKey, cancellationToken);

            // 如果作业不存在，先添加到调度器
            if (!jobExists)
            {
                _logger.LogInfo(
                    "作业不在调度器中，先添加作业到调度器: {JobKey}",
                    $"{jobGroup}.{jobName}"
                );

                var jobBuilder = JobBuilder
                    .Create()
                    .WithIdentity(jobKey)
                    .WithDescription(jobInfo.Description ?? string.Empty)
                    .StoreDurably(true); // 没有触发器的作业必须设置为持久化

                // 禁止并发执行：与 ScheduleJobAsync 保持一致
                if (jobInfo.DisallowConcurrentExecution)
                {
                    jobBuilder.DisallowConcurrentExecution();
                }

                // 根据作业类型设置作业类（配置了重试的注册为重试包装器）
                Type? realJobType = jobInfo.JobType == JobTypeEnum.API ? typeof(ApiJob) : null;
                if (realJobType == null)
                {
                    realJobType = GetJobType(jobInfo.JobClassOrApi);
                    if (realJobType == null || !typeof(IJob).IsAssignableFrom(realJobType))
                    {
                        return ApiResponseDto<bool>.ErrorResponse(
                            $"无效的作业类型: {jobInfo.JobClassOrApi}"
                        );
                    }
                }

                if (jobInfo.RetryCount > 0)
                {
                    jobBuilder.OfType<RetryJobWrapper>();
                    // Quartz 3.22 起 Put(...) 已过时，统一改用索引器
                    persistentMap[QuartzJobConst.RealJobType] = realJobType.AssemblyQualifiedName!;
                    persistentMap[QuartzJobConst.RetryCount] = jobInfo.RetryCount;
                    var interval = jobInfo.RetryIntervalSeconds > 0
                        ? jobInfo.RetryIntervalSeconds
                        : 30;
                    persistentMap[QuartzJobConst.RetryIntervalSeconds] = interval;

                    executionMap[QuartzJobConst.RealJobType] = realJobType.AssemblyQualifiedName!;
                    executionMap[QuartzJobConst.RetryCount] = jobInfo.RetryCount;
                    executionMap[QuartzJobConst.RetryIntervalSeconds] = interval;
                }
                else
                {
                    jobBuilder.OfType(realJobType);
                }

                // 只把持久化数据写入作业详情
                jobBuilder.UsingJobData(persistentMap);
                var jobDetail = jobBuilder.Build();
                await Scheduler.AddJob(jobDetail, true, cancellationToken);
            }

            await Scheduler.TriggerJob(jobKey, executionMap, cancellationToken);

            _logger.LogSuccess(
                "TriggerJob",
                $"触发作业成功: {jobGroup}.{jobName}, 来源: {triggerType}"
            );
            return ApiResponseDto<bool>.SuccessResponse(true, "作业触发成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("TriggerJob", ex);
            return ApiResponseDto<bool>.ErrorResponse($"触发作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建持久化作业数据映射（只包含作业自身配置）
    /// </summary>
    private JobDataMap CreatePersistentJobDataMap(QuartzJobInfo jobInfo)
    {
        var jobDataMap = new JobDataMap();

        if (!string.IsNullOrEmpty(jobInfo.JobData))
        {
            jobDataMap.Add(QuartzJobConst.JobData, jobInfo.JobData);
        }

        return jobDataMap;
    }

    /// <summary>
    /// 获取作业类型
    /// </summary>
    /// <param name="jobClassOrApi">作业类名或API地址</param>
    /// <returns>作业类型</returns>
    private Type GetJobType(string jobClassOrApi)
    {
        // 先尝试直接获取类型
        var jobType = Type.GetType(jobClassOrApi);

        if (jobType == null)
        {
            // 如果没找到，搜索所有已加载的程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                jobType = assembly.GetType(jobClassOrApi);
                if (jobType != null)
                {
                    break;
                }
            }
        }

        return jobType;
    }

    /// <summary>
    /// 获取作业列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页作业列表</returns>
    public async Task<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>> GetJobsAsync(
        QuartzJobQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.GetJobsAsync(queryDto, cancellationToken);

            var responseItems = new List<QuartzJobResponseDto>();

            foreach (var jobInfo in result.Items)
            {
                var jobResponse = MapToResponseDto(jobInfo);

                // 调度器已停止时无法查询实时触发器，直接返回存储中的时间，避免逐个作业记警告
                if (!IsSchedulerAvailable)
                {
                    responseItems.Add(jobResponse);
                    continue;
                }

                // 从调度器获取实时的触发信息
                try
                {
                    var jobKey = new JobKey(jobInfo.JobName, jobInfo.JobGroup);
                    var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                    _logger.LogInfo(
                        "获取作业列表",
                        "作业: {JobKey} 找到 {TriggerCount} 个触发器",
                        $"{jobInfo.JobGroup}.{jobInfo.JobName}",
                        triggers.Count
                    );

                    if (triggers.Any())
                    {
                        // 找到最早的下一次执行时间
                        var nextFireTimes = triggers
                            .Select(t => t.GetNextFireTimeUtc())
                            .Where(t => t.HasValue)
                            .Select(t => t.Value)
                            .OrderBy(t => t)
                            .ToList();

                        if (nextFireTimes.Any())
                        {
                            jobResponse.NextRunTime =
                                jobResponse.NextRunTime == null
                                    ? nextFireTimes.First().LocalDateTime
                                    : jobResponse.NextRunTime;
                            _logger.LogInfo(
                                "获取作业列表",
                                "作业: {JobKey} 的下次执行时间: {NextRunTime}",
                                $"{jobInfo.JobGroup}.{jobInfo.JobName}",
                                jobResponse.NextRunTime
                            );
                        }
                        else
                        {
                            _logger.LogInfo(
                                "获取作业列表",
                                "作业: {JobKey} 的所有触发器都没有下一次执行时间",
                                $"{jobInfo.JobGroup}.{jobInfo.JobName}"
                            );
                        }

                        // 找到最近的上一次执行时间
                        var previousFireTimes = triggers
                            .Select(t => t.GetPreviousFireTimeUtc())
                            .Where(t => t.HasValue)
                            .Select(t => t.Value)
                            .OrderByDescending(t => t)
                            .ToList();

                        if (previousFireTimes.Any())
                        {
                            // 将UTC时间转换为北京时间（UTC+8）
                            jobResponse.PreviousRunTime =
                                jobResponse.PreviousRunTime == null
                                    ? previousFireTimes.First().LocalDateTime
                                    : jobResponse.PreviousRunTime;
                        }
                    }
                    else
                    {
                        _logger.LogInfo(
                            "获取作业列表",
                            "作业: {JobKey} 没有找到触发器",
                            $"{jobInfo.JobGroup}.{jobInfo.JobName}"
                        );
                    }
                }
                catch (Exception)
                {
                    _logger.LogWarn(
                        "获取作业触发器",
                        $"获取作业 {jobInfo.JobGroup}.{jobInfo.JobName} 的触发信息失败"
                    );
                }

                responseItems.Add(jobResponse);
            }

            var response = new PagedResponseDto<QuartzJobResponseDto>
            {
                Items = responseItems,
                TotalCount = result.TotalCount,
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
            };

            return ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetJobs", ex);
            return ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.ErrorResponse(
                $"获取作业列表失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 获取作业详情
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业详情</returns>
    public async Task<ApiResponseDto<QuartzJobResponseDto>> GetJobDetailAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo == null)
            {
                return ApiResponseDto<QuartzJobResponseDto>.ErrorResponse("作业不存在");
            }

            var response = MapToResponseDto(jobInfo);

            // 调度器中的实时触发器信息仅用于补全时间字段：
            // 调度器已停止或查询失败时降级为只使用存储中的时间，保证详情仍可打开
            if (IsSchedulerAvailable)
            {
                try
                {
                    var jobKey = new JobKey(jobName, jobGroup);
                    var triggers = await Scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                    if (triggers.Any())
                    {
                        var trigger = triggers.First();
                        response.NextRunTime =
                            response.NextRunTime == null
                                ? trigger.GetNextFireTimeUtc()?.LocalDateTime
                                : response.NextRunTime;
                        response.PreviousRunTime =
                            response.PreviousRunTime == null
                                ? trigger.GetPreviousFireTimeUtc()?.LocalDateTime
                                : response.PreviousRunTime;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarn(
                        "GetJobDetail",
                        $"获取作业 {jobGroup}.{jobName} 的调度器信息失败: {ex.Message}"
                    );
                }
            }

            return ApiResponseDto<QuartzJobResponseDto>.SuccessResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetJobDetail", ex);
            return ApiResponseDto<QuartzJobResponseDto>.ErrorResponse(
                $"获取作业详情失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 清除所有作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> ClearAllJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 调度器已停止时其中的作业已随实例销毁，无需也无法再清理
            if (!IsSchedulerAvailable)
            {
                return ApiResponseDto<bool>.SuccessResponse(true, "调度器未运行，无需清理");
            }

            var jobKeys = await Scheduler.GetJobKeys(
                GroupMatcher<JobKey>.AnyGroup(),
                cancellationToken
            );

            foreach (var jobKey in jobKeys)
            {
                // 与单条删除保持一致：先中断执行中的实例，再移除作业定义
                await TryInterruptJobAsync(jobKey, cancellationToken);
                await Scheduler.DeleteJob(jobKey, cancellationToken);
            }

            _logger.LogSuccess("ClearAllJobs");
            return ApiResponseDto<bool>.SuccessResponse(true, "清除所有作业成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ClearAllJobs", ex);
            return ApiResponseDto<bool>.ErrorResponse($"清除所有作业失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新作业执行时间信息
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="trigger">触发器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> UpdateJobExecutionTimesAsync(
        string jobName,
        string jobGroup,
        Quartz.ITrigger trigger,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 从存储中获取作业信息
            var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, cancellationToken);
            if (jobInfo == null)
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不存在");
            }

            var nextRunTime = trigger.GetNextFireTimeUtc()?.LocalDateTime;
            var previousRunTime = trigger.GetPreviousFireTimeUtc()?.LocalDateTime;

            // 手动/补偿触发使用的是 Quartz 临时触发器，其 NextFireTimeUtc 恒为 null，
            // 直接写入会把 Cron 触发器的下次执行时间清空，
            // 因此回落到作业自身的持久化触发器；无法判断时保留原值
            if (nextRunTime == null)
            {
                var (resolved, nextFromTriggers) = await TryGetNextFireTimeFromTriggersAsync(
                    jobName,
                    jobGroup,
                    cancellationToken
                );
                nextRunTime = resolved ? nextFromTriggers : jobInfo.NextRunTime;
            }

            // 更新作业执行时间
            jobInfo.NextRunTime = nextRunTime;
            if (previousRunTime.HasValue)
            {
                jobInfo.PreviousRunTime = previousRunTime;
            }
            jobInfo.UpdateTime = DateTime.Now;

            // 保存到存储
            var updateResult = await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
            if (!updateResult)
            {
                return ApiResponseDto<bool>.ErrorResponse("更新作业执行时间失败");
            }

            _logger.LogInfo("更新作业执行时间信息", new JobKey(jobName, jobGroup).ToString());
            return ApiResponseDto<bool>.SuccessResponse(true, "作业执行时间更新成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("更新作业执行时间信息", ex);
            return ApiResponseDto<bool>.ErrorResponse($"更新作业执行时间失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从作业的持久化触发器中取最近的下次执行时间
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Resolved=false 表示调度器不可用、无法判断，调用方应保留原值</returns>
    private async Task<(bool Resolved, DateTime? NextFireTime)> TryGetNextFireTimeFromTriggersAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken
    )
    {
        if (!IsSchedulerAvailable)
        {
            return (false, null);
        }

        try
        {
            var triggers = await Scheduler.GetTriggersOfJob(
                new JobKey(jobName, jobGroup),
                cancellationToken
            );

            var nextFireTime = triggers
                .Select(t => t.GetNextFireTimeUtc())
                .Where(t => t.HasValue)
                .Select(t => t.GetValueOrDefault())
                .OrderBy(t => t)
                .FirstOrDefault();

            return (true, nextFireTime == default ? null : nextFireTime.LocalDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarn(
                "获取下次执行时间",
                $"获取作业 {jobGroup}.{jobName} 的下次执行时间失败: {ex.Message}"
            );
            return (false, null);
        }
    }

    /// <summary>
    /// 获取ClassJobs列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业类列表</returns>
    public async Task<ApiResponseDto<List<string>>> GetJobClassesAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInfoStructured("获取ClassJobs列表");
            var jobClasses = _jobClassScanner.ScanJobClasses();
            _logger.LogSuccess("获取ClassJobs列表", $"共计 {jobClasses.Count} 个");
            return ApiResponseDto<List<string>>.SuccessResponse(
                jobClasses,
                "获取ClassJobs列表成功"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取ClassJobs列表", ex);
            return ApiResponseDto<List<string>>.ErrorResponse(
                $"获取ClassJobs列表失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 验证作业类型是否有效
    /// </summary>
    /// <param name="jobType">作业类型字符串</param>
    /// <param name="jobTypeEnum">作业类型枚举</param>
    /// <returns>验证结果</returns>
    private bool ValidateJobType(string jobType, JobTypeEnum jobTypeEnum)
    {
        try
        {
            // 如果是API作业，验证URL格式
            if (jobTypeEnum == JobTypeEnum.API)
            {
                return Uri.TryCreate(jobType, UriKind.Absolute, out var uriResult)
                    && (
                        uriResult.Scheme == Uri.UriSchemeHttp
                        || uriResult.Scheme == Uri.UriSchemeHttps
                    );
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

    /// <summary>
    /// 调度作业到Quartz调度器
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ScheduleJobAsync(
        QuartzJobInfo jobInfo,
        CancellationToken cancellationToken = default
    )
    {
        // 确定作业类型
        Type jobType;

        if (jobInfo.JobType == JobTypeEnum.API)
        {
            // API作业使用ApiJob类
            jobType = typeof(ApiJob);
        }
        else
        {
            // DLL作业使用指定的类型
            jobType = Type.GetType(jobInfo.JobClassOrApi);

            // 如果没找到，搜索所有已加载的程序集
            if (jobType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    jobType = assembly.GetType(jobInfo.JobClassOrApi);
                    if (jobType != null)
                    {
                        break;
                    }
                }
            }

            if (jobType == null)
            {
                throw new ArgumentException($"无法找到作业类型: {jobInfo.JobClassOrApi}");
            }
        }

        // 配置了重试的作业注册为重试包装器，真实类型通过 JobDataMap 传递；
        // 未配置重试（RetryCount=0，含旧版本数据）仍直接注册真实类型，行为与旧版一致
        var useRetryWrapper = jobInfo.RetryCount > 0;
        var registerType = useRetryWrapper ? typeof(RetryJobWrapper) : jobType;

        // 创建作业
        var jobBuilder = JobBuilder
            .Create(registerType)
            .WithIdentity(jobInfo.JobName, jobInfo.JobGroup)
            .WithDescription(jobInfo.Description ?? string.Empty)
            .StoreDurably();

        // 禁止并发执行：同一作业上一次执行未完成时，新的触发会等待
        if (jobInfo.DisallowConcurrentExecution)
        {
            jobBuilder.DisallowConcurrentExecution();
        }

        // 设置作业数据
        if (!string.IsNullOrEmpty(jobInfo.JobData))
        {
            jobBuilder.UsingJobData(QuartzJobConst.JobData, jobInfo.JobData);
        }

        // 设置重试配置
        if (useRetryWrapper)
        {
            jobBuilder.UsingJobData(QuartzJobConst.RealJobType, jobType.AssemblyQualifiedName!);
            jobBuilder.UsingJobData(QuartzJobConst.RetryCount, jobInfo.RetryCount);
            jobBuilder.UsingJobData(
                QuartzJobConst.RetryIntervalSeconds,
                jobInfo.RetryIntervalSeconds > 0 ? jobInfo.RetryIntervalSeconds : 30
            );
        }

        var jobDetail = jobBuilder.Build();

        // 创建触发器
        var triggerBuilder = TriggerBuilder
            .Create()
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
        await Scheduler.ScheduleJob(
            jobDetail,
            new List<ITrigger> { trigger },
            true,
            cancellationToken
        );

        // 获取作业的下次执行时间并更新到存储
        var nextRunTime = trigger.GetNextFireTimeUtc()?.LocalDateTime;
        var previousRunTime = trigger.GetPreviousFireTimeUtc()?.LocalDateTime;

        // 更新作业信息
        jobInfo.NextRunTime = nextRunTime;
        jobInfo.PreviousRunTime = previousRunTime;
        await _jobStorage.UpdateJobAsync(jobInfo, cancellationToken);
    }

    /// <summary>
    /// 验证作业前置依赖：依赖作业必须存在，不能依赖自身，不能形成循环依赖
    /// </summary>
    /// <param name="jobName">当前作业名称</param>
    /// <param name="jobGroup">当前作业分组</param>
    /// <param name="dependencies">依赖作业列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    private async Task<ApiResponseDto<bool>> ValidateDependenciesAsync(
        string jobName,
        string jobGroup,
        List<JobDependencyDto>? dependencies,
        CancellationToken cancellationToken = default
    )
    {
        if (dependencies == null || dependencies.Count == 0)
        {
            return ApiResponseDto<bool>.SuccessResponse(true);
        }

        var selfKey = $"{jobGroup}.{jobName}";
        var allJobs = await _jobStorage.GetAllJobsAsync(cancellationToken);
        var jobKeys = allJobs.Select(j => j.GetJobKey()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 构建依赖图（已有作业 + 本次提交的依赖），用于循环依赖检测
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in allJobs)
        {
            graph[job.GetJobKey()] = JobDependencyHelper
                .Deserialize(job.Dependencies)
                .Select(JobDependencyHelper.GetJobKey)
                .ToList();
        }

        var newDependencies = dependencies.Select(JobDependencyHelper.GetJobKey).ToList();
        foreach (var dependencyKey in newDependencies)
        {
            if (string.Equals(dependencyKey, selfKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<bool>.ErrorResponse("作业不能依赖自身");
            }

            if (!jobKeys.Contains(dependencyKey))
            {
                return ApiResponseDto<bool>.ErrorResponse($"依赖作业不存在: {dependencyKey}");
            }
        }

        graph[selfKey] = newDependencies;

        if (HasDependencyCycle(selfKey, graph))
        {
            return ApiResponseDto<bool>.ErrorResponse("存在循环依赖，请检查前置依赖配置");
        }

        return ApiResponseDto<bool>.SuccessResponse(true);
    }

    /// <summary>
    /// 从起始作业出发检查依赖链是否回到自身（循环依赖）
    /// </summary>
    /// <param name="startKey">起始作业键</param>
    /// <param name="graph">依赖图</param>
    /// <returns>是否存在循环依赖</returns>
    private static bool HasDependencyCycle(
        string startKey,
        Dictionary<string, List<string>> graph
    )
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(startKey);

        while (stack.Count > 0)
        {
            var currentKey = stack.Pop();
            if (!visited.Add(currentKey))
            {
                continue;
            }

            if (!graph.TryGetValue(currentKey, out var dependencies))
            {
                continue;
            }

            foreach (var dependencyKey in dependencies)
            {
                if (string.Equals(dependencyKey, startKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                stack.Push(dependencyKey);
            }
        }

        return false;
    }

    /// <summary>
    /// 将作业信息实体映射为响应DTO
    /// </summary>
    /// <param name="jobInfo">作业信息实体</param>
    /// <returns>作业响应DTO</returns>
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
            JobType = jobInfo.JobType,
            JobClassOrApi = jobInfo.JobClassOrApi,
            JobData = jobInfo.JobData,
            ApiMethod = jobInfo.ApiMethod,
            ApiHeaders = jobInfo.ApiHeaders,
            ApiBody = jobInfo.ApiBody,
            ApiTimeout = jobInfo.ApiTimeout,
            RetryCount = jobInfo.RetryCount,
            RetryIntervalSeconds = jobInfo.RetryIntervalSeconds,
            SkipSslValidation = jobInfo.SkipSslValidation,
            DisallowConcurrentExecution = jobInfo.DisallowConcurrentExecution,
            RerunOnRecovery = jobInfo.RerunOnRecovery,
            EffectiveRerunOnRecovery =
                jobInfo.RerunOnRecovery ?? _recoveryOptions.Current.DefaultRerunOnRecovery,
            Dependencies = JobDependencyHelper.Deserialize(jobInfo.Dependencies),
            StartTime = jobInfo.StartTime,
            EndTime = jobInfo.EndTime,
            Status = jobInfo.Status,
            IsEnabled = jobInfo.IsEnabled,
            CreateTime = jobInfo.CreateTime,
            UpdateTime = jobInfo.UpdateTime,
            CreateBy = jobInfo.CreateBy,
            UpdateBy = jobInfo.UpdateBy,
            NextRunTime = jobInfo.NextRunTime,
            PreviousRunTime = jobInfo.PreviousRunTime,
        };
    }

    #endregion

    #region 作业日志

    /// <summary>
    /// 获取作业执行日志列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页作业日志列表</returns>
    public async Task<ApiResponseDto<PagedResponseDto<QuartzJobLog>>> GetJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.GetJobLogsAsync(queryDto, cancellationToken);
            return ApiResponseDto<PagedResponseDto<QuartzJobLog>>.SuccessResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业日志失败");
            return ApiResponseDto<PagedResponseDto<QuartzJobLog>>.ErrorResponse(
                $"获取作业日志失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 获取单条日志
    /// </summary>
    public async Task<ApiResponseDto<QuartzJobLog>> GetJobLogAsync(
        Guid logId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var log = await _jobStorage.GetJobLogAsync(logId, cancellationToken);
            return log == null
                ? ApiResponseDto<QuartzJobLog>.ErrorResponse("日志不存在", "LOG_NOT_FOUND")
                : ApiResponseDto<QuartzJobLog>.SuccessResponse(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取日志详情失败");
            return ApiResponseDto<QuartzJobLog>.ErrorResponse(
                $"获取日志详情失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 根据查询条件清空作业日志
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<ApiResponseDto<bool>> ClearJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.ClearJobLogsAsync(queryDto, cancellationToken);
            if (result)
            {
                _logger.LogSuccess("ClearJobLogs", "清空作业日志成功");
                return ApiResponseDto<bool>.SuccessResponse(true, "清空作业日志成功");
            }
            else
            {
                _logger.LogWarning("清空作业日志失败");
                return ApiResponseDto<bool>.ErrorResponse("清空作业日志失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空作业日志失败");
            return ApiResponseDto<bool>.ErrorResponse($"清空作业日志失败: {ex.Message}");
        }
    }

    #endregion

    #region Cron表达式
    /// <summary>
    /// 验证Cron表达式是否有效
    /// </summary>
    /// <param name="cronExpression">Cron表达式</param>
    /// <returns>验证结果</returns>
    public ApiResponseDto<bool> ValidateCronExpression(string cronExpression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                return ApiResponseDto<bool>.ErrorResponse("Cron表达式无效: 表达式不能为空");
            }

            // 构造成功即表示语法合法。
            // IsSatisfiedBy(Now) 只表示「此刻是否匹配」，不能作为有效性判断，
            // 否则像 0 0 3 * * ? 这类合法表达式会返回 false
            _ = new CronExpression(cronExpression);
            return ApiResponseDto<bool>.SuccessResponse(true, "Cron表达式有效");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.ErrorResponse($"Cron表达式无效: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取Cron表达式的下几次执行时间
    /// </summary>
    /// <param name="cronExpression">Cron表达式</param>
    /// <param name="count">获取的次数，默认5次</param>
    /// <returns>执行时间列表</returns>
    public ApiResponseDto<List<DateTimeOffset>> GetNextRunTimes(string cronExpression, int count = 5)
    {
        try
        {
            var expression = new CronExpression(cronExpression);
            var nextRunTimes = new List<DateTimeOffset>();
            var currentTime = DateTimeOffset.Now;

            for (int i = 0; i < count; i++)
            {
                var nextTime = expression.GetNextValidTimeAfter(currentTime);
                if (nextTime.HasValue)
                {
                    nextRunTimes.Add(nextTime.Value);
                    currentTime = nextTime.Value;
                }
                else
                {
                    break;
                }
            }

            return ApiResponseDto<List<DateTimeOffset>>.SuccessResponse(nextRunTimes);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<DateTimeOffset>>.ErrorResponse(
                $"获取下次执行时间失败: {ex.Message}"
            );
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
    public async Task<ApiResponseDto<AnalyticsOverviewDto>> GetAnalyticsOverviewAsync(
        StatsQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var data = await _jobStorage.GetAnalyticsOverviewAsync(queryDto, cancellationToken);
            return ApiResponseDto<AnalyticsOverviewDto>.SuccessResponse(data, "获取统计分析概览数据成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetAnalyticsOverview", ex);
            return ApiResponseDto<AnalyticsOverviewDto>.ErrorResponse($"获取统计分析概览数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取作业性能分析聚合数据（合并健康概览+耗时排行）
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="topCount">耗时排行取前N条</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业性能分析聚合数据</returns>
    public async Task<ApiResponseDto<AnalyticsJobPerformanceDto>> GetAnalyticsJobPerformanceAsync(
        StatsQueryDto queryDto,
        int topCount = 10,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var data = await _jobStorage.GetAnalyticsJobPerformanceAsync(queryDto, topCount, cancellationToken);
            return ApiResponseDto<AnalyticsJobPerformanceDto>.SuccessResponse(data, "获取作业性能分析数据成功");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetAnalyticsJobPerformance", ex);
            return ApiResponseDto<AnalyticsJobPerformanceDto>.ErrorResponse($"获取作业性能分析数据失败: {ex.Message}");
        }
    }

    #endregion

    #region 通知管理

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PushPlus配置</returns>
    public async Task<ApiResponseDto<PushPlusConfigDto>> GetPushPlusConfigAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var config = await _notificationService.GetPushPlusConfigAsync(cancellationToken);
            return ApiResponseDto<PushPlusConfigDto>.SuccessResponse(
                config,
                "获取PushPlus配置成功"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetPushPlusConfig", ex);
            return ApiResponseDto<PushPlusConfigDto>.ErrorResponse(
                $"获取PushPlus配置失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    /// <param name="config">PushPlus配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    public async Task<ApiResponseDto<bool>> SavePushPlusConfigAsync(
        PushPlusConfigDto config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _notificationService.SavePushPlusConfigAsync(
                config,
                cancellationToken
            );
            if (result)
            {
                _logger.LogSuccess("SavePushPlusConfig", "保存PushPlus配置成功");
                return ApiResponseDto<bool>.SuccessResponse(true, "保存PushPlus配置成功");
            }
            else
            {
                _logger.LogWarn("SavePushPlusConfig", "保存PushPlus配置失败");
                return ApiResponseDto<bool>.ErrorResponse("保存PushPlus配置失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("SavePushPlusConfig", ex);
            return ApiResponseDto<bool>.ErrorResponse($"保存PushPlus配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送测试通知
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    public async Task<ApiResponseDto<bool>> SendTestNotificationAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _notificationService.SendTestNotificationAsync(cancellationToken);
            if (result)
            {
                _logger.LogSuccess("SendTestNotification", "发送测试通知成功");
                return ApiResponseDto<bool>.SuccessResponse(true, "发送测试通知成功");
            }
            else
            {
                _logger.LogWarn("SendTestNotification", "发送测试通知失败");
                return ApiResponseDto<bool>.ErrorResponse("发送测试通知失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("SendTestNotification", ex);
            return ApiResponseDto<bool>.ErrorResponse($"发送测试通知失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取通知消息列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息列表</returns>
    public async Task<
        ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>
    > GetNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.GetNotificationsAsync(queryDto, cancellationToken);

            // 将实体转换为DTO
            var notificationDtos = result.Items.Select(MapToNotificationDto).ToList();
            var pagedDto = new PagedResponseDto<QuartzNotificationDto>
            {
                Items = notificationDtos,
                TotalCount = result.TotalCount,
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
            };

            return ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>.SuccessResponse(
                pagedDto,
                "获取通知消息列表成功"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetNotifications", ex);
            return ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>.ErrorResponse(
                $"获取通知消息列表失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 获取通知消息详情
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息详情</returns>
    public async Task<ApiResponseDto<QuartzNotificationDto>> GetNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notification = await _jobStorage.GetNotificationAsync(
                notificationId,
                cancellationToken
            );
            if (notification == null)
            {
                return ApiResponseDto<QuartzNotificationDto>.ErrorResponse("通知不存在");
            }
            var notificationDto = MapToNotificationDto(notification);
            return ApiResponseDto<QuartzNotificationDto>.SuccessResponse(
                notificationDto,
                "获取通知消息详情成功"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetNotification", ex);
            return ApiResponseDto<QuartzNotificationDto>.ErrorResponse(
                $"获取通知消息详情失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    public async Task<ApiResponseDto<bool>> DeleteNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.DeleteNotificationAsync(
                notificationId,
                cancellationToken
            );
            if (result)
            {
                _logger.LogSuccess("DeleteNotification", $"删除通知消息成功: {notificationId}");
                return ApiResponseDto<bool>.SuccessResponse(true, "删除通知消息成功");
            }
            else
            {
                _logger.LogWarning("删除通知消息失败: {NotificationId}", notificationId);
                return ApiResponseDto<bool>.ErrorResponse("删除通知消息失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("DeleteNotification", ex);
            return ApiResponseDto<bool>.ErrorResponse($"删除通知消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清空通知消息
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空结果</returns>
    public async Task<ApiResponseDto<bool>> ClearNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobStorage.ClearNotificationsAsync(queryDto, cancellationToken);
            if (result)
            {
                _logger.LogSuccess("ClearNotifications", "清空通知消息成功");
                return ApiResponseDto<bool>.SuccessResponse(true, "清空通知消息成功");
            }
            else
            {
                _logger.LogWarning("清空通知消息失败");
                return ApiResponseDto<bool>.ErrorResponse("清空通知消息失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ClearNotifications", ex);
            return ApiResponseDto<bool>.ErrorResponse($"清空通知消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将通知实体映射为DTO
    /// </summary>
    /// <param name="notification">通知实体</param>
    /// <returns>通知DTO</returns>
    private QuartzNotificationDto MapToNotificationDto(QuartzNotification notification)
    {
        return new QuartzNotificationDto
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Content = notification.Content,
            Status = notification.Status,
            ErrorMessage = notification.ErrorMessage,
            TriggeredBy = notification.TriggeredBy,
            CreateTime = notification.CreateTime,
            SendTime = notification.SendTime,
            Duration = notification.Duration,
        };
    }

    #endregion

    #region 系统配置

    /// <summary>
    /// 系统配置存储键
    /// </summary>
    private const string SystemConfigSettingKey = "SystemConfig";

    /// <summary>
    /// 系统配置 JSON 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions SystemConfigJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // 中文等非 ASCII 字符按原文输出，避免保存成 \uXXXX 转义
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 枚举统一按字符串读写，避免不同宿主配置下出现数字/字符串两种形态
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// 归一化系统配置：历史「等待完成」已并入「排水后停止」，
    /// 读取与保存都用 Drain 表示，避免存储中的旧值长期被当成「待重启生效的变更」
    /// </summary>
    /// <param name="config">系统配置</param>
    /// <returns>归一化后的配置</returns>
    private static SystemConfigDto NormalizeConfig(SystemConfigDto config)
    {
        if (config.ShutdownMode == ShutdownMode.Wait)
        {
            config.ShutdownMode = ShutdownMode.Drain;
        }

        return config;
    }

    /// <summary>
    /// 获取系统配置
    /// 不存在时返回默认配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>系统配置</returns>
    public async Task<ApiResponseDto<SystemConfigResponseDto>> GetSystemConfigAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var setting = await _jobStorage.GetSettingAsync(
                SystemConfigSettingKey,
                cancellationToken
            );

            SystemConfigDto config;
            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                config =
                    JsonSerializer.Deserialize<SystemConfigDto>(
                        setting.Value,
                        SystemConfigJsonOptions
                    ) ?? new SystemConfigDto();
            }
            else
            {
                config = new SystemConfigDto();
            }

            // 读取路径同样归一化历史「等待完成」：持久化里残留的旧值不再和
            // 生效快照（已按 Drain 运行）反复产生「重启后生效」的差异
            config = NormalizeConfig(config);

            // 兜底：未经过启动 HostedService 时（如单元测试）也能拿到生效快照
            if (!_recoveryOptions.IsInitialized)
            {
                _recoveryOptions.Initialize(config);
            }

            var response =
                JsonSerializer.Deserialize<SystemConfigResponseDto>(
                    JsonSerializer.Serialize(config, SystemConfigJsonOptions),
                    SystemConfigJsonOptions
                ) ?? new SystemConfigResponseDto();
            response.EffectiveConfig = _recoveryOptions.Current;
            response.RestartRequired = _recoveryOptions.ComputeRestartRequired(config);
            response.BootTime = _runtimeInstance.BootTime;

            return ApiResponseDto<SystemConfigResponseDto>.SuccessResponse(
                response,
                "获取系统配置成功"
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetSystemConfig", ex);
            return ApiResponseDto<SystemConfigResponseDto>.ErrorResponse(
                $"获取系统配置失败: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// 保存系统配置
    /// </summary>
    /// <param name="config">系统配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    public async Task<ApiResponseDto<bool>> SaveSystemConfigAsync(
        SystemConfigDto config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 「等待完成」已并入「排水后停止」，历史配置保存时统一归一化为 Drain
            config = NormalizeConfig(config);

            var validationError = RecoveryOptionsProvider.Validate(config);
            if (validationError != null)
            {
                return ApiResponseDto<bool>.ErrorResponse(validationError, "CONFIG_INVALID");
            }

            var json = JsonSerializer.Serialize(config, SystemConfigJsonOptions);
            var setting = new QuartzSetting
            {
                Key = SystemConfigSettingKey,
                Value = json,
                Description = "系统基础配置（服务名称、环境、描述、停机与恢复策略）",
                Enabled = true,
                UpdateTime = DateTime.Now,
            };

            var result = await _jobStorage.SaveSettingAsync(setting, cancellationToken);
            if (result)
            {
                _logger.LogSuccess("SaveSystemConfig", "保存系统配置成功");
                var restartRequired =
                    _recoveryOptions.IsInitialized
                    && _recoveryOptions.ComputeRestartRequired(config);
                return ApiResponseDto<bool>.SuccessResponse(
                    true,
                    restartRequired ? "保存成功，重启应用后生效" : "保存系统配置成功"
                );
            }
            else
            {
                _logger.LogWarn("SaveSystemConfig", "保存系统配置失败");
                return ApiResponseDto<bool>.ErrorResponse("保存系统配置失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("SaveSystemConfig", ex);
            return ApiResponseDto<bool>.ErrorResponse($"保存系统配置失败: {ex.Message}");
        }
    }

    #endregion
}