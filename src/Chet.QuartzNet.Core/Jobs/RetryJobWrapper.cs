using Chet.QuartzNet.Core.Consts;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Chet.QuartzNet.Core.Jobs;

/// <summary>
/// 作业失败重试包装器
/// 配置了重试次数的作业注册为本类型，执行时解析真实作业类型并循环执行：
/// 成功即返回；失败且未达重试上限时等待间隔后重试，每次重试记录一条日志。
/// 仅当 RetryCount > 0 时才会注册为本包装器，未配置重试的作业（含旧版本数据）仍直接注册真实类型，行为不变。
/// </summary>
public class RetryJobWrapper : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeInstance _instance;
    private readonly ILogger<RetryJobWrapper> _logger;

    public RetryJobWrapper(
        IServiceScopeFactory scopeFactory,
        RuntimeInstance instance,
        ILogger<RetryJobWrapper> logger
    )
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // 读取重试配置
        var dataMap = context.MergedJobDataMap;
        var retryCount =
            dataMap.TryGetValue(QuartzJobConst.RetryCount, out var countValue)
            && countValue is int c
                ? c
                : 0;
        var retryIntervalSeconds =
            dataMap.TryGetValue(QuartzJobConst.RetryIntervalSeconds, out var intervalValue)
            && intervalValue is int i && i > 0
                ? i
                : 30;
        var realJobTypeName =
            dataMap.TryGetValue(QuartzJobConst.RealJobType, out var typeValue)
                ? typeValue?.ToString()
                : null;

        // 未配置重试（含异常兜底）时直接执行真实作业
        if (retryCount <= 0 || string.IsNullOrEmpty(realJobTypeName))
        {
            await ExecuteRealJobAsync(context, realJobTypeName);
            return;
        }

        var realJobType = ResolveJobType(realJobTypeName);
        if (realJobType == null)
        {
            throw new JobExecutionException($"重试包装器无法解析真实作业类型: {realJobTypeName}");
        }

        var totalAttempts = retryCount + 1; // 首次执行 + N 次重试
        Exception? lastException = null;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            // 本次尝试的真实起止时间，用于生成准确的重试日志
            var attemptStartedAt = DateTime.Now;

            try
            {
                // 每次尝试用独立作用域创建作业实例，保证依赖状态干净
                using var scope = _scopeFactory.CreateScope();
                var realJob = (IJob)ActivatorUtilities.CreateInstance(scope.ServiceProvider, realJobType);
                await realJob.Execute(context);

                if (attempt > 1)
                {
                    _logger.LogInfo(
                        "作业重试",
                        $"重试成功: {context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}, 第 {attempt - 1} 次重试后成功"
                    );
                }
                return; // 执行成功，结束
            }
            catch (OperationCanceledException)
            {
                // 取消（如应用停机）不重试，直接抛出让调度器处理
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                // 还有重试机会：记录重试日志并等待
                if (attempt < totalAttempts)
                {
                    await WriteRetryLogAsync(
                        context,
                        attempt,
                        totalAttempts,
                        attemptStartedAt,
                        DateTime.Now,
                        retryIntervalSeconds,
                        ex
                    );
                    try
                    {
                        // 执行上下文的令牌在停机或「删除/禁用作业」触发 Interrupt 时会被取消
                        await Task.Delay(
                            TimeSpan.FromSeconds(retryIntervalSeconds),
                            context.CancellationToken
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        // 等待期间被取消，终止重试
                        throw new JobExecutionException("重试等待期间作业被取消", ex);
                    }
                }
            }
        }

        // 全部尝试均失败，抛出最终异常由监听器记录
        throw new JobExecutionException(
            $"作业连续 {totalAttempts} 次执行均失败（含 {retryCount} 次重试）",
            lastException
        );
    }

    /// <summary>
    /// 直接执行真实作业（未配置重试的兜底路径）
    /// </summary>
    private async Task ExecuteRealJobAsync(IJobExecutionContext context, string? realJobTypeName)
    {
        if (string.IsNullOrEmpty(realJobTypeName))
        {
            throw new JobExecutionException("重试包装器缺少真实作业类型配置");
        }

        var realJobType = ResolveJobType(realJobTypeName);
        if (realJobType == null)
        {
            throw new JobExecutionException($"重试包装器无法解析真实作业类型: {realJobTypeName}");
        }

        using var scope = _scopeFactory.CreateScope();
        var realJob = (IJob)ActivatorUtilities.CreateInstance(scope.ServiceProvider, realJobType);
        await realJob.Execute(context);
    }

    /// <summary>
    /// 解析真实作业类型（程序集限定名直接解析，失败时搜索已加载程序集）
    /// </summary>
    private static Type? ResolveJobType(string typeName)
    {
        var jobType = Type.GetType(typeName);
        if (jobType != null)
        {
            return jobType;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            jobType = assembly.GetType(typeName);
            if (jobType != null)
            {
                return jobType;
            }
        }
        return null;
    }

    /// <summary>
    /// 记录一次重试日志（每次重试独立记录，便于审计执行轨迹）
    /// </summary>
    /// <param name="context">作业执行上下文</param>
    /// <param name="attempt">当前尝试序号（从 1 开始）</param>
    /// <param name="totalAttempts">总尝试次数（首次 + 重试）</param>
    /// <param name="startedAt">本次尝试的真实开始时间</param>
    /// <param name="endedAt">本次尝试的真实结束时间</param>
    /// <param name="retryIntervalSeconds">重试间隔（秒）</param>
    /// <param name="ex">本次尝试抛出的异常</param>
    private async Task WriteRetryLogAsync(
        IJobExecutionContext context,
        int attempt,
        int totalAttempts,
        DateTime startedAt,
        DateTime endedAt,
        int retryIntervalSeconds,
        Exception ex
    )
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

            var jobLog = new QuartzJobLog
            {
                LogId = Guid.NewGuid(),
                JobName = context.JobDetail.Key.Name,
                JobGroup = context.JobDetail.Key.Group,
                TriggerName = context.Trigger.Key.Name,
                TriggerGroup = context.Trigger.Key.Group,
                Status = LogStatus.Failed,
                StartTime = startedAt,
                EndTime = endedAt,
                // 使用本次尝试的真实耗时，避免用重试间隔倒推出假数据
                Duration = Math.Max(0, (long)(endedAt - startedAt).TotalMilliseconds),
                Message =
                    $"第 {attempt}/{totalAttempts} 次执行失败，{retryIntervalSeconds} 秒后将重试",
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                TriggerType = ResolveTriggerType(context),
                InstanceId = _instance.InstanceId,
                BootId = _instance.BootId,
                CreateTime = DateTime.Now,
            };

            await jobStorage.AddJobLogAsync(jobLog, context.CancellationToken);
        }
        catch (Exception logEx)
        {
            // 重试日志写入失败不影响重试流程
            _logger.LogFailure("记录重试日志", logEx);
        }
    }

    /// <summary>
    /// 解析本次执行的触发来源（与 QuartzJobListener 保持一致）
    /// </summary>
    private static TriggerType ResolveTriggerType(IJobExecutionContext context)
    {
        if (
            context.MergedJobDataMap.TryGetValue(QuartzJobConst.TriggerType, out var value)
            && value != null
            && Enum.TryParse<TriggerType>(value.ToString(), true, out var parsed)
        )
        {
            return parsed;
        }

        return TriggerType.Cron;
    }
}
