using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.HostedServices;

/// <summary>
/// 启动恢复服务：在调度器与作业加载完成后，加载配置快照并执行一次中断补偿。
/// </summary>
public sealed class RecoveryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecoveryOptionsProvider _options;
    private readonly RecoveryCoordinator _coordinator;
    private readonly ILogger<RecoveryHostedService> _logger;

    /// <summary>
    /// 初始化启动恢复服务
    /// </summary>
    public RecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        RecoveryOptionsProvider options,
        RecoveryCoordinator coordinator,
        ILogger<RecoveryHostedService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _coordinator = coordinator;
        _logger = logger;
    }

    /// <summary>
    /// 执行启动恢复
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 等待调度器与作业加载完成
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

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
}
