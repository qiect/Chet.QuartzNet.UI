using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Core.Interfaces;

/// <summary>
/// 作业存储接口
/// </summary>
public interface IJobStorage
{
    /// <summary>
    /// 添加作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> AddJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新作业
    /// </summary>
    /// <param name="jobInfo">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> UpdateJobAsync(QuartzJobInfo jobInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> DeleteJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业信息</returns>
    Task<QuartzJobInfo?> GetJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业列表</returns>
    Task<PagedResponseDto<QuartzJobInfo>> GetJobsAsync(QuartzJobQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有作业
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业列表</returns>
    Task<List<QuartzJobInfo>> GetAllJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新作业状态
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="status">状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> UpdateJobStatusAsync(string jobName, string jobGroup, JobStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加作业日志
    /// </summary>
    /// <param name="jobLog">作业日志</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> AddJobLogAsync(QuartzJobLog jobLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业日志列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业日志列表</returns>
    Task<PagedResponseDto<QuartzJobLog>> GetJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除过期日志
    /// </summary>
    /// <param name="daysToKeep">保留天数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清除的日志数量</returns>
    Task<int> ClearExpiredLogsAsync(int daysToKeep, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空作业日志
    /// </summary>
    /// <param name="queryDto">查询条件，用于指定清空哪些日志</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> ClearJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 初始化存储
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查存储是否已初始化
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否已初始化</returns>
    Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业统计数据
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业统计数据</returns>
    Task<JobStatsDto> GetJobStatsAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业状态分布数据
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业状态分布数据</returns>
    Task<List<JobStatusDistributionDto>> GetJobStatusDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业执行趋势数据
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业执行趋势数据</returns>
    Task<List<JobExecutionTrendDto>> GetJobExecutionTrendAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业类型分布数据
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业类型分布数据</returns>
    Task<List<JobTypeDistributionDto>> GetJobTypeDistributionAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业执行耗时数据
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业执行耗时数据</returns>
    Task<List<JobExecutionTimeDto>> GetJobExecutionTimeAsync(StatsQueryDto queryDto, CancellationToken cancellationToken = default);

    #region 通知管理

    /// <summary>
    /// 保存设置
    /// </summary>
    /// <param name="setting">设置信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存成功返回true，失败返回false</returns>
    Task<bool> SaveSettingAsync(QuartzSetting setting, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取设置
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置信息，不存在返回null</returns>
    Task<QuartzSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有设置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>设置列表</returns>
    Task<List<QuartzSetting>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加通知消息
    /// </summary>
    /// <param name="notification">通知消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加成功返回true，失败返回false</returns>
    Task<bool> AddNotificationAsync(QuartzNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新通知消息
    /// </summary>
    /// <param name="notification">通知消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新成功返回true，失败返回false</returns>
    Task<bool> UpdateNotificationAsync(QuartzNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息，不存在返回null</returns>
    Task<QuartzNotification?> GetNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取通知消息列表，支持分页、过滤和排序
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页通知消息列表</returns>
    Task<PagedResponseDto<QuartzNotification>> GetNotificationsAsync(NotificationQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除成功返回true，失败返回false</returns>
    Task<bool> DeleteNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据查询条件清空通知消息
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空成功返回true，失败返回false</returns>
    Task<bool> ClearNotificationsAsync(NotificationQueryDto queryDto, CancellationToken cancellationToken = default);

    #endregion
}