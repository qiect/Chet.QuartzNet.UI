using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Core.Migration;

/// <summary>
/// 迁移分页结果
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public sealed class MigrationPage<T>
{
    /// <summary>
    /// 当前偏移量
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// 总记录数（未知时为 0）
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// 当前页数据
    /// </summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}

/// <summary>
/// 迁移写入结果
/// </summary>
public sealed class WriteOutcome
{
    /// <summary>
    /// 插入数量
    /// </summary>
    public long Inserted { get; set; }

    /// <summary>
    /// 更新数量
    /// </summary>
    public long Updated { get; set; }

    /// <summary>
    /// 跳过数量
    /// </summary>
    public long Skipped { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// 错误样本
    /// </summary>
    public List<MigrationRowError> Errors { get; set; } = new();
}

/// <summary>
/// 迁移实体键辅助类，用于源/目标两端生成一致的业务键
/// </summary>
public static class MigrationKeys
{
    private const char Separator = '\u0001';

    /// <summary>
    /// 作业键（分组 + 名称）
    /// </summary>
    public static string Job(string jobGroup, string jobName) =>
        $"{jobGroup}{Separator}{jobName}";

    /// <summary>
    /// 作业键
    /// </summary>
    public static string Job(QuartzJobInfo job) => Job(job.JobGroup, job.JobName);

    /// <summary>
    /// 日志键
    /// </summary>
    public static string Log(Guid logId) => logId.ToString("D");

    /// <summary>
    /// 日志键
    /// </summary>
    public static string Log(QuartzJobLog log) => Log(log.LogId);

    /// <summary>
    /// 设置键
    /// </summary>
    public static string Setting(string key) => key;

    /// <summary>
    /// 设置键
    /// </summary>
    public static string Setting(QuartzSetting setting) => Setting(setting.Key);

    /// <summary>
    /// 通知键
    /// </summary>
    public static string Notification(Guid notificationId) => notificationId.ToString("D");

    /// <summary>
    /// 通知键
    /// </summary>
    public static string Notification(QuartzNotification notification) =>
        Notification(notification.NotificationId);

    /// <summary>
    /// 拆分作业键
    /// </summary>
    public static (string JobGroup, string JobName) SplitJob(string key)
    {
        var index = key.IndexOf(Separator);
        return index < 0
            ? (key, string.Empty)
            : (key.Substring(0, index), key.Substring(index + 1));
    }
}
