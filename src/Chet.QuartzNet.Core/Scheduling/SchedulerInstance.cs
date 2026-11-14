using Quartz;

namespace Chet.QuartzNet.Core.Scheduling;

/// <summary>
/// 进程内「当前调度器实例」的持有者。
/// Quartz 在调度器 Shutdown 后会把它从实例仓库移除，此时再调用
/// <see cref="ISchedulerFactory.GetScheduler"/> 会隐式创建一个「从未启动」的新实例，
/// 导致已停止的调度器在状态接口里显示成「待机」。
/// 因此状态读取等只读场景必须复用这里的当前实例，只有显式启动时才允许创建新实例。
/// </summary>
public sealed class SchedulerInstance
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IScheduler? _current;

    /// <summary>
    /// 当前实例（可能已被关闭，也可能尚未创建）
    /// </summary>
    public IScheduler? Current => _current;

    /// <summary>
    /// 设置当前实例（启动或重建调度器后调用）
    /// </summary>
    public void Set(IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _current = scheduler;
    }

    /// <summary>
    /// 获取当前实例；不存在或已关闭时按需创建（仅用于确实需要调度器的操作）
    /// </summary>
    public async Task<IScheduler> GetSchedulerAsync(
        ISchedulerFactory schedulerFactory,
        CancellationToken cancellationToken = default
    )
    {
        var current = _current;
        if (current is not null && !current.IsShutdown)
        {
            return current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            current = _current;
            if (current is not null && !current.IsShutdown)
            {
                return current;
            }

            current = await schedulerFactory.GetScheduler(cancellationToken);
            _current = current;
            return current;
        }
        finally
        {
            _gate.Release();
        }
    }
}
