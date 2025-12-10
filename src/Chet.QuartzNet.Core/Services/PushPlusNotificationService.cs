using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// PushPlus通知服务实现
/// </summary>
public class PushPlusNotificationService : INotificationService
{
    private const string PushPlusApiUrl = "https://www.pushplus.plus/send";
    private const string PushPlusConfigKey = "PushPlusConfig";

    private readonly IJobStorage _jobStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PushPlusNotificationService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jobStorage">作业存储服务</param>
    /// <param name="httpClientFactory">HTTP客户端工厂</param>
    /// <param name="logger">日志记录器</param>
    public PushPlusNotificationService(
        IJobStorage jobStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<PushPlusNotificationService> logger)
    {
        _jobStorage = jobStorage;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 发送作业执行通知
    /// </summary>
    public async Task SendJobExecutionNotificationAsync(
        string jobName, string jobGroup, bool success, string message,
        long duration, string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取配置
            var config = await GetPushPlusConfigAsync(cancellationToken);
            if (!config.Enable)
            {
                _logger.LogInformation("PushPlus通知已禁用，跳过发送作业执行通知");
                return;
            }

            // 检查通知策略
            if (success && !config.Strategy.NotifyOnJobSuccess)
            {
                _logger.LogInformation("作业执行成功，但根据策略不发送通知");
                return;
            }

            if (!success && !config.Strategy.NotifyOnJobFailure)
            {
                _logger.LogInformation("作业执行失败，但根据策略不发送通知");
                return;
            }

            // 创建通知记录
            var notification = new QuartzNotification
            {
                Title = $"作业执行{(success ? "成功" : "失败")}: {jobName}",
                Content = GenerateJobExecutionContent(config.Template, jobName, jobGroup, success, message, duration, errorMessage),
                Status = NotificationStatus.Pending,
                TriggeredBy = $"{jobGroup}.{jobName}"
            };

            // 保存通知记录
            await _jobStorage.AddNotificationAsync(notification, cancellationToken);

            // 发送通知
            var sendResult = await SendPushPlusNotificationAsync(config, notification.Title, notification.Content, cancellationToken);

            // 更新通知记录
            notification.Status = sendResult ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SendTime = DateTime.Now;
            await _jobStorage.UpdateNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送作业执行通知失败");
        }
    }

    /// <summary>
    /// 发送调度器异常通知
    /// </summary>
    public async Task SendSchedulerErrorNotificationAsync(
        Exception exception, CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取配置
            var config = await GetPushPlusConfigAsync(cancellationToken);
            if (!config.Enable)
            {
                _logger.LogInformation("PushPlus通知已禁用，跳过发送调度器异常通知");
                return;
            }

            // 检查通知策略
            if (!config.Strategy.NotifyOnSchedulerError)
            {
                _logger.LogInformation("调度器异常，但根据策略不发送通知");
                return;
            }

            // 创建通知记录
            var notification = new QuartzNotification
            {
                Title = "调度器异常",
                Content = GenerateSchedulerErrorContent(config.Template, exception),
                Status = NotificationStatus.Pending,
                TriggeredBy = "Scheduler"
            };

            // 保存通知记录
            await _jobStorage.AddNotificationAsync(notification, cancellationToken);

            // 发送通知
            var sendResult = await SendPushPlusNotificationAsync(config, notification.Title, notification.Content, cancellationToken);

            // 更新通知记录
            notification.Status = sendResult ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SendTime = DateTime.Now;
            await _jobStorage.UpdateNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送调度器异常通知失败");
        }
    }

    /// <summary>
    /// 发送测试通知
    /// </summary>
    public async Task<bool> SendTestNotificationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取配置
            var config = await GetPushPlusConfigAsync(cancellationToken);
            if (!config.Enable)
            {
                _logger.LogInformation("PushPlus通知已禁用，测试通知发送失败");
                return false;
            }

            // 发送测试通知
            var title = "测试通知";
            var content = "这是一条测试通知，用于验证PushPlus配置是否正确。";
            return await SendPushPlusNotificationAsync(config, title, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送测试通知失败");
            return false;
        }
    }

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    public async Task<PushPlusConfigDto> GetPushPlusConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 从存储中获取配置
            var setting = await _jobStorage.GetSettingAsync(PushPlusConfigKey, cancellationToken);
            if (setting == null || string.IsNullOrEmpty(setting.Value))
            {
                // 返回默认配置
                return new PushPlusConfigDto
                {
                    Enable = false,
                    Channel = "wechat",
                    Template = "html",
                    Strategy = new NotificationStrategyDto
                    {
                        NotifyOnJobSuccess = false,
                        NotifyOnJobFailure = true,
                        NotifyOnSchedulerError = true
                    }
                };
            }

            // 解析配置
            var config = JsonSerializer.Deserialize<PushPlusConfigDto>(setting.Value, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return config ?? new PushPlusConfigDto
            {
                Enable = false,
                Channel = "wechat",
                Template = "html",
                Strategy = new NotificationStrategyDto
                {
                    NotifyOnJobSuccess = false,
                    NotifyOnJobFailure = true,
                    NotifyOnSchedulerError = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取PushPlus配置失败");
            return new PushPlusConfigDto
            {
                Enable = false,
                Channel = "wechat",
                Template = "html",
                Strategy = new NotificationStrategyDto
                {
                    NotifyOnJobSuccess = false,
                    NotifyOnJobFailure = true,
                    NotifyOnSchedulerError = true
                }
            };
        }
    }

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    public async Task<bool> SavePushPlusConfigAsync(PushPlusConfigDto config, CancellationToken cancellationToken = default)
    {
        try
        {
            // 序列化配置
            var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 保存配置
            var setting = new QuartzSetting
            {
                Key = PushPlusConfigKey,
                Value = configJson,
                Description = "PushPlus通知配置",
                Enabled = config.Enable
            };

            return await _jobStorage.SaveSettingAsync(setting, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存PushPlus配置失败");
            return false;
        }
    }

    /// <summary>
    /// 发送PushPlus通知
    /// </summary>
    private async Task<bool> SendPushPlusNotificationAsync(
        PushPlusConfigDto config, string title, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            // 创建HttpClient时配置显式禁用代理，避免系统代理导致的连接问题
            var handler = new HttpClientHandler
            {
                // 显式禁用代理，解决127.0.0.1:7890连接拒绝问题
                UseProxy = false,
                // 禁用SSL验证（仅开发环境使用，生产环境建议启用）
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Chet.QuartzNet.UI");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // 构建请求体
            var requestBody = new
            {
                token = config.Token,
                title = title,
                content = content,
                template = config.Template,
                channel = config.Channel,
                topic = string.IsNullOrEmpty(config.Topic) ? null : config.Topic
            };

            // 发送请求
            var response = await httpClient.PostAsJsonAsync(PushPlusApiUrl, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            // 解析响应
            var responseBody = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            if (responseBody == null || !responseBody.TryGetValue("code", out var codeObj))
            {
                _logger.LogError("PushPlus API响应格式错误");
                return false;
            }

            var code = Convert.ToString(codeObj);
            if (code != "200")
            {
                var msg = responseBody.TryGetValue("msg", out var msgObj) ? Convert.ToString(msgObj) : "未知错误";
                _logger.LogError("PushPlus API调用失败: {Msg}", msg);
                return false;
            }

            _logger.LogInformation("PushPlus通知发送成功");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送PushPlus通知失败");
            return false;
        }
    }

    /// <summary>
    /// 生成作业执行通知内容
    /// </summary>
    private string GenerateJobExecutionContent(
        string template, string jobName, string jobGroup, bool success, string message,
        long duration, string? errorMessage = null)
    {
        var status = success ? "成功" : "失败";
        var executionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        switch (template.ToLower())
        {
            case "txt":
                return GenerateJobExecutionTextContent(jobName, jobGroup, success, message, duration, errorMessage, executionTime);
            case "markdown":
                return GenerateJobExecutionMarkdownContent(jobName, jobGroup, success, message, duration, errorMessage, executionTime);
            case "html":
            default:
                return GenerateJobExecutionHtmlContent(jobName, jobGroup, success, message, duration, errorMessage, executionTime);
        }
    }

    /// <summary>
    /// 生成作业执行HTML通知内容
    /// </summary>
    private string GenerateJobExecutionHtmlContent(string jobName, string jobGroup, bool success, string message, long duration, string? errorMessage, string executionTime)
    {
        var status = success ? "成功" : "失败";
        var statusBadge = success ? "✅" : "❌";
        var statusColor = success ? "#2da44e" : "#cf222e";
        var headerBg = success ? "#f0fff4" : "#fff5f5";

        // 使用简单的字符串拼接，避免复杂的转义问题
        return $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">" +
               $"<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;\">" +
               $"<div style=\"background-color: {headerBg}; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;\">" +
               $"<h1 style=\"margin: 0; font-size: 20px; font-weight: 600; color: {statusColor};\">{statusBadge} 作业执行{status}: {jobName}</h1>" +
               $"</div>" +
               $"<div style=\"padding: 20px;\">" +
               $"<div style=\"margin-bottom: 24px;\">" +
               $"<h2 style=\"margin: 0 0 16px 0; font-size: 16px; font-weight: 600;\">作业信息</h2>" +
               $"<table style=\"width: 100%; border-collapse: collapse; font-size: 14px;\">" +
               $"<tbody>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">作业名称</td>" +
               $"<td style=\"padding: 10px 16px;\">{jobName}</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">作业分组</td>" +
               $"<td style=\"padding: 10px 16px;\">{jobGroup}</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行状态</td>" +
               $"<td style=\"padding: 10px 16px;\"><span style=\"color: {statusColor}; font-weight: 600;\">{statusBadge} {status}</span></td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行时间</td>" +
               $"<td style=\"padding: 10px 16px;\">{executionTime}</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行耗时</td>" +
               $"<td style=\"padding: 10px 16px;\">{duration} 毫秒</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">消息</td>" +
               $"<td style=\"padding: 10px 16px;\">{message}</td>" +
               $"</tr>" +
               (!success && !string.IsNullOrEmpty(errorMessage) ?
                $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
                $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">错误信息</td>" +
                $"<td style=\"padding: 10px 16px; color: {statusColor}; word-break: break-word;\">{errorMessage}</td>" +
                $"</tr>" : "") +
               $"</tbody>" +
               $"</table>" +
               $"</div>" +
               $"</div>" +
               $"<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 16px 20px; font-size: 12px; color: #656d76;\">" +
               $"<p style=\"margin: 0;\">此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。</p>" +
               $"</div>" +
               $"</div>" +
               $"</div>";
    }

    /// <summary>
    /// 生成作业执行纯文本通知内容
    /// </summary>
    private string GenerateJobExecutionTextContent(string jobName, string jobGroup, bool success, string message, long duration, string? errorMessage, string executionTime)
    {
        var status = success ? "成功" : "失败";

        var content = $@"作业执行{status}: {jobName}

作业名称: {jobName}
作业分组: {jobGroup}
执行状态: {status}
执行时间: {executionTime}
执行耗时: {duration} 毫秒
消息: {message}";

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            content += $@"
错误信息: {errorMessage}";
        }

        content += $@"

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。";

        return content;
    }

    /// <summary>
    /// 生成作业执行Markdown通知内容
    /// </summary>
    private string GenerateJobExecutionMarkdownContent(string jobName, string jobGroup, bool success, string message, long duration, string? errorMessage, string executionTime)
    {
        var status = success ? "成功" : "失败";
        var statusBadge = success ? "✅" : "❌";

        var content = $@"# {statusBadge} 作业执行{status}: {jobName}

## 作业信息

| 项目 | 内容 |
|------|------|
| 作业名称 | {jobName} |
| 作业分组 | {jobGroup} |
| 执行状态 | {statusBadge} {status} |
| 执行时间 | {executionTime} |
| 执行耗时 | {duration} 毫秒 |
| 消息 | {message} |";

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            content += $@"
| 错误信息 | {errorMessage} |";
        }

        content += $@"

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。";

        return content;
    }

    /// <summary>
    /// 生成调度器异常通知内容
    /// </summary>
    private string GenerateSchedulerErrorContent(string template, Exception exception)
    {
        var executionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        switch (template.ToLower())
        {
            case "txt":
                return GenerateSchedulerErrorTextContent(exception, executionTime);
            case "markdown":
                return GenerateSchedulerErrorMarkdownContent(exception, executionTime);
            case "html":
            default:
                return GenerateSchedulerErrorHtmlContent(exception, executionTime);
        }
    }

    /// <summary>
    /// 生成调度器异常HTML通知内容
    /// </summary>
    private string GenerateSchedulerErrorHtmlContent(Exception exception, string executionTime)
    {
        var statusColor = "#cf222e";

        // 使用简单的字符串拼接，避免复杂的转义问题
        return $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">" +
               $"<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;\">" +
               $"<div style=\"background-color: #fff5f5; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;\">" +
               $"<h1 style=\"margin: 0; font-size: 20px; font-weight: 600; color: {statusColor};\">⚠️ 调度器异常</h1>" +
               $"</div>" +
               $"<div style=\"padding: 20px;\">" +
               $"<div style=\"margin-bottom: 24px;\">" +
               $"<h2 style=\"margin: 0 0 16px 0; font-size: 16px; font-weight: 600;\">异常信息</h2>" +
               $"<table style=\"width: 100%; border-collapse: collapse; font-size: 14px;\">" +
               $"<tbody>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常时间</td>" +
               $"<td style=\"padding: 10px 16px;\">{executionTime}</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常类型</td>" +
               $"<td style=\"padding: 10px 16px;\">{exception.GetType().Name}</td>" +
               $"</tr>" +
               $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常消息</td>" +
               $"<td style=\"padding: 10px 16px; color: {statusColor}; word-break: break-word;\">{exception.Message}</td>" +
               $"</tr>" +
               $"<tr>" +
               $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa; vertical-align: top;\">堆栈跟踪</td>" +
               $"<td style=\"padding: 10px 16px;\">" +
               $"<pre style=\"background-color: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px; padding: 16px; font-size: 12px; line-height: 1.5; font-family: ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, Liberation Mono, monospace; overflow-x: auto; margin: 0;\">{exception.StackTrace}</pre>" +
               $"</td>" +
               $"</tr>" +
               $"</tbody>" +
               $"</table>" +
               $"</div>" +
               $"</div>" +
               $"<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 16px 20px; font-size: 12px; color: #656d76;\">" +
               $"<p style=\"margin: 0;\">此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。</p>" +
               $"</div>" +
               $"</div>" +
               $"</div>";
    }

    /// <summary>
    /// 生成调度器异常纯文本通知内容
    /// </summary>
    private string GenerateSchedulerErrorTextContent(Exception exception, string executionTime)
    {
        return $@"调度器异常

异常时间: {executionTime}
异常类型: {exception.GetType().Name}
异常消息: {exception.Message}
堆栈跟踪: {exception.StackTrace}

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。";
    }

    /// <summary>
    /// 生成调度器异常Markdown通知内容
    /// </summary>
    private string GenerateSchedulerErrorMarkdownContent(Exception exception, string executionTime)
    {
        return $@"# ⚠️ 调度器异常

## 异常信息

| 项目 | 内容 |
|------|------|
| 异常时间 | {executionTime} |
| 异常类型 | {exception.GetType().Name} |
| 异常消息 | {exception.Message} |
| 堆栈跟踪 | 
```
{exception.StackTrace}
``` |

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送，请勿回复。";
    }
}