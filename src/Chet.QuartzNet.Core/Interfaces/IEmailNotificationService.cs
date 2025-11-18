namespace Chet.QuartzNet.Core.Interfaces;

/// <summary>
/// 邮件通知服务接口
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// 发送作业执行结果通知
    /// </summary>
    /// <param name="jobName">作业名称</param>
    /// <param name="jobGroup">作业分组</param>
    /// <param name="success">是否成功</param>
    /// <param name="message">消息</param>
    /// <param name="duration">执行耗时（毫秒）</param>
    /// <param name="errorMessage">错误信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendJobExecutionNotificationAsync(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送调度器异常通知
    /// </summary>
    /// <param name="exception">异常信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendSchedulerErrorNotificationAsync(
        Exception exception,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送自定义通知
    /// </summary>
    /// <param name="subject">主题</param>
    /// <param name="content">内容</param>
    /// <param name="isHtml">是否为HTML格式</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendCustomNotificationAsync(
        string subject,
        string content,
        bool isHtml = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试邮件配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> TestEmailConfigurationAsync(CancellationToken cancellationToken = default);
}