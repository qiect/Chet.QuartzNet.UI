using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.Core.Services;

/// <summary>
/// PushPlus通知服务实现
/// </summary>
public class PushPlusNotificationService : INotificationService
{
    private const string PushPlusApiUrl = "https://www.pushplus.plus/send";
    private const string PushPlusConfigKey = "PushPlusConfig";

    /// <summary>
    /// PushPlus 专用命名 HttpClient：由 IHttpClientFactory 统一创建并轮换底层 Handler，
    /// 避免每次发送都新建连接导致 TIME_WAIT 端口耗尽。超时/代理/SSL 策略在注册处配置。
    /// </summary>
    public const string HttpClientName = "PushPlus";

    /// <summary>
    /// JSON 模板序列化选项：缩进输出、中文不转义、忽略空字段
    /// </summary>
    private static readonly JsonSerializerOptions NotificationJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IJobStorage _jobStorage;
    private readonly HttpClient _httpClient;
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
        ILogger<PushPlusNotificationService> logger
    )
    {
        _jobStorage = jobStorage;
        _logger = logger;

        // 从工厂获取命名客户端：Handler 由工厂池化并定期轮换，
        // 不能 using 释放，否则会破坏工厂对 Handler 生命周期的管理
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
    }

    /// <summary>
    /// 发送作业执行通知
    /// </summary>
    public async Task SendJobExecutionNotificationAsync(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 获取配置
            var config = await GetPushPlusConfigAsync(cancellationToken);
            if (!config.Enable)
            {
                _logger.LogInfo("PushPlus通知", "已禁用, 跳过");
                return;
            }

            // 检查通知策略
            if (success && !config.Strategy.NotifyOnJobSuccess)
            {
                _logger.LogInfo("PushPlus通知", "作业执行成功, 但根据策略不发送通知");
                return;
            }

            if (!success && !config.Strategy.NotifyOnJobFailure)
            {
                _logger.LogInfo("PushPlus通知", "作业执行失败, 但根据策略不发送通知");
                return;
            }

            // 创建通知记录
            var notification = new QuartzNotification
            {
                Title = $"作业执行{(success ? "成功" : "失败")}: {jobName}",
                Content = GenerateJobExecutionContent(
                    config.Template,
                    jobName,
                    jobGroup,
                    success,
                    message,
                    duration,
                    errorMessage
                ),
                Status = NotificationStatus.Pending,
                TriggeredBy = $"{jobGroup}.{jobName}",
            };

            // 保存通知记录
            await _jobStorage.AddNotificationAsync(notification, cancellationToken);

            // 发送通知
            var sendResult = await SendPushPlusNotificationAsync(
                config,
                notification.Title,
                notification.Content,
                cancellationToken
            );

            // 更新通知记录
            notification.Status = sendResult ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SendTime = DateTime.Now;
            await _jobStorage.UpdateNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PushPlus通知", "发送作业执行通知", ex);
        }
    }

    /// <summary>
    /// 发送调度器异常通知
    /// </summary>
    public async Task SendSchedulerErrorNotificationAsync(
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 获取配置
            var config = await GetPushPlusConfigAsync(cancellationToken);
            if (!config.Enable)
            {
                _logger.LogInfo("PushPlus通知", "已禁用, 跳过");
                return;
            }

            // 检查通知策略
            if (!config.Strategy.NotifyOnSchedulerError)
            {
                _logger.LogInfo("PushPlus通知", "调度器异常, 但根据策略不发送通知");
                return;
            }

            // 创建通知记录
            var notification = new QuartzNotification
            {
                Title = "调度器异常",
                Content = GenerateSchedulerErrorContent(config.Template, exception),
                Status = NotificationStatus.Pending,
                TriggeredBy = "Scheduler",
            };

            // 保存通知记录
            await _jobStorage.AddNotificationAsync(notification, cancellationToken);

            // 发送通知
            var sendResult = await SendPushPlusNotificationAsync(
                config,
                notification.Title,
                notification.Content,
                cancellationToken
            );

            // 更新通知记录
            notification.Status = sendResult ? NotificationStatus.Sent : NotificationStatus.Failed;
            notification.SendTime = DateTime.Now;
            await _jobStorage.UpdateNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PushPlus通知", "发送调度器异常通知", ex);
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
                _logger.LogInfo("PushPlus通知", "已禁用, 测试通知发送失败");
                return false;
            }

            // 发送测试通知（按当前模板格式生成, 便于直接验证模板效果）
            var title = "测试通知";
            var content = GenerateTestNotificationContent(config.Template, title);
            return await SendPushPlusNotificationAsync(config, title, content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PushPlus通知", "发送测试通知", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成测试通知内容（按所选模板格式）
    /// </summary>
    private string GenerateTestNotificationContent(string template, string title)
    {
        var sendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        const string description = "这是一条测试通知, 用于验证PushPlus配置是否正确。";
        const string footer = "此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";

        switch ((template ?? "html").ToLower())
        {
            case "txt":
                return $@"{title}

{description}

发送时间: {sendTime}

---
{footer}";
            case "markdown":
                return $@"# {title}

{description}

| 项目 | 内容 |
|------|------|
| 通知类型 | 测试通知 |
| 发送时间 | {sendTime} |

---
{footer}";
            case "json":
                var payload = new Dictionary<string, object?>
                {
                    ["eventType"] = "test",
                    ["title"] = title,
                    ["message"] = description,
                    ["sendTime"] = sendTime,
                    ["source"] = "Chet.QuartzNET.UI",
                };
                return JsonSerializer.Serialize(payload, NotificationJsonOptions);
            case "html":
            default:
                return $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">"
                    + $"<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;\">"
                    + $"<div style=\"background-color: #f0fff4; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;\">"
                    + $"<h1 style=\"margin: 0; font-size: 20px; font-weight: 600; color: #2da44e;\">🔔 {title}</h1>"
                    + $"</div>"
                    + $"<div style=\"padding: 20px;\">"
                    + $"<p style=\"margin: 0 0 12px 0;\">{description}</p>"
                    + $"<p style=\"margin: 0; color: #656d76; font-size: 13px;\">发送时间: {sendTime}</p>"
                    + $"</div>"
                    + $"<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 16px 20px; font-size: 12px; color: #656d76;\">"
                    + $"<p style=\"margin: 0;\">{footer}</p>"
                    + $"</div>"
                    + $"</div>"
                    + $"</div>";
        }
    }

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    public async Task<PushPlusConfigDto> GetPushPlusConfigAsync(
        CancellationToken cancellationToken = default
    )
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
                        NotifyOnSchedulerError = true,
                    },
                };
            }

            // 解析配置
            var config = JsonSerializer.Deserialize<PushPlusConfigDto>(
                setting.Value,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return config
                ?? new PushPlusConfigDto
                {
                    Enable = false,
                    Channel = "wechat",
                    Template = "html",
                    Strategy = new NotificationStrategyDto
                    {
                        NotifyOnJobSuccess = false,
                        NotifyOnJobFailure = true,
                        NotifyOnSchedulerError = true,
                    },
                };
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetPushPlusConfig", ex);
            return new PushPlusConfigDto
            {
                Enable = false,
                Channel = "wechat",
                Template = "html",
                Strategy = new NotificationStrategyDto
                {
                    NotifyOnJobSuccess = false,
                    NotifyOnJobFailure = true,
                    NotifyOnSchedulerError = true,
                },
            };
        }
    }

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    public async Task<bool> SavePushPlusConfigAsync(
        PushPlusConfigDto config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 序列化配置
            var configJson = JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // 中文等非 ASCII 字符按原文输出，避免保存成 \uXXXX 转义
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            );

            // 保存配置
            var setting = new QuartzSetting
            {
                Key = PushPlusConfigKey,
                Value = configJson,
                Description = "PushPlus通知配置",
                Enabled = config.Enable,
            };

            return await _jobStorage.SaveSettingAsync(setting, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("SavePushPlusConfig", ex);
            return false;
        }
    }

    /// <summary>
    /// 发送PushPlus通知
    /// </summary>
    private async Task<bool> SendPushPlusNotificationAsync(
        PushPlusConfigDto config,
        string title,
        string content,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // 构建请求体
            var requestBody = new Dictionary<string, object?>
            {
                ["token"] = config.Token,
                ["title"] = title,
                ["content"] = content,
                ["template"] = config.Template,
                ["channel"] = config.Channel,
            };

            // 可选参数：仅在有值时添加，避免发送无效字段
            if (!string.IsNullOrEmpty(config.Topic))
            {
                requestBody["topic"] = config.Topic;
            }
            if (!string.IsNullOrEmpty(config.Option))
            {
                requestBody["option"] = config.Option;
            }
            if (!string.IsNullOrEmpty(config.To))
            {
                requestBody["to"] = config.To;
            }
            if (!string.IsNullOrEmpty(config.CallbackUrl))
            {
                requestBody["callbackUrl"] = config.CallbackUrl;
            }
            if (config.Timestamp.HasValue)
            {
                requestBody["timestamp"] = config.Timestamp.Value;
            }

            // 发送请求
            var response = await _httpClient.PostAsJsonAsync(
                PushPlusApiUrl,
                requestBody,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            // 解析响应
            var responseBody = await response.Content.ReadFromJsonAsync<
                Dictionary<string, object>
            >();
            if (responseBody == null || !responseBody.TryGetValue("code", out var codeObj))
            {
                _logger.LogFailure("PushPlus通知", "PushPlus API响应格式错误");
                return false;
            }

            var code = Convert.ToString(codeObj);
            if (code != "200")
            {
                var msg = responseBody.TryGetValue("msg", out var msgObj)
                    ? Convert.ToString(msgObj)
                    : "未知错误";
                _logger.LogFailure("PushPlus通知", $"PushPlus API调用失败: {msg}");
                return false;
            }

            _logger.LogSuccess("PushPlus通知", "发送PushPlus通知");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogFailure("PushPlus通知", "发送PushPlus通知", ex);
            return false;
        }
    }

    /// <summary>
    /// 生成作业执行通知内容
    /// </summary>
    private string GenerateJobExecutionContent(
        string template,
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage = null
    )
    {
        var status = success ? "成功" : "失败";
        var executionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        switch (template.ToLower())
        {
            case "txt":
                return GenerateJobExecutionTextContent(
                    jobName,
                    jobGroup,
                    success,
                    message,
                    duration,
                    errorMessage,
                    executionTime
                );
            case "markdown":
                return GenerateJobExecutionMarkdownContent(
                    jobName,
                    jobGroup,
                    success,
                    message,
                    duration,
                    errorMessage,
                    executionTime
                );
            case "json":
                return GenerateJobExecutionJsonContent(
                    jobName,
                    jobGroup,
                    success,
                    message,
                    duration,
                    errorMessage,
                    executionTime
                );
            case "html":
            default:
                return GenerateJobExecutionHtmlContent(
                    jobName,
                    jobGroup,
                    success,
                    message,
                    duration,
                    errorMessage,
                    executionTime
                );
        }
    }

    /// <summary>
    /// 生成作业执行HTML通知内容
    /// </summary>
    private string GenerateJobExecutionHtmlContent(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage,
        string executionTime
    )
    {
        var status = success ? "成功" : "失败";
        var statusBadge = success ? "✅" : "❌";
        var statusColor = success ? "#2da44e" : "#cf222e";
        var headerBg = success ? "#f0fff4" : "#fff5f5";

        // 使用简单的字符串拼接, 避免复杂的转义问题
        return $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">"
            + $"<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;\">"
            + $"<div style=\"background-color: {headerBg}; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;\">"
            + $"<h1 style=\"margin: 0; font-size: 20px; font-weight: 600; color: {statusColor};\">{statusBadge} 作业执行{status}: {jobName}</h1>"
            + $"</div>"
            + $"<div style=\"padding: 20px;\">"
            + $"<div style=\"margin-bottom: 24px;\">"
            + $"<h2 style=\"margin: 0 0 16px 0; font-size: 16px; font-weight: 600;\">作业信息</h2>"
            + $"<table style=\"width: 100%; border-collapse: collapse; font-size: 14px;\">"
            + $"<tbody>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">作业名称</td>"
            + $"<td style=\"padding: 10px 16px;\">{jobName}</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">作业分组</td>"
            + $"<td style=\"padding: 10px 16px;\">{jobGroup}</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行状态</td>"
            + $"<td style=\"padding: 10px 16px;\"><span style=\"color: {statusColor}; font-weight: 600;\">{statusBadge} {status}</span></td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行时间</td>"
            + $"<td style=\"padding: 10px 16px;\">{executionTime}</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">执行耗时</td>"
            + $"<td style=\"padding: 10px 16px;\">{duration} 毫秒</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">消息</td>"
            + $"<td style=\"padding: 10px 16px;\">{message}</td>"
            + $"</tr>"
            + (
                !success && !string.IsNullOrEmpty(errorMessage)
                    ? $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
                        + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">错误信息</td>"
                        + $"<td style=\"padding: 10px 16px; color: {statusColor}; word-break: break-word;\">{errorMessage}</td>"
                        + $"</tr>"
                    : ""
            )
            + $"</tbody>"
            + $"</table>"
            + $"</div>"
            + $"</div>"
            + $"<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 16px 20px; font-size: 12px; color: #656d76;\">"
            + $"<p style=\"margin: 0;\">此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。</p>"
            + $"</div>"
            + $"</div>"
            + $"</div>";
    }

    /// <summary>
    /// 生成作业执行纯文本通知内容
    /// </summary>
    private string GenerateJobExecutionTextContent(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage,
        string executionTime
    )
    {
        var status = success ? "成功" : "失败";

        var content =
            $@"作业执行{status}: {jobName}

作业名称: {jobName}
作业分组: {jobGroup}
执行状态: {status}
执行时间: {executionTime}
执行耗时: {duration} 毫秒
消息: {message}";

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            content +=
                $@"
错误信息: {errorMessage}";
        }

        content +=
            $@"

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";

        return content;
    }

    /// <summary>
    /// 生成作业执行Markdown通知内容
    /// </summary>
    private string GenerateJobExecutionMarkdownContent(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage,
        string executionTime
    )
    {
        var status = success ? "成功" : "失败";
        var statusBadge = success ? "✅" : "❌";

        var content =
            $@"# {statusBadge} 作业执行{status}: {jobName}

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
            content +=
                $@"
| 错误信息 | {errorMessage} |";
        }

        content +=
            $@"

---
此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";

        return content;
    }

    /// <summary>
    /// 生成作业执行JSON通知内容
    /// </summary>
    private string GenerateJobExecutionJsonContent(
        string jobName,
        string jobGroup,
        bool success,
        string message,
        long duration,
        string? errorMessage,
        string executionTime
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["eventType"] = "jobExecution",
            ["title"] = $"作业执行{(success ? "成功" : "失败")}: {jobName}",
            ["jobName"] = jobName,
            ["jobGroup"] = jobGroup,
            ["status"] = success ? "success" : "failed",
            ["statusText"] = success ? "成功" : "失败",
            ["message"] = message,
            ["executionTime"] = executionTime,
            ["duration"] = duration,
            ["errorMessage"] = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage,
            ["source"] = "Chet.QuartzNET.UI",
        };

        return JsonSerializer.Serialize(payload, NotificationJsonOptions);
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
            case "json":
                return GenerateSchedulerErrorJsonContent(exception, executionTime);
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

        // 使用简单的字符串拼接, 避免复杂的转义问题
        return $"<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">"
            + $"<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;\">"
            + $"<div style=\"background-color: #fff5f5; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;\">"
            + $"<h1 style=\"margin: 0; font-size: 20px; font-weight: 600; color: {statusColor};\">⚠️ 调度器异常</h1>"
            + $"</div>"
            + $"<div style=\"padding: 20px;\">"
            + $"<div style=\"margin-bottom: 24px;\">"
            + $"<h2 style=\"margin: 0 0 16px 0; font-size: 16px; font-weight: 600;\">异常信息</h2>"
            + $"<table style=\"width: 100%; border-collapse: collapse; font-size: 14px;\">"
            + $"<tbody>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常时间</td>"
            + $"<td style=\"padding: 10px 16px;\">{executionTime}</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常类型</td>"
            + $"<td style=\"padding: 10px 16px;\">{exception.GetType().Name}</td>"
            + $"</tr>"
            + $"<tr style=\"border-bottom: 1px solid #e1e4e8;\">"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa;\">异常消息</td>"
            + $"<td style=\"padding: 10px 16px; color: {statusColor}; word-break: break-word;\">{exception.Message}</td>"
            + $"</tr>"
            + $"<tr>"
            + $"<td style=\"padding: 10px 16px; width: 120px; font-weight: 500; background-color: #f6f8fa; vertical-align: top;\">堆栈跟踪</td>"
            + $"<td style=\"padding: 10px 16px;\">"
            + $"<pre style=\"background-color: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px; padding: 16px; font-size: 12px; line-height: 1.5; font-family: ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, Liberation Mono, monospace; overflow-x: auto; margin: 0;\">{exception.StackTrace}</pre>"
            + $"</td>"
            + $"</tr>"
            + $"</tbody>"
            + $"</table>"
            + $"</div>"
            + $"</div>"
            + $"<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 16px 20px; font-size: 12px; color: #656d76;\">"
            + $"<p style=\"margin: 0;\">此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。</p>"
            + $"</div>"
            + $"</div>"
            + $"</div>";
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
此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";
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
此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";
    }

    /// <summary>
    /// 生成调度器异常JSON通知内容
    /// </summary>
    private string GenerateSchedulerErrorJsonContent(Exception exception, string executionTime)
    {
        var payload = new Dictionary<string, object?>
        {
            ["eventType"] = "schedulerError",
            ["title"] = "调度器异常",
            ["exceptionType"] = exception.GetType().Name,
            ["message"] = exception.Message,
            ["stackTrace"] = exception.StackTrace,
            ["executionTime"] = executionTime,
            ["source"] = "Chet.QuartzNET.UI",
        };

        return JsonSerializer.Serialize(payload, NotificationJsonOptions);
    }
}
