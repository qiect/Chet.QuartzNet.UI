namespace Chet.QuartzNet.Core.Configuration;

/// <summary>
/// 邮件通知配置选项
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// 是否启用邮件通知
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SMTP服务器地址
    /// </summary>
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP服务器端口
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// 发件人邮箱
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// 发件人显示名称
    /// </summary>
    public string SenderName { get; set; } = "Quartz.NET 调度器";

    /// <summary>
    /// 发件人邮箱密码或授权码
    /// </summary>
    public string SenderPassword { get; set; } = string.Empty;

    /// <summary>
    /// 收件人邮箱列表（用分号分隔）
    /// </summary>
    public string Recipients { get; set; } = string.Empty;

    /// <summary>
    /// 邮件主题前缀
    /// </summary>
    public string SubjectPrefix { get; set; } = "[Quartz.NET]";

    /// <summary>
    /// 作业失败时发送通知
    /// </summary>
    public bool NotifyOnFailure { get; set; } = true;

    /// <summary>
    /// 作业成功时发送通知
    /// </summary>
    public bool NotifyOnSuccess { get; set; } = false;

    /// <summary>
    /// 调度器异常时发送通知
    /// </summary>
    public bool NotifyOnSchedulerError { get; set; } = true;

    /// <summary>
    /// 获取收件人列表
    /// </summary>
    public List<string> GetRecipientList()
    {
        if (string.IsNullOrWhiteSpace(Recipients))
            return new List<string>();

        return Recipients.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(email => email.Trim())
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .ToList();
    }
}