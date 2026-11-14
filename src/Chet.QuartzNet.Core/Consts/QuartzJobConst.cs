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

    /// <summary>
    /// 手动触发标记的Key（执行级，兼容历史数据）
    /// </summary>
    public const string IsManualTrigger = "IsManualTrigger";

    /// <summary>
    /// 执行日志ID的Key（执行级，不能写入 JobDetail）
    /// </summary>
    public const string LogId = "__QuartzLogId";

    /// <summary>
    /// 触发来源的Key（执行级）
    /// </summary>
    public const string TriggerType = "__QuartzTriggerType";

    /// <summary>
    /// 补偿来源日志ID的Key（执行级）
    /// </summary>
    public const string RecoverySourceLogId = "__QuartzRecoverySourceLogId";

    /// <summary>
    /// 补偿尝试次数的Key（执行级）
    /// </summary>
    public const string RecoveryAttempt = "__QuartzRecoveryAttempt";

    /// <summary>
    /// 操作人的Key（执行级）
    /// </summary>
    public const string TriggeredBy = "__QuartzTriggeredBy";
}