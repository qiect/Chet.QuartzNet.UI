using Chet.QuartzNet.Models.DTOs;

namespace Chet.QuartzNet.Core.Interfaces;

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 发送作业执行通知
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="success">是否成功</param>
    /// <param name="message">消息</param>
    /// <param name="duration">执行耗时（毫秒）</param>
    /// <param name="errorMessage">错误信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendJobExecutionNotificationAsync(
        string jobName, string jobGroup, bool success, string message,
        long duration, string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送调度器异常通知
    /// </summary>
    /// <param name="exception">异常信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendSchedulerErrorNotificationAsync(
        Exception exception, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送测试通知
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送成功返回true，失败返回false</returns>
    Task<bool> SendTestNotificationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PushPlus配置</returns>
    Task<PushPlusConfigDto> GetPushPlusConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    /// <param name="config">PushPlus配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存成功返回true，失败返回false</returns>
    Task<bool> SavePushPlusConfigAsync(PushPlusConfigDto config, CancellationToken cancellationToken = default);
}