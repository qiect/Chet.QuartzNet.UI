using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.HostedServices;

/// <summary>
/// 启动恢复服务：在调度器与作业加载完成后，加载配置快照并执行一次中断补偿。
/// </summary>
public sealed class RecoveryHostedService : BackgroundService
{
    /// <summary>
    /// 等待调度器进入运行状态的最长时间
    /// </summary>
    private static readonly TimeSpan SchedulerWaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 轮询间隔
    /// </summary>
    private static readonly TimeSpan SchedulerPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryOptionsProvider _options;
    private readonly RecoveryCoordinator _coordinator;
    private readonly SchedulerInstance _schedulerInstance;
    private readonly ILogger<RecoveryHostedService> _logger;

    /// <summary>
    /// 初始化启动恢复服务
    /// </summary>
    public RecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        RecoveryOptionsProvider options,
        RecoveryCoordinator coordinator,
        SchedulerInstance schedulerInstance,
        ILogger<RecoveryHostedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _coordinator = coordinator;
        _schedulerInstance = schedulerInstance;
        _logger = logger;
    }

    /// <summary>
    /// 执行启动恢复
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 等待调度器真正启动：宿主服务启动顺序虽然固定，但启动耗时不确定，
            // 固定延迟既可能在调度器就绪前就开始补偿，也会白白拖慢启动
            await WaitForSchedulerStartedAsync(stoppingToken);

            using (var scope = _scopeFactory.CreateScope())
            {
                var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();
                var configResult = await jobService.GetSystemConfigAsync(stoppingToken);
                var config = configResult.Data;
                if (config == null)
                {
                    _logger.LogWarning("读取系统配置失败，跳过启动补偿: {Message}", configResult.Message);
                    return;
                }

                _options.Initialize(config);
            }

            await _coordinator.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 应用停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动中断恢复失败");
        }
    }

    /// <summary>
    /// 轮询等待调度器进入运行状态（未关闭、已启动且不在待机）
    /// </summary>
    /// <param name="stoppingToken">停机令牌</param>
    private async Task WaitForSchedulerStartedAsync(CancellationToken stoppingToken)
    {
        var deadline = DateTime.UtcNow + SchedulerWaitTimeout;
        while (!stoppingToken.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var scheduler = _schedulerInstance.Current;
            if (scheduler is { IsShutdown: false, IsStarted: true, InStandbyMode: false })
            {
                return;
            }

            await Task.Delay(SchedulerPollInterval, stoppingToken);
        }

        _logger.LogWarning(
            "等待调度器启动超时（{Timeout} 秒），继续尝试执行启动补偿",
            SchedulerWaitTimeout.TotalSeconds
        );
    }
}
