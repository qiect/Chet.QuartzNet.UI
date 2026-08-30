namespace Chet.QuartzNet.Core.Consts;

/// <summary>
/// Quartz作业常量类
/// </summary>
public static class QuartzJobConst
{
    /// <summary>
    /// 作业数据JSON字符串的Key
    /// </summary>
    public const string JobData = "JobData";

    /// <summary>
    /// 失败重试次数的Key
    /// </summary>
    public const string RetryCount = "RetryCount";

    /// <summary>
    /// 失败重试间隔（秒）的Key
    /// </summary>
    public const string RetryIntervalSeconds = "RetryIntervalSeconds";

    /// <summary>
    /// 重试包装器中真实作业类型全名的Key
    /// </summary>
    public const string RealJobType = "RealJobType";

    /// <summary>
    /// 禁止并发执行的Key
    /// </summary>
    public const string DisallowConcurrentExecution = "DisallowConcurrentExecution";
}