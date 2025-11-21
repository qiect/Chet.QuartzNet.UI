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
}