namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 当前进程的运行实例信息。用于区分「本次启动」与历史运行。
/// </summary>
public sealed class RuntimeInstance
{
    /// <summary>
    /// 实例标识（机器名:进程号:启动短标识）
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// 本次启动标识
    /// </summary>
    public Guid BootId { get; } = Guid.NewGuid();

    /// <summary>
    /// 进程启动时间
    /// </summary>
    public DateTime BootTime { get; } = DateTime.Now;

    /// <summary>
    /// 初始化实例信息
    /// </summary>
    public RuntimeInstance()
    {
        InstanceId =
            $"{Environment.MachineName}:{Environment.ProcessId}:{BootId.ToString("N")[..8]}";
    }
}
