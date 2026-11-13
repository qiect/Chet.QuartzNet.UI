using Chet.QuartzNet.Models.DTOs;

namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 本次启动的恢复汇总（进程内单例）
/// </summary>
public sealed class RecoverySummaryStore
{
    private const int MaxDetails = 100;

    private readonly object _lock = new();
    private readonly RecoverySummaryDto _summary;

    /// <summary>
    /// 初始化汇总
    /// </summary>
    public RecoverySummaryStore(RuntimeInstance instance)
    {
        _summary = new RecoverySummaryDto
        {
            BootId = instance.BootId,
            InstanceId = instance.InstanceId,
            BootTime = instance.BootTime,
            IsRunning = false,
        };
    }

    /// <summary>
    /// 标记恢复开始
    /// </summary>
    public void Begin()
    {
        lock (_lock)
        {
            _summary.IsRunning = true;
            _summary.StartedAt = DateTime.Now;
            _summary.CompletedAt = null;
        }
    }

    /// <summary>
    /// 标记恢复完成
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            _summary.IsRunning = false;
            _summary.CompletedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// 记录检测到的中断数
    /// </summary>
    public void AddInterrupted(int count = 1)
    {
        lock (_lock)
        {
            _summary.InterruptedCount += count;
        }
    }

    /// <summary>
    /// 记录单条处理结果
    /// </summary>
    public void Record(
        Guid logId,
        string jobName,
        string jobGroup,
        int attempt,
        RecoveryOutcome outcome,
        string? reason
    )
    {
        lock (_lock)
        {
            switch (outcome)
            {
                case RecoveryOutcome.Rerun:
                    _summary.RerunCount++;
                    break;
                case RecoveryOutcome.Skipped:
                    _summary.SkippedCount++;
                    break;
                case RecoveryOutcome.Failed:
                    _summary.FailedCount++;
                    break;
            }

            if (_summary.Details.Count < MaxDetails)
            {
                _summary.Details.Add(
                    new RecoveryDetailDto
                    {
                        LogId = logId,
                        JobName = jobName,
                        JobGroup = jobGroup,
                        Attempt = attempt,
                        Outcome = outcome,
                        Reason = reason,
                    }
                );
            }
            else
            {
                _summary.Truncated = true;
            }
        }
    }

    /// <summary>
    /// 获取汇总快照
    /// </summary>
    public RecoverySummaryDto Snapshot()
    {
        lock (_lock)
        {
            var done = _summary.RerunCount + _summary.SkippedCount + _summary.FailedCount;
            return new RecoverySummaryDto
            {
                BootId = _summary.BootId,
                InstanceId = _summary.InstanceId,
                BootTime = _summary.BootTime,
                StartedAt = _summary.StartedAt,
                CompletedAt = _summary.CompletedAt,
                IsRunning = _summary.IsRunning,
                InterruptedCount = _summary.InterruptedCount,
                RerunCount = _summary.RerunCount,
                SkippedCount = _summary.SkippedCount,
                FailedCount = _summary.FailedCount,
                PendingCount = Math.Max(0, _summary.InterruptedCount - done),
                Truncated = _summary.Truncated,
                Details = _summary.Details.ToList(),
            };
        }
    }
}
