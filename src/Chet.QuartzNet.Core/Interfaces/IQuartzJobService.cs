using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Core.Interfaces;

/// <summary>
/// Quartz作业服务接口
/// </summary>
public interface IQuartzJobService
{
    /// <summary>
    /// 添加作业
    /// </summary>
    /// <param name="jobDto">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> AddJobAsync(
        QuartzJobDto jobDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 更新作业
    /// </summary>
    /// <param name="jobDto">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> UpdateJobAsync(
        QuartzJobDto jobDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 删除作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> DeleteJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 批量删除作业
    /// </summary>
    /// <param name="jobs">作业列表（包含jobName和jobGroup）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> BatchDeleteJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 暂停作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> PauseJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 恢复作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ResumeJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 批量暂停作业
    /// </summary>
    /// <param name="jobs">作业列表（包含jobName和jobGroup）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> BatchPauseJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 批量恢复作业
    /// </summary>
    /// <param name="jobs">作业列表（包含jobName和jobGroup）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> BatchResumeJobsAsync(
        List<(string JobName, string JobGroup)> jobs,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 批量启用/禁用作业
    /// </summary>
    /// <param name="jobs">作业列表（包含jobName和jobGroup）</param>
    /// <param name="enable">true=启用，false=禁用</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> BatchSetEnabledAsync(
        List<(string JobName, string JobGroup)> jobs,
        bool enable,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 立即执行作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> TriggerJobAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 带执行上下文触发作业（手动/补偿共用），执行级标记只写入触发器，不写入 JobDetail
    /// </summary>
    Task<ApiResponseDto<bool>> TriggerJobWithContextAsync(
        string jobName,
        string jobGroup,
        TriggerType triggerType,
        Guid? logId = null,
        Guid? recoverySourceLogId = null,
        int recoveryAttempt = 1,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取作业列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业列表</returns>
    Task<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>> GetJobsAsync(
        QuartzJobQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取作业详情
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业详情</returns>
    Task<ApiResponseDto<QuartzJobResponseDto>> GetJobDetailAsync(
        string jobName,
        string jobGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取作业日志列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业日志列表</returns>
    Task<ApiResponseDto<PagedResponseDto<QuartzJobLog>>> GetJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取单条日志
    /// </summary>
    /// <param name="logId">日志ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志详情</returns>
    Task<ApiResponseDto<QuartzJobLog>> GetJobLogAsync(
        Guid logId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度器状态</returns>
    Task<ApiResponseDto<SchedulerStatusDto>> GetSchedulerStatusAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 启动调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> StartSchedulerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止调度器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ShutdownSchedulerAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 排水：停止接收新触发，等待执行中任务收尾后自动停止
    /// </summary>
    /// <param name="timeoutSeconds">覆盖默认排水超时（秒），null 使用配置值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> DrainSchedulerAsync(
        int? timeoutSeconds = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 取消排水并恢复调度
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ResumeSchedulerAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取本次启动的中断恢复汇总
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>恢复汇总</returns>
    Task<ApiResponseDto<RecoverySummaryDto>> GetRecoverySummaryAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 手动重新执行一条中断日志
    /// </summary>
    /// <param name="logId">中断日志ID</param>
    /// <param name="triggeredBy">操作人</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> RerunInterruptedLogAsync(
        Guid logId,
        string? triggeredBy = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清除所有作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ClearAllJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实现了IJob接口的类名列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业类名列表</returns>
    Task<ApiResponseDto<List<string>>> GetJobClassesAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 验证Cron表达式
    /// </summary>
    /// <param name="cronExpression">Cron表达式</param>
    /// <returns>验证结果</returns>
    ApiResponseDto<bool> ValidateCronExpression(string cronExpression);

    /// <summary>
    /// 根据Cron表达式获取未来N次执行时间
    /// </summary>
    /// <param name="cronExpression">Cron表达式</param>
    /// <param name="count">获取次数</param>
    /// <returns>执行时间列表</returns>
    ApiResponseDto<List<DateTimeOffset>> GetNextRunTimes(string cronExpression, int count = 5);

    /// <summary>
    /// 更新作业执行时间
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="trigger">触发器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> UpdateJobExecutionTimesAsync(
        string jobName,
        string jobGroup,
        Quartz.ITrigger trigger,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清空作业日志
    /// </summary>
    /// <param name="queryDto">查询条件，用于指定清空哪些日志</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ClearJobLogsAsync(
        QuartzJobLogQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取统计分析概览聚合数据（合并作业统计+状态分布+执行趋势+热力图）
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计分析概览聚合数据</returns>
    Task<ApiResponseDto<AnalyticsOverviewDto>> GetAnalyticsOverviewAsync(
        StatsQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取作业性能分析聚合数据（合并健康概览+耗时排行）
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="topCount">耗时排行取前N条</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业性能分析聚合数据</returns>
    Task<ApiResponseDto<AnalyticsJobPerformanceDto>> GetAnalyticsJobPerformanceAsync(
        StatsQueryDto queryDto,
        int topCount = 10,
        CancellationToken cancellationToken = default
    );

    #region 通知管理

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PushPlus配置</returns>
    Task<ApiResponseDto<PushPlusConfigDto>> GetPushPlusConfigAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    /// <param name="config">PushPlus配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    Task<ApiResponseDto<bool>> SavePushPlusConfigAsync(
        PushPlusConfigDto config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 发送测试通知
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    Task<ApiResponseDto<bool>> SendTestNotificationAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取通知消息列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息列表</returns>
    Task<ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>> GetNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取通知消息详情
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息详情</returns>
    Task<ApiResponseDto<QuartzNotificationDto>> GetNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    Task<ApiResponseDto<bool>> DeleteNotificationAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清空通知消息
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空结果</returns>
    Task<ApiResponseDto<bool>> ClearNotificationsAsync(
        NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    );

    #endregion

    #region 系统配置

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>系统配置</returns>
    Task<ApiResponseDto<SystemConfigResponseDto>> GetSystemConfigAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 保存系统配置
    /// </summary>
    /// <param name="config">系统配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    Task<ApiResponseDto<bool>> SaveSystemConfigAsync(
        SystemConfigDto config,
        CancellationToken cancellationToken = default
    );

    #endregion
}

/// <summary>
/// 调度器状态DTO
/// </summary>
public class SchedulerStatusDto
{
    /// <summary>
    /// 调度器名称
    /// </summary>
    public string SchedulerName { get; set; } = string.Empty;

    /// <summary>
    /// 调度器实例ID
    /// </summary>
    public string SchedulerInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 当前进程运行实例标识（机器名:进程号:启动短标识），用于区分多进程部署下的实例
    /// </summary>
    public string RuntimeInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 是否启动
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// 是否关闭
    /// </summary>
    public bool IsShutdown { get; set; }

    /// <summary>
    /// 是否处于待机模式
    /// </summary>
    public bool InStandbyMode { get; set; }

    /// <summary>
    /// 运行状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 作业数量
    /// </summary>
    public int JobCount { get; set; }

    /// <summary>
    /// 正在执行的作业数量
    /// </summary>
    public int ExecutingJobCount { get; set; }

    /// <summary>
    /// 是否正在排水
    /// </summary>
    public bool IsDraining { get; set; }

    /// <summary>
    /// 排水开始时间
    /// </summary>
    public DateTime? DrainStartedAt { get; set; }

    /// <summary>
    /// 排水截止时间
    /// </summary>
    public DateTime? DrainDeadline { get; set; }

    /// <summary>
    /// 排水仍需等待的执行中任务数
    /// </summary>
    public int RemainingExecutingCount { get; set; }

    /// <summary>
    /// 本实例恢复队列中尚未完成的补偿数
    /// </summary>
    public int PendingCompensationCount { get; set; }

    /// <summary>
    /// 线程池大小
    /// </summary>
    public int ThreadPoolSize { get; set; }

    /// <summary>
    /// 版本信息
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 运行时长（毫秒）
    /// </summary>
    public long? RunningTime { get; set; }
}