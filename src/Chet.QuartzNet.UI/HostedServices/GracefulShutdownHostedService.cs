using Chet.QuartzNet.Core.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.HostedServices;

/// <summary>
/// 优雅停机服务：在 Quartz 关闭前执行排水/等待，并标记残留执行。
/// 必须最后注册，停止时最先执行。
/// </summary>
public sealed class GracefulShutdownHostedService : IHostedService
{
    private readonly DrainCoordinator _drainCoordinator;
    private readonly ILogger<GracefulShutdownHostedService> _logger;

    /// <summary>
    /// 初始化优雅停机服务
    /// </summary>
    public GracefulShutdownHostedService(
        DrainCoordinator drainCoordinator,
        ILogger<GracefulShutdownHostedService> logger
    )
    {
        _drainCoordinator = drainCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// 无需启动逻辑
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 应用停止时执行排水等待
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _drainCoordinator.GracefulStopAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("优雅停机等待被宿主取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "优雅停机执行失败");
        }
    }
}
