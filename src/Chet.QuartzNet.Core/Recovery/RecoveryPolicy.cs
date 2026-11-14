using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 单条中断记录的补偿决策
/// </summary>
public enum RecoveryDecision
{
    /// <summary>
    /// 触发重跑
    /// </summary>
    Rerun = 0,

    /// <summary>
    /// 跳过
    /// </summary>
    Skip = 1,
}

/// <summary>
/// 中断补偿策略。按顺序短路，第一条命中的规则生效。
/// </summary>
public static class RecoveryPolicy
{
    /// <summary>
    /// 评估一条中断记录
    /// </summary>
    /// <param name="log">中断日志</param>
    /// <param name="job">作业信息，null 表示作业已删除</param>
    /// <param name="options">启动期配置快照</param>
    /// <param name="nextFireTime">作业下次触发时间，null 表示无触发器</param>
    /// <param name="now">当前时间</param>
    /// <returns>决策与原因</returns>
    public static (RecoveryDecision Decision, string? Reason) Evaluate(
        QuartzJobLog log,
        QuartzJobInfo? job,
        SystemConfigDto options,
        DateTime? nextFireTime,
        DateTime now
    )
    {
        if (!options.RecoveryEnabled)
        {
            return (RecoveryDecision.Skip, "自动补偿已关闭");
        }

        if (log.CompensationStatus == CompensationStatus.Triggered)
        {
            return (RecoveryDecision.Skip, "已补偿");
        }

        if (log.RecoveryAttempt >= options.MaxRecoveryAttempts)
        {
            return (
                RecoveryDecision.Skip,
                $"已是第 {log.RecoveryAttempt} 次执行，达到最大执行次数 {options.MaxRecoveryAttempts}（含原执行），疑似启动即崩"
            );
        }

        if (job == null)
        {
            return (RecoveryDecision.Skip, "作业已删除");
        }

        if (!job.IsEnabled)
        {
            return (RecoveryDecision.Skip, "作业已禁用");
        }

        // 暂停的作业不参与自动补偿：重跑只会被监听器判定为跳过，
        // 白白消耗一次补偿资格
        if (job.Status == JobStatus.Paused)
        {
            return (RecoveryDecision.Skip, "作业已暂停");
        }

        if (job.RerunOnRecovery == false)
        {
            return (RecoveryDecision.Skip, "作业已关闭自动重跑");
        }

        if (
            options.SkipIfNextFireWithinSeconds > 0
            && nextFireTime.HasValue
            && nextFireTime.Value > now
            && (nextFireTime.Value - now).TotalSeconds
                <= options.SkipIfNextFireWithinSeconds
        )
        {
            return (RecoveryDecision.Skip, "临近下次触发");
        }

        if (options.CatchUpStrategy == CatchUpStrategy.None)
        {
            return (RecoveryDecision.Skip, "补偿策略为不补偿");
        }

        return (RecoveryDecision.Rerun, null);
    }
}
