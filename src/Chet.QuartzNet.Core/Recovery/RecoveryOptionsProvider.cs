using System.Text.Json;
using Chet.QuartzNet.Models.DTOs;

namespace Chet.QuartzNet.Core.Recovery;

/// <summary>
/// 启动期配置快照。配置修改后必须重启才生效。
/// </summary>
public sealed class RecoveryOptionsProvider
{
    private SystemConfigDto? _current;

    /// <summary>
    /// 是否已加载
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _current) != null;

    /// <summary>
    /// 当前生效配置（未加载时返回默认值）
    /// </summary>
    public SystemConfigDto Current =>
        Volatile.Read(ref _current) ?? new SystemConfigDto();

    /// <summary>
    /// 加载快照（仅启动阶段调用一次）
    /// </summary>
    public void Initialize(SystemConfigDto config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Volatile.Write(ref _current, Clone(config));
    }

    /// <summary>
    /// 计算持久化配置与当前生效值是否存在差异
    /// </summary>
    public bool ComputeRestartRequired(SystemConfigDto persisted)
    {
        var current = Current;
        return persisted.ServiceName != current.ServiceName
            || persisted.Environment != current.Environment
            || persisted.ServiceDescription != current.ServiceDescription
            || persisted.LogRetentionDays != current.LogRetentionDays
            || persisted.LogMaxCount != current.LogMaxCount
            || persisted.LogCleanupIntervalHours != current.LogCleanupIntervalHours
            || persisted.ShutdownMode != current.ShutdownMode
            || persisted.ShutdownWaitTimeoutSeconds != current.ShutdownWaitTimeoutSeconds
            || persisted.DrainTimeoutSeconds != current.DrainTimeoutSeconds
            || persisted.RecoveryEnabled != current.RecoveryEnabled
            || persisted.CompensationDelaySeconds != current.CompensationDelaySeconds
            || persisted.HeartbeatIntervalSeconds != current.HeartbeatIntervalSeconds
            || persisted.LeaseDurationSeconds != current.LeaseDurationSeconds
            || persisted.SkipIfNextFireWithinSeconds != current.SkipIfNextFireWithinSeconds
            || persisted.CatchUpStrategy != current.CatchUpStrategy
            || persisted.MaxRecoveryAttempts != current.MaxRecoveryAttempts
            || persisted.DefaultRerunOnRecovery != current.DefaultRerunOnRecovery
            || persisted.InterruptedLogRetentionDays != current.InterruptedLogRetentionDays;
    }

    /// <summary>
    /// 校验配置合法性
    /// </summary>
    public static string? Validate(SystemConfigDto config)
    {
        if (config.HeartbeatIntervalSeconds is < 5 or > 300)
        {
            return "心跳间隔必须在 5 到 300 秒之间";
        }

        if (config.LeaseDurationSeconds < 10 || config.LeaseDurationSeconds > 3600)
        {
            return "租约时长必须在 10 到 3600 秒之间";
        }

        if (config.LeaseDurationSeconds < config.HeartbeatIntervalSeconds * 2)
        {
            return "租约时长必须不小于心跳间隔的 2 倍";
        }

        if (config.ShutdownWaitTimeoutSeconds is < 0 or > 3600)
        {
            return "停机等待超时必须在 0 到 3600 秒之间";
        }

        if (config.DrainTimeoutSeconds is < 0 or > 3600)
        {
            return "排水超时必须在 0 到 3600 秒之间";
        }

        if (config.CompensationDelaySeconds is < 0 or > 3600)
        {
            return "补偿延迟必须在 0 到 3600 秒之间";
        }

        if (config.SkipIfNextFireWithinSeconds is < 0 or > 86400)
        {
            return "临近触发跳过阈值必须在 0 到 86400 秒之间";
        }

        if (config.MaxRecoveryAttempts is < 1 or > 10)
        {
            return "最大补偿次数必须在 1 到 10 之间";
        }

        if (config.InterruptedLogRetentionDays is < 0 or > 3650)
        {
            return "中断日志保留天数必须在 0 到 3650 之间";
        }

        return null;
    }

    private static SystemConfigDto Clone(SystemConfigDto config)
    {
        var json = JsonSerializer.Serialize(config);
        return JsonSerializer.Deserialize<SystemConfigDto>(json) ?? new SystemConfigDto();
    }
}
