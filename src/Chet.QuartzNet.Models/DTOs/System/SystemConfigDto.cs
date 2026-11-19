using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 系统配置DTO
/// 用于在多实例环境中区分不同服务，配置显示在分析页顶部
/// </summary>
public class SystemConfigDto
{
    /// <summary>
    /// 服务名称
    /// 显示在分析页顶部横幅及浏览器标题
    /// </summary>
    public string ServiceName { get; set; } = "QuartzNet 调度服务";

    /// <summary>
    /// 环境标识
    /// 可选值：DEV / TEST / UAT / PROD
    /// </summary>
    public string Environment { get; set; } = "DEV";

    /// <summary>
    /// 服务描述
    /// 显示在服务名称下方的副标题
    /// </summary>
    public string ServiceDescription { get; set; } = string.Empty;

    /// <summary>
    /// 日志保留天数，&lt;=0 表示不按天数清理，默认 30 天
    /// </summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// 日志最大保留条数，&lt;=0 表示不限制，默认 0
    /// </summary>
    public int LogMaxCount { get; set; } = 0;

    /// <summary>
    /// 日志自动清理间隔（小时），默认 6 小时
    /// </summary>
    public int LogCleanupIntervalHours { get; set; } = 6;

    #region 停机策略（启动期配置）

    /// <summary>
    /// 停机模式：Immediate=立即停止；Drain=停止接收新任务并等待；Wait=等待全部完成
    /// </summary>
    public ShutdownMode ShutdownMode { get; set; } = ShutdownMode.Drain;

    /// <summary>
    /// 兼容保留：「等待完成」已并入「排水后停止」，不再单独使用该超时。
    /// 仅用于兼容历史配置，停机等待统一使用 <see cref="DrainTimeoutSeconds"/>。
    /// </summary>
    public int ShutdownWaitTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 排水后停止模式下等待执行中任务完成的最长秒数
    /// </summary>
    public int DrainTimeoutSeconds { get; set; } = 60;

    #endregion

    #region 中断恢复（启动期配置）

    /// <summary>
    /// 是否启用启动中断补偿
    /// </summary>
    public bool RecoveryEnabled { get; set; } = true;

    /// <summary>
    /// 启动后延迟多少秒开始补偿，给依赖服务留启动时间
    /// </summary>
    public int CompensationDelaySeconds { get; set; } = 30;

    /// <summary>
    /// 运行中任务写心跳的间隔（秒）
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// 租约时长（秒）：超过该时长未续期即判定中断
    /// </summary>
    public int LeaseDurationSeconds { get; set; } = 60;

    /// <summary>
    /// 距下次触发不足该秒数时跳过补偿；0 表示不跳过
    /// </summary>
    public int SkipIfNextFireWithinSeconds { get; set; } = 120;

    /// <summary>
    /// 补偿策略：None=只标记；Latest=每个作业只补最近一次；All=全部补
    /// </summary>
    public CatchUpStrategy CatchUpStrategy { get; set; } = CatchUpStrategy.Latest;

    /// <summary>
    /// 同一执行链最大补偿次数，防止启动即崩导致无限重跑
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// 作业未显式配置时，中断后是否自动重跑
    /// </summary>
    public bool DefaultRerunOnRecovery { get; set; } = true;

    /// <summary>
    /// 中断/补偿日志保留天数，&lt;=0 表示跟随普通日志清理
    /// </summary>
    public int InterruptedLogRetentionDays { get; set; } = 30;

    #endregion
}
