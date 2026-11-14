using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Chet.QuartzNet.Core.Scheduling;

/// <summary>
/// 排水阶段
/// </summary>
public enum DrainPhase
{
    /// <summary>未在排水</summary>
    Idle,

    /// <summary>正在排水，可以取消并恢复调度</summary>
    Draining,

    /// <summary>排水已收尾，正在关闭调度器（不可取消）</summary>
    Stopping,

    /// <summary>排水已结束，调度器已关闭，只能重新启动</summary>
    Completed,
}

/// <summary>
/// 排水协调器：停止接收新触发，等待执行中任务收尾，超时后关闭并标记残留。
/// </summary>
public sealed class DrainCoordinator : IDisposable
{
    /// <summary>
    /// Standby 后至少观察这么久才允许判定「无执行中任务」。
    /// 给排队任务进入执行队列、以及前端刷新状态留出窗口，避免瞬间停机导致「恢复调度」失效。
    /// </summary>
    private static readonly TimeSpan MinimumObserveWindow = TimeSpan.FromSeconds(3);
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly SchedulerInstance _schedulerInstance;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryOptionsProvider _options;
    private readonly RuntimeInstance _instance;
    private readonly JobExecutionRegistry _registry;
    private readonly ILogger<DrainCoordinator> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    /// <summary>
    /// 初始化排水协调器
    /// </summary>
    public DrainCoordinator(
        ISchedulerFactory schedulerFactory,
        SchedulerInstance schedulerInstance,
        IServiceScopeFactory scopeFactory,
        RecoveryOptionsProvider options,
        RuntimeInstance instance,
        JobExecutionRegistry registry,
        ILogger<DrainCoordinator> logger
    )
    {
        _schedulerFactory = schedulerFactory;
        _schedulerInstance = schedulerInstance;
        _scopeFactory = scopeFactory;
        _options = options;
        _instance = instance;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// 取当前调度器实例：优先复用进程内缓存，避免向工厂隐式索取到「从未启动」的新实例
    /// </summary>
    private async Task<IScheduler> GetSchedulerAsync(CancellationToken cancellationToken)
    {
        return _schedulerInstance.Current
            ?? await _schedulerFactory.GetScheduler(cancellationToken);
    }

    /// <summary>
    /// 当前排水阶段
    /// </summary>
    public DrainPhase Phase { get; private set; } = DrainPhase.Idle;

    /// <summary>
    /// 是否仍处于排水流程中（排水等待或正在关闭调度器）
    /// </summary>
    public bool IsDraining => Phase is DrainPhase.Draining or DrainPhase.Stopping;

    /// <summary>
    /// 是否处于排水收尾（正在关闭调度器）
    /// </summary>
    public bool IsStopping => Phase == DrainPhase.Stopping;

    /// <summary>
    /// 排水开始时间
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>
    /// 排水截止时间
    /// </summary>
    public DateTime? Deadline { get; private set; }

    /// <summary>
    /// 排水结束时间
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// 仍需等待的执行中任务数
    /// </summary>
    public int RemainingExecutingCount { get; private set; }

    /// <summary>
    /// 开始排水。
    /// 停机策略为「立即停止」时，排水等价于立即关闭调度器，不等待执行中任务。
    /// </summary>
    public async Task<ApiResponseDto<bool>> BeginAsync(
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default,
        bool allowCancelWindow = true
    )
    {
        await _gate.WaitAsync(cancellationToken);
        var monitorStarted = false;
        try
        {
            if (IsDraining)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器已在排水",
                    "SCHEDULER_ALREADY_DRAINING"
                );
            }

            var scheduler = await GetSchedulerAsync(cancellationToken);
            if (scheduler.IsShutdown || !scheduler.IsStarted)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器未启动，无法排水",
                    "SCHEDULER_NOT_STARTED"
                );
            }

            var options = _options.Current;
            var executing = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);

            Phase = DrainPhase.Draining;
            StartedAt = DateTime.Now;
            Deadline = null;
            CompletedAt = null;
            RemainingExecutingCount = executing.Count;

            // 停机策略为「立即停止」：排水不再等待执行中任务，直接关闭调度器
            var immediate =
                options.ShutdownMode == ShutdownMode.Immediate
                && timeoutSeconds is null or <= 0;

            if (immediate)
            {
                return await StopImmediatelyAsync(scheduler, executing.Count);
            }

            var timeout =
                timeoutSeconds is > 0
                    ? timeoutSeconds.Value
                    : Math.Max(1, options.DrainTimeoutSeconds);

            Deadline = StartedAt.Value.AddSeconds(timeout);

            await scheduler.Standby(cancellationToken);

            _monitorCts = new CancellationTokenSource();
            _monitorTask = MonitorAsync(
                scheduler,
                Deadline.Value,
                allowCancelWindow,
                _monitorCts.Token
            );
            monitorStarted = true;

            _logger.LogInformation(
                "进入排水模式，当前执行中 {Count} 个，超时 {Timeout} 秒",
                RemainingExecutingCount,
                timeout
            );

            return ApiResponseDto<bool>.SuccessResponse(true, "已进入排水模式");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始排水失败");
            // 排水流程未真正启动（例如 Standby 失败），回退状态，避免前端一直显示「排水」
            if (!monitorStarted)
            {
                Reset();
            }

            return ApiResponseDto<bool>.ErrorResponse($"开始排水失败: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 「立即停止」策略下的排水：直接关闭调度器并把执行中任务标记为中断。
    /// </summary>
    private async Task<ApiResponseDto<bool>> StopImmediatelyAsync(
        IScheduler scheduler,
        int interruptedCount
    )
    {
        Phase = DrainPhase.Stopping;
        RemainingExecutingCount = 0;

        var stopped = await TryShutdownAsync(scheduler);
        if (!stopped)
        {
            Reset();
            return ApiResponseDto<bool>.ErrorResponse(
                "关闭调度器失败，请稍后重试或直接停止调度器",
                "SCHEDULER_STOP_FAILED"
            );
        }

        if (interruptedCount > 0)
        {
            await MarkRemainingInterruptedAsync("按「立即停止」策略关闭调度器，执行未正常完成");
        }

        Complete();
        _logger.LogInformation(
            "按「立即停止」策略关闭调度器，中断执行中任务 {Count} 个",
            interruptedCount
        );

        return ApiResponseDto<bool>.SuccessResponse(
            true,
            interruptedCount > 0
                ? $"已按「立即停止」策略关闭调度器，中断 {interruptedCount} 个执行中任务"
                : "已按「立即停止」策略关闭调度器"
        );
    }

    /// <summary>
    /// 取消排水并恢复调度
    /// </summary>
    public async Task<ApiResponseDto<bool>> ResumeAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Phase == DrainPhase.Completed)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "排水已完成并停止了调度器，请点击「启动调度器」重新启动",
                    "SCHEDULER_DRAIN_COMPLETED"
                );
            }

            if (Phase == DrainPhase.Stopping)
            {
                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器正在停止，无法取消",
                    "SCHEDULER_DRAIN_STOPPING"
                );
            }

            var scheduler = await GetSchedulerAsync(cancellationToken);
            if (scheduler.IsShutdown)
            {
                if (Phase == DrainPhase.Draining)
                {
                    Complete();
                }

                return ApiResponseDto<bool>.ErrorResponse(
                    "排水已完成并停止了调度器，请点击「启动调度器」重新启动",
                    "SCHEDULER_DRAIN_COMPLETED"
                );
            }

            if (Phase != DrainPhase.Draining)
            {
                // 兜底：调度器停在待机但不在排水流程中（例如上次排水收尾失败），允许直接恢复
                if (scheduler.InStandbyMode)
                {
                    await scheduler.Start(cancellationToken);
                    Reset();
                    _logger.LogInformation("调度器处于待机且未在排水，已恢复调度");
                    return ApiResponseDto<bool>.SuccessResponse(true, "已恢复调度");
                }

                return ApiResponseDto<bool>.ErrorResponse(
                    "调度器未处于排水状态，无需恢复",
                    "SCHEDULER_NOT_DRAINING"
                );
            }

            _monitorCts?.Cancel();
            await scheduler.Start(cancellationToken);

            Reset();

            _logger.LogInformation("已取消排水并恢复调度");
            return ApiResponseDto<bool>.SuccessResponse(true, "已恢复调度");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复调度失败");
            return ApiResponseDto<bool>.ErrorResponse($"恢复调度失败: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 调度器重新启动后重置排水状态，避免上一次排水的结果影响后续操作。
    /// </summary>
    public void Reset()
    {
        Phase = DrainPhase.Idle;
        StartedAt = null;
        Deadline = null;
        CompletedAt = null;
        RemainingExecutingCount = 0;
    }

    /// <summary>
    /// 应用关闭时执行优雅停机
    /// </summary>
    public async Task GracefulStopAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Current;
        if (options.ShutdownMode == ShutdownMode.Immediate)
        {
            return;
        }

        var scheduler = await GetSchedulerAsync(cancellationToken);
        if (scheduler.IsShutdown || !scheduler.IsStarted)
        {
            return;
        }

        // 「等待完成」已并入「排水后停止」，两者行为一致，统一使用排水超时
        var timeout = options.DrainTimeoutSeconds;

        if (timeout <= 0)
        {
            return;
        }

        var result = await BeginAsync(timeout, cancellationToken, false);
        if (!result.Success || _monitorTask == null)
        {
            return;
        }

        try
        {
            await _monitorTask.WaitAsync(
                TimeSpan.FromSeconds(timeout + 5),
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("优雅停机等待被取消，残留执行将在下次启动时判定为中断");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("优雅停机等待超时，残留执行将在下次启动时判定为中断");
        }
    }

    private async Task MonitorAsync(
        IScheduler scheduler,
        DateTime deadline,
        bool allowCancelWindow,
        CancellationToken ct
    )
    {
        var timedOut = false;
        var remainingWindow = deadline - DateTime.Now;
        var observeWindow =
            allowCancelWindow && remainingWindow > TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(
                    Math.Min(MinimumObserveWindow.TotalMilliseconds, remainingWindow.TotalMilliseconds / 3)
                )
                : TimeSpan.Zero;
        var observeUntil = DateTime.Now.Add(observeWindow);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var executing = await scheduler.GetCurrentlyExecutingJobs(ct);
                RemainingExecutingCount = executing.Count;

                // 观察窗口内不判定完成：Standby 后排队中的任务可能才刚进入执行队列
                if (RemainingExecutingCount == 0 && DateTime.Now >= observeUntil)
                {
                    break;
                }

                if (DateTime.Now >= deadline)
                {
                    timedOut = true;
                    break;
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "排水监视失败");
            timedOut = true;
        }

        // 恢复调度已抢先取消，不要再关闭调度器、也不要覆盖 Idle 状态
        if (ct.IsCancellationRequested)
        {
            return;
        }

        // 进入收尾阶段：此时仍保持 IsDraining，避免前端在关闭调度器的瞬间显示成「待机」
        Phase = DrainPhase.Stopping;
        RemainingExecutingCount = 0;

        var stopped = await TryShutdownAsync(scheduler);
        if (!stopped)
        {
            // 关闭失败：回退到真实状态，让前端能显示调度器的真实状态并允许重新操作
            Reset();
            _logger.LogWarning("排水收尾未能关闭调度器，已回退排水状态，调度器当前状态以实际为准");
            return;
        }

        if (timedOut)
        {
            await MarkRemainingInterruptedAsync("排水超时，执行未正常完成");
        }

        Complete();
    }

    /// <summary>
    /// 关闭调度器。
    /// 排水阶段已经确认过剩余执行中任务数，这里不再等待任务完成，
    /// 避免个别任务卡死导致调度器迟迟不关闭、前端状态一直停在「排水」。
    /// </summary>
    private async Task<bool> TryShutdownAsync(IScheduler scheduler)
    {
        try
        {
            if (!scheduler.IsShutdown)
            {
                await scheduler.Shutdown(false, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "排水完成阶段关闭调度器失败");
        }

        return scheduler.IsShutdown;
    }

    private void Complete()
    {
        Phase = DrainPhase.Completed;
        CompletedAt = DateTime.Now;
        RemainingExecutingCount = 0;
        StartedAt = null;
        Deadline = null;
    }

    private async Task MarkRemainingInterruptedAsync(string reason)
    {
        var active = _registry.Snapshot();
        if (active.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
            foreach (var entry in active)
            {
                await storage.TryMarkInterruptedAsync(
                    entry.LogId,
                    _instance.InstanceId,
                    DateTime.Now,
                    reason,
                    CancellationToken.None
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记排水残留执行失败");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _gate.Dispose();
    }
}
