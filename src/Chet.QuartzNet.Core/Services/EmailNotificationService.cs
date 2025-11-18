using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.Core.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        EmailOptions emailOptions,
        ILogger<EmailNotificationService> logger)
    {
        _emailOptions = emailOptions ?? throw new ArgumentNullException(nameof(emailOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendJobExecutionNotificationAsync(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        // 根据成功/失败状态决定是否发送通知
        if (!_emailOptions.Enabled || (success && !_emailOptions.NotifyOnSuccess) || (!success && !_emailOptions.NotifyOnFailure))
            return;

        try
        {
            var subject = $"{_emailOptions.SubjectPrefix} 作业执行{(success ? "成功" : "失败")} - {jobName}";
            var content = GenerateJobExecutionEmailContent(jobName, jobGroup, success, message, duration, errorMessage);
            
            await SendEmailAsync(subject, content, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送作业执行通知邮件失败");
        }
    }

    public async Task SendSchedulerErrorNotificationAsync(
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled || !_emailOptions.NotifyOnSchedulerError)
            return;

        try
        {
            var subject = $"{_emailOptions.SubjectPrefix} 调度器异常";
            var content = GenerateSchedulerErrorEmailContent(exception);
            
            await SendEmailAsync(subject, content, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送调度器异常通知邮件失败");
        }
    }

    public async Task SendCustomNotificationAsync(
        string subject,
        string content,
        bool isHtml = false,
        CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
            return;

        try
        {
            var fullSubject = $"{_emailOptions.SubjectPrefix} {subject}";
            await SendEmailAsync(fullSubject, content, isHtml, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送自定义通知邮件失败");
        }
    }

    public async Task<bool> TestEmailConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
        {
            _logger.LogWarning("邮件通知功能未启用");
            return false;
        }

        try
        {
            _logger.LogInformation("开始测试邮件配置：SMTP服务器={SmtpServer}:{SmtpPort}, SSL={EnableSsl}, 发件人={SenderEmail}, 收件人={Recipients}",
                _emailOptions.SmtpServer, _emailOptions.SmtpPort, _emailOptions.EnableSsl, _emailOptions.SenderEmail,
                string.Join(", ", _emailOptions.GetRecipientList()));

            // 验证配置
            if (string.IsNullOrEmpty(_emailOptions.SmtpServer))
            {
                _logger.LogError("SMTP服务器地址未配置");
                return false;
            }

            if (string.IsNullOrEmpty(_emailOptions.SenderEmail))
            {
                _logger.LogError("发件人邮箱未配置");
                return false;
            }

            if (string.IsNullOrEmpty(_emailOptions.SenderPassword))
            {
                _logger.LogError("发件人密码未配置（请确认是否使用163邮箱授权码）");
                return false;
            }

            if (!_emailOptions.GetRecipientList().Any())
            {
                _logger.LogError("收件人邮箱未配置");
                return false;
            }

            var subject = $"{_emailOptions.SubjectPrefix} 邮件配置测试";
            var content = $"这是一封测试邮件，用于验证邮件配置是否正确。\n\n" +
                         $"配置信息：\n" +
                         $"SMTP服务器：{_emailOptions.SmtpServer}:{_emailOptions.SmtpPort}\n" +
                         $"SSL/TLS：{(_emailOptions.EnableSsl ? "启用" : "禁用")}\n" +
                         $"发件人：{_emailOptions.SenderEmail}\n" +
                         $"测试时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                         $"如果您收到此邮件，说明邮件配置正常。";
            
            await SendEmailAsync(subject, content, false, cancellationToken);
            
            _logger.LogInformation("邮件配置测试成功");
            return true;
        }
        catch (SmtpException ex) when (ex.Message.Contains("authentication failed") || ex.Message.Contains("需要认证"))
        {
            _logger.LogError(ex, "邮件配置测试失败：认证失败。对于163邮箱，请确认：\n" +
                "1. 使用授权码而不是邮箱密码\n" +
                "2. 授权码是否正确\n" +
                "3. 发件人邮箱是否已开启SMTP服务");
            return false;
        }
        catch (SmtpException ex) when (ex.Message.Contains("Syntax error, command unrecognized"))
        {
            _logger.LogError(ex, "邮件配置测试失败：SMTP连接错误。请确认：\n" +
                "1. 端口配置是否正确（587推荐）\n" +
                "2. SSL/TLS设置是否正确\n" +
                "3. 网络连接是否正常");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件配置测试失败：{ErrorMessage}", ex.Message);
            return false;
        }
    }

    private async Task SendEmailAsync(string subject, string content, bool isHtml, CancellationToken cancellationToken)
    {
        var recipients = _emailOptions.GetRecipientList();
        if (!recipients.Any())
        {
            _logger.LogWarning("没有配置收件人邮箱地址");
            return;
        }

        try
        {
            _logger.LogInformation("正在发送邮件：SMTP服务器={SmtpServer}:{SmtpPort}, SSL={EnableSsl}, 发件人={SenderEmail}, 收件人={Recipients}",
                _emailOptions.SmtpServer, _emailOptions.SmtpPort, _emailOptions.EnableSsl, _emailOptions.SenderEmail, 
                string.Join(", ", recipients));

            using var smtpClient = new SmtpClient();
            
            // 配置SMTP客户端
            smtpClient.Host = _emailOptions.SmtpServer;
            smtpClient.Port = _emailOptions.SmtpPort;
            smtpClient.EnableSsl = _emailOptions.EnableSsl;
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(_emailOptions.SenderEmail, _emailOptions.SenderPassword);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            
            // 设置超时时间
            smtpClient.Timeout = 30000; // 30秒

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailOptions.SenderEmail, _emailOptions.SenderName),
                Subject = subject,
                Body = content,
                IsBodyHtml = isHtml,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8
            };

            foreach (var recipient in recipients)
            {
                mailMessage.To.Add(recipient);
            }

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            
            _logger.LogInformation("邮件发送成功: {Subject}, 收件人: {Recipients}", 
                subject, string.Join(", ", recipients));
        }
        catch (SmtpException ex) when (ex.Message.Contains("Syntax error, command unrecognized"))
        {
            _logger.LogError(ex, "SMTP连接失败，可能是SSL/TLS配置问题。对于163邮箱，请确认：\n" +
                "1. 使用授权码而不是邮箱密码\n" +
                "2. 端口587 + EnableSsl=true（推荐）\n" +
                "3. 端口465 + EnableSsl=true（需要SSL）\n" +
                "4. 发件人邮箱已开启SMTP服务");
            throw new InvalidOperationException($"SMTP连接失败：{ex.Message}。请检查SSL/TLS配置和邮箱设置。", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败，请检查配置：SMTP服务器={SmtpServer}:{SmtpPort}, SSL={EnableSsl}, 发件人={SenderEmail}",
                _emailOptions.SmtpServer, _emailOptions.SmtpPort, _emailOptions.EnableSsl, _emailOptions.SenderEmail);
            throw;
        }
    }

    private string GenerateJobExecutionEmailContent(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage)
    {
        var status = success ? "成功" : "失败";
        var statusColor = success ? "#28a745" : "#dc3545";
        
        var errorRow = (success || string.IsNullOrEmpty(errorMessage)) ? "" : $@"
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'><strong>错误信息：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; color: #dc3545;'>{errorMessage}</td>
            </tr>";

        return $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px;'>
        <h2 style='color: {statusColor}; margin-bottom: 20px;'>作业执行{status}</h2>
        
        <table style='width: 100%; border-collapse: collapse;'>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>作业名称：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{jobName}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>作业分组：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{jobGroup}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>执行状态：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'><span style='color: {statusColor}; font-weight: bold;'>{status}</span></td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>执行时间：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>执行耗时：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{duration} 毫秒</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>消息：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{message}</td>
            </tr>
            {errorRow}
        </table>
        
        <div style='margin-top: 20px; padding: 10px; background-color: #e9ecef; border-radius: 3px; font-size: 12px; color: #6c757d;'>
            此邮件由 Quartz.NET 调度系统自动发送，请勿回复。
        </div>
    </div>
</div>";
    }

    private string GenerateSchedulerErrorEmailContent(Exception exception)
    {
        return $@"
<div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px;'>
        <h2 style='color: #dc3545; margin-bottom: 20px;'>调度器异常</h2>
        
        <table style='width: 100%; border-collapse: collapse;'>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>异常时间：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>异常类型：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>{exception.GetType().Name}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px;'><strong>异常消息：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; color: #dc3545;'>{exception.Message}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6; width: 60px; vertical-align: top;'><strong>堆栈跟踪：</strong></td>
                <td style='padding: 10px; border-bottom: 1px solid #dee2e6;'>
                    <pre style='background-color: #f1f3f4; padding: 10px; border-radius: 3px; font-size: 12px; overflow-x: auto;'>{exception.StackTrace}</pre>
                </td>
            </tr>
        </table>
        
        <div style='margin-top: 20px; padding: 10px; background-color: #e9ecef; border-radius: 3px; font-size: 12px; color: #6c757d;'>
            此邮件由 Quartz.NET 调度系统自动发送，请勿回复。
        </div>
    </div>
</div>";
    }
}