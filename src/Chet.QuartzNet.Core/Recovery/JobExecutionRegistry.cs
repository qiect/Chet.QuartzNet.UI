using System.Collections.Concurrent;

namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 本进程正在执行的作业登记表。
/// 心跳服务只续期这里的日志，避免全表扫描。
/// </summary>
public sealed class JobExecutionRegistry
{
    /// <summary>
    /// 单条执行登记
    /// </summary>
    /// <param name="LogId">日志ID</param>
    /// <param name="JobName">作业名称</param>
    /// <param name="JobGroup">作业分组</param>
    /// <param name="StartedAt">开始时间</param>
    public sealed record ExecutionEntry(
        Guid LogId,
        string JobName,
        string JobGroup,
        DateTime StartedAt
    );

    private readonly ConcurrentDictionary<Guid, ExecutionEntry> _entries = new();

    /// <summary>
    /// 登记一次执行
    /// </summary>
    public void Register(ExecutionEntry entry) => _entries[entry.LogId] = entry;

    /// <summary>
    /// 移除执行登记
    /// </summary>
    public void Unregister(Guid logId) => _entries.TryRemove(logId, out _);

    /// <summary>
    /// 当前活跃日志ID
    /// </summary>
    public IReadOnlyCollection<Guid> GetActiveLogIds() => _entries.Keys.ToList();

    /// <summary>
    /// 当前活跃执行快照
    /// </summary>
    public IReadOnlyCollection<ExecutionEntry> Snapshot() => _entries.Values.ToList();

    /// <summary>
    /// 活跃执行数量
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// 清空（进程内仅用于测试/停机）
    /// </summary>
    public void Clear() => _entries.Clear();
}
