using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace Chet.QuartzNet.Core.Jobs;

/// <summary>
/// 作业工厂：在 DI 作业工厂外为每次执行实例包一层取消可见性装饰器。
/// </summary>
/// <remarks>
/// Quartz 在令牌已取消时会把作业抛出的原生 <see cref="OperationCanceledException"/> 吞掉
/// （JobRunShell 将其归为 Cancelled，传给 <c>JobWasExecuted</c> 的异常为 null），
/// 监听器因此无法区分「因取消而结束」与「忽略令牌后正常完成」，
/// 会把已标记为「中断」的日志按真实结果覆盖成「成功」。
/// 本装饰器把这类取消异常包装成 <see cref="JobExecutionException"/> 再抛出，
/// 使监听器的中断保留条件（已标记中断 + 异常非空 + 令牌已取消）对按文档编写的
/// DLL 作业同样成立。忽略令牌正常结束的作业不受影响，仍按真实结果收尾。
/// </remarks>
public class CancellationWrappingJobFactory : MicrosoftDependencyInjectionJobFactory
{
    public CancellationWrappingJobFactory(
        IServiceProvider serviceProvider,
        IOptions<QuartzOptions> options
    ) : base(serviceProvider, options)
    {
    }

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var inner = base.NewJob(bundle, scheduler);
        return new CancellationWrappingJobDecorator(inner);
    }
}

/// <summary>
/// 取消可见性装饰器：捕获「令牌已取消」时作业抛出的原生取消异常并包装后重新抛出。
/// 其它异常（含 <see cref="JobExecutionException"/>）原样透传，不改变现有行为。
/// </summary>
public class CancellationWrappingJobDecorator : IJob
{
    private readonly IJob _inner;

    public CancellationWrappingJobDecorator(IJob inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _inner.Execute(context);
        }
        catch (OperationCanceledException ex)
            when (context.CancellationToken.IsCancellationRequested)
        {
            throw new JobExecutionException(
                ex.Message ?? "作业被取消",
                ex
            );
        }
    }
}
