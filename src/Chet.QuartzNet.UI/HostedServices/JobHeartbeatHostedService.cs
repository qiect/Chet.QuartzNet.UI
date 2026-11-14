using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.HostedServices;

/// <summary>
/// 运行中作业的心跳服务：按配置间隔为本进程活跃执行续期。
/// 空闲时不产生任何数据库写入。
/// </summary>
public sealed class JobHeartbeatHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobExecutionRegistry _registry;
    private readonly RecoveryOptionsProvider _options;
    private readonly RuntimeInstance _instance;
    private readonly RecoveryCoordinator _recoveryCoordinator;
    private readonly ILogger<JobHeartbeatHostedService> _logger;

    /// <summary>
    /// 距上次兜底补偿检查累计的秒数（避免每轮心跳都做一次补偿扫描）
    /// </summary>
    private int _pendingScanElapsedSeconds;

    /// <summary>
    /// 初始化心跳服务
    /// </summary>
    public JobHeartbeatHostedService(
        IServiceScopeFactory scopeFactory,
        JobExecutionRegistry registry,
        RecoveryOptionsProvider options,
        RuntimeInstance instance,
        RecoveryCoordinator recoveryCoordinator,
        ILogger<JobHeartbeatHostedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _options = options;
        _instance = instance;
        _recoveryCoordinator = recoveryCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// 心跳循环
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Max(5, _options.Current.HeartbeatIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var activeIds = _registry.GetActiveLogIds();
            var markedAny = false;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var storage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                if (activeIds.Count > 0)
                {
                    await storage.UpdateJobLogHeartbeatsAsync(
                        activeIds,
                        DateTime.Now,
                        stoppingToken
                    );
                }

                // 僵尸清理每轮都要跑：调度器未运行、监听器未捕获完成事件时，
                // 会产生没有活跃执行的 Running 记录，此时 activeIds 恰好为空。
                markedAny = await MarkStaleRunningLogsAsync(storage, activeIds, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "作业心跳/僵死记录清理失败");
            }

            // 僵尸记录刚被标记为中断时立刻补一次补偿：
            // 启动扫描只跑一次，被强杀的进程留下的 Running 记录往往要等心跳过期
            // 才会在运行期被标记，若只标记不补偿，就要等到下次启动才补上。
            // 其余情况每 60 秒兜底重试一次遗留的待补偿记录。
            _pendingScanElapsedSeconds += interval;
            var shouldScanPending = markedAny || _pendingScanElapsedSeconds >= 60;
            if (shouldScanPending)
            {
                _pendingScanElapsedSeconds = 0;
                try
                {
                    await _recoveryCoordinator.RunPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "待补偿中断记录处理失败");
                }
            }
        }
    }

    /// <summary>
    /// 把心跳已过期、且不在本进程活跃执行中的 Running 记录标记为中断，
    /// 避免调度器未运行或执行结束事件丢失时留下永不收尾的「运行中」记录。
    /// </summary>
    private async Task<bool> MarkStaleRunningLogsAsync(
        IJobStorage storage,
        IReadOnlyCollection<Guid> activeIds,
        CancellationToken cancellationToken
    )
    {
        var leaseSeconds = Math.Max(30, _options.Current.LeaseDurationSeconds);
        var staleBefore = DateTime.Now.AddSeconds(-leaseSeconds);
        var candidates = await storage.GetRecoverableLogsAsync(
            staleBefore,
            200,
            cancellationToken
        );
        var activeSet = activeIds.ToHashSet();
        var markedAny = false;

        foreach (var candidate in candidates)
        {
            if (
                candidate.Status != LogStatus.Running
                || activeSet.Contains(candidate.LogId)
            )
            {
                continue;
            }

            var marked = await storage.TryMarkInterruptedAsync(
                candidate.LogId,
                _instance.InstanceId,
                DateTime.Now,
                "执行未正常结束（调度器未运行或未捕获到完成事件）",
                cancellationToken
            );
            if (marked)
            {
                markedAny = true;
                _logger.LogWarning(
                    "僵死运行记录已标记为中断: {LogId} {JobGroup}.{JobName}",
                    candidate.LogId,
                    candidate.JobGroup,
                    candidate.JobName
                );
            }
        }

        return markedAny;
    }
}
