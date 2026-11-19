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
    /// HTML 通知卡片底部说明
    /// </summary>
    private const string HtmlFooter = "此消息由 Chet.QuartzNET.UI 调度系统自动发送, 请勿回复。";

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
        ILogger<PushPlusNotificationService> logger
    )
    {
        _jobStorage = jobStorage;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
                var descriptionHtml = $"<p style=\"margin: 0 0 12px 0;\">{description}</p>";
                return BuildHtmlCard(
                    "#2da44e",
                    "#f0fff4",
                    "🔔",
                    title,
                    null,
                    descriptionHtml
                        + BuildHtmlRows(("通知类型", "测试通知", null), ("发送时间", sendTime, null))
                );
        }
    }

    /// <summary>
    /// 生成统一风格的 HTML 通知卡片（与前端「推送预览」结构保持一致）
    /// </summary>
    /// <param name="accentColor">主色（标题颜色）</param>
    /// <param name="headerBg">头部背景色</param>
    /// <param name="badge">头部徽标（emoji）</param>
    /// <param name="title">标题</param>
    /// <param name="subtitle">副标题，可为空</param>
    /// <param name="bodyHtml">正文内容</param>
    private static string BuildHtmlCard(
        string accentColor,
        string headerBg,
        string badge,
        string title,
        string? subtitle,
        string bodyHtml
    )
    {
        var subtitleHtml = string.IsNullOrWhiteSpace(subtitle)
            ? string.Empty
            : "<div style=\"margin-top: 2px; font-size: 12px; color: #656d76; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; word-break: break-all;\">"
                + subtitle
                + "</div>";

        return "<div style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.6; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;\">"
            + "<div style=\"background-color: #ffffff; border: 1px solid #e1e4e8; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.06); overflow: hidden;\">"
            + $"<div style=\"background-color: {headerBg}; border-bottom: 1px dashed #e1e4e8; padding: 14px 20px;\">"
            + "<table style=\"width: 100%; border-collapse: collapse;\"><tbody><tr>"
            + "<td style=\"width: 28px; vertical-align: middle; font-size: 18px;\">"
            + badge
            + "</td>"
            + "<td style=\"vertical-align: middle;\">"
            + $"<div style=\"font-size: 16px; font-weight: 600; color: {accentColor}; word-break: break-word;\">{title}</div>"
            + subtitleHtml
            + "</td>"
            + "</tr></tbody></table>"
            + "</div>"
            + $"<div style=\"padding: 16px 20px;\">{bodyHtml}</div>"
            + "<div style=\"background-color: #f6f8fa; border-top: 1px solid #e1e4e8; padding: 12px 20px; font-size: 12px; color: #656d76;\">"
            + $"<p style=\"margin: 0;\">{HtmlFooter}</p>"
            + "</div>"
            + "</div>"
            + "</div>";
    }

    /// <summary>
    /// 生成 HTML 通知卡片中的键值行表格
    /// </summary>
    /// <param name="rows">行数据：标签、内容、内容颜色（可选）</param>
    private static string BuildHtmlRows(params (string Label, string Value, string? Color)[] rows)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(
            "<table style=\"width: 100%; border-collapse: collapse; font-size: 13.5px; line-height: 1.6;\"><tbody>"
        );

        foreach (var (label, value, color) in rows)
        {
            var valueColor = string.IsNullOrWhiteSpace(color) ? "#24292f" : color;
            builder.Append("<tr style=\"border-bottom: 1px dashed #e1e4e8;\">");
            builder.Append(
                $"<td style=\"padding: 9px 0; width: 96px; color: #656d76; vertical-align: top;\">{label}</td>"
            );
            builder.Append(
                $"<td style=\"padding: 9px 0; text-align: right; color: {valueColor}; word-break: break-all;\">{value}</td>"
            );
            builder.Append("</tr>");
        }

        builder.Append("</tbody></table>");
        return builder.ToString();
    }

    /// <summary>
    /// HTML 转义，避免作业名/消息里的特殊字符破坏页面结构
    /// </summary>
    private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

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
            // 创建HttpClient时配置显式禁用代理, 避免系统代理导致的连接问题
            var handler = new HttpClientHandler
            {
                // 显式禁用代理, 解决127.0.0.1:7890连接拒绝问题
                UseProxy = false,
                // 禁用SSL验证（仅开发环境使用, 生产环境建议启用）
                ServerCertificateCustomValidationCallback = (
                    sender,
                    cert,
                    chain,
                    sslPolicyErrors
                ) => true,
            };

            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Chet.QuartzNet.UI");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

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
            var response = await httpClient.PostAsJsonAsync(
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

        var rows = new List<(string Label, string Value, string? Color)>
        {
            ("作业名称", Html(jobName), null),
            ("作业分组", Html(jobGroup), null),
            ("执行状态", $"{statusBadge} {status}", statusColor),
            ("执行时间", executionTime, null),
            ("执行耗时", DurationFormatter.Format(duration), null),
            ("消息", Html(message), null),
        };

        if (!success && !string.IsNullOrWhiteSpace(errorMessage))
        {
            rows.Add(("错误信息", Html(errorMessage), statusColor));
        }

        return BuildHtmlCard(
            statusColor,
            headerBg,
            statusBadge,
            $"作业执行{status}",
            Html(jobName),
            BuildHtmlRows(rows.ToArray())
        );
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
执行耗时: {DurationFormatter.Format(duration)}
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
| 执行耗时 | {DurationFormatter.Format(duration)} |
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

        var rows = new List<(string Label, string Value, string? Color)>
        {
            ("异常时间", executionTime, null),
            ("异常类型", Html(exception.GetType().Name), null),
            ("异常消息", Html(exception.Message), statusColor),
        };

        var stackTraceHtml = string.IsNullOrWhiteSpace(exception.StackTrace)
            ? string.Empty
            : "<div style=\"margin-top: 14px;\">"
                + "<div style=\"margin-bottom: 6px; color: #656d76;\">堆栈跟踪</div>"
                + $"<pre style=\"margin: 0; padding: 12px; background-color: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px; font-size: 12px; line-height: 1.5; white-space: pre-wrap; word-break: break-all; font-family: ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, Liberation Mono, monospace;\">{Html(exception.StackTrace)}</pre>"
                + "</div>";

        return BuildHtmlCard(
            statusColor,
            "#fff5f5",
            "⚠️",
            "调度器异常",
            null,
            BuildHtmlRows(rows.ToArray()) + stackTraceHtml
        );
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
