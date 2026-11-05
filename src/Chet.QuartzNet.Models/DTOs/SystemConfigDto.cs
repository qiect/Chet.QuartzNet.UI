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
    /// 日志保留天数，<=0 表示不按天数清理，默认 30 天
    /// </summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// 日志最大保留条数，<=0 表示不限制，默认 0
    /// </summary>
    public int LogMaxCount { get; set; } = 0;

    /// <summary>
    /// 日志自动清理间隔（小时），默认 6 小时
    /// </summary>
    public int LogCleanupIntervalHours { get; set; } = 6;
}
