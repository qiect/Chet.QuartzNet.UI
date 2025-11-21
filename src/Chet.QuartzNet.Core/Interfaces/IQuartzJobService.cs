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
    Task<ApiResponseDto<bool>> AddJobAsync(QuartzJobDto jobDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新作业
    /// </summary>
    /// <param name="jobDto">作业信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> UpdateJobAsync(QuartzJobDto jobDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> DeleteJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> PauseJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> ResumeJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 立即执行作业
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> TriggerJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业列表</returns>
    Task<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>> GetJobsAsync(QuartzJobQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业详情
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业详情</returns>
    Task<ApiResponseDto<QuartzJobResponseDto>> GetJobDetailAsync(string jobName, string jobGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取作业日志列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>作业日志列表</returns>
    Task<ApiResponseDto<PagedResponseDto<QuartzJobLog>>> GetJobLogsAsync(QuartzJobLogQueryDto queryDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调度器状态</returns>
    Task<ApiResponseDto<SchedulerStatusDto>> GetSchedulerStatusAsync(CancellationToken cancellationToken = default);

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
    Task<ApiResponseDto<bool>> ShutdownSchedulerAsync(CancellationToken cancellationToken = default);

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
    Task<ApiResponseDto<List<string>>> GetJobClassesAsync(CancellationToken cancellationToken = default);

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
    ApiResponseDto<List<DateTime>> GetNextRunTimes(string cronExpression, int count = 5);

    /// <summary>
    /// 更新作业执行时间
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="trigger">触发器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<ApiResponseDto<bool>> UpdateJobExecutionTimesAsync(string jobName, string jobGroup, Quartz.ITrigger trigger, CancellationToken cancellationToken = default);
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