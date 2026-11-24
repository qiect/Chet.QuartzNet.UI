using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.Core.Helpers;

/// <summary>
/// 日志帮助类，提供统一的日志记录方法和标准化格式
/// </summary>
public static class LoggerHelper
{
    #region 日志前缀常量
    private const string LOG_PREFIX = "Chet.QuartzNet.UI"; // 统一日志前缀
    #endregion

    /// <summary>
    /// 转义文本中的花括号（{ → {{、} → }}），避免被日志模板解析成占位符，
    /// 并因缺少对应参数抛出 FormatException（Index (zero based) ...）。
    /// 仅用于不承接 args 的插值段（operation、details、无参数时的 message）；
    /// 有 args 时 message 保持占位符模板语义。
    /// </summary>
    private static string EscapeLogText(string? text) =>
        text?.Replace("{", "{{").Replace("}", "}}") ?? string.Empty;

    #region Information 日志

    /// <summary>
    /// 记录信息日志（带参数）
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="message">日志消息模板</param>
    /// <param name="args">日志消息参数</param>
    public static void LogInfo<T>(
        this ILogger<T> logger,
        string operation,
        string message,
        params object?[] args
    )
    {
        // 无参数时 message 中不会存在可填充的占位符，其花括号必为字面量，转义防解析
        var safeMessage = args.Length == 0 ? EscapeLogText(message) : message;
        logger.LogInformation($"[{LOG_PREFIX}] <{EscapeLogText(operation)}> {safeMessage}", args);
    }

    /// <summary>
    /// 记录操作成功的日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="details">操作详情</param>
    public static void LogSuccess<T>(this ILogger<T> logger, string operation, string details = "")
    {
        if (string.IsNullOrEmpty(details))
        {
            logger.LogInformation(
                $"[{LOG_PREFIX}] (SUCCESS) <{EscapeLogText(operation)}> 操作成功"
            );
        }
        else
        {
            logger.LogInformation(
                $"[{LOG_PREFIX}] (SUCCESS) <{EscapeLogText(operation)}> 操作成功, {EscapeLogText(details)}"
            );
        }
    }

    /// <summary>
    /// 记录操作成功的日志（带参数）
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="message">日志消息模板</param>
    /// <param name="args">日志消息参数</param>
    public static void LogSuccess<T>(
        this ILogger<T> logger,
        string operation,
        string message,
        params object?[] args
    )
    {
        var safeMessage = args.Length == 0 ? EscapeLogText(message) : message;
        logger.LogInformation($"[{LOG_PREFIX}] (SUCCESS) <{EscapeLogText(operation)}> 操作成功, {safeMessage}", args);
    }
    #endregion

    #region Warning 日志

    /// <summary>
    /// 记录操作警告的日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="details">警告详情</param>
    public static void LogWarn<T>(this ILogger<T> logger, string operation, string details = "")
    {
        if (string.IsNullOrEmpty(details))
        {
            logger.LogWarning($"[{LOG_PREFIX}] (WARNING) <{EscapeLogText(operation)}> 操作警告");
        }
        else
        {
            logger.LogWarning(
                $"[{LOG_PREFIX}] (WARNING) <{EscapeLogText(operation)}> 操作警告, {EscapeLogText(details)}"
            );
        }
    }

    /// <summary>
    /// 记录操作警告的日志（带参数）
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="message">日志消息模板</param>
    /// <param name="args">日志消息参数</param>
    public static void LogWarn<T>(
        this ILogger<T> logger,
        string operation,
        string message,
        params object?[] args
    )
    {
        var safeMessage = args.Length == 0 ? EscapeLogText(message) : message;
        logger.LogWarning($"[{LOG_PREFIX}] (WARNING) <{EscapeLogText(operation)}> {safeMessage}", args);
    }
    #endregion

    #region Error 日志

    /// <summary>
    /// 记录操作失败的日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="exception">异常对象</param>
    public static void LogFailure<T>(this ILogger<T> logger, string operation, Exception exception)
    {
        logger.LogError(
            exception,
            $"[{LOG_PREFIX}] (FAILURE) <{EscapeLogText(operation)}> 操作失败"
        );
    }

    /// <summary>
    /// 记录操作失败的日志（带详情）
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="details">失败详情</param>
    /// <param name="exception">异常对象</param>
    public static void LogFailure<T>(
        this ILogger<T> logger,
        string operation,
        string details,
        Exception exception
    )
    {
        logger.LogError(
            exception,
            $"[{LOG_PREFIX}] (FAILURE) <{EscapeLogText(operation)}> 操作失败, {EscapeLogText(details)}"
        );
    }

    /// <summary>
    /// 记录操作失败的日志（不带异常）
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="details">失败详情</param>
    public static void LogFailure<T>(this ILogger<T> logger, string operation, string details)
    {
        logger.LogError(
            $"[{LOG_PREFIX}] (FAILURE) <{EscapeLogText(operation)}> 操作失败, {EscapeLogText(details)}"
        );
    }
    #endregion

    #region 结构化日志
    /// <summary>
    /// 记录结构化日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="properties">结构化属性</param>
    public static void LogStructured<T>(
        this ILogger<T> logger,
        LogLevel logLevel,
        string eventName,
        params (string Key, object? Value)[] properties
    )
    {
        var state = new Dictionary<string, object?> { ["EventName"] = eventName };

        foreach (var (key, value) in properties)
        {
            state[key] = value;
        }

        logger.Log(
            logLevel,
            new EventId(),
            state,
            null,
            (s, e) =>
            {
                var propertyString = string.Join(", ", s.Select(kv => $"{kv.Key}: {kv.Value}"));
                return $"[{LOG_PREFIX}] [{eventName}] {propertyString}";
            }
        );
    }

    /// <summary>
    /// 记录信息级别的结构化日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="properties">结构化属性</param>
    public static void LogInfoStructured<T>(
        this ILogger<T> logger,
        string eventName,
        params (string Key, object? Value)[] properties
    )
    {
        LogStructured(logger, LogLevel.Information, eventName, properties);
    }

    /// <summary>
    /// 记录错误级别的结构化日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="exception">异常对象</param>
    /// <param name="properties">结构化属性</param>
    public static void LogErrorStructured<T>(
        this ILogger<T> logger,
        string eventName,
        Exception exception,
        params (string Key, object? Value)[] properties
    )
    {
        var state = new Dictionary<string, object?> { ["EventName"] = eventName };

        foreach (var (key, value) in properties)
        {
            state[key] = value;
        }

        logger.Log(
            LogLevel.Error,
            new EventId(),
            state,
            exception,
            (s, e) =>
            {
                var propertyString = string.Join(", ", s.Select(kv => $"{kv.Key}: {kv.Value}"));
                return $"[{LOG_PREFIX}] (FAILURE) [{eventName}] {propertyString}";
            }
        );
    }
    #endregion

    #region 性能日志
    /// <summary>
    /// 记录性能指标日志
    /// </summary>
    /// <typeparam name="T">日志记录器泛型类型</typeparam>
    /// <param name="logger">日志记录器</param>
    /// <param name="operation">操作名称</param>
    /// <param name="durationMilliseconds">持续时间（毫秒）</param>
    /// <param name="thresholdMilliseconds">警告阈值（毫秒）</param>
    public static void LogPerformance<T>(
        this ILogger<T> logger,
        string operation,
        long durationMilliseconds,
        long thresholdMilliseconds = 1000
    )
    {
        if (durationMilliseconds > thresholdMilliseconds)
        {
            logger.LogWarning(
                $"[{LOG_PREFIX}] (PERFORMANCE) <{EscapeLogText(operation)}> 性能警告, 执行时间过长 - {durationMilliseconds}ms"
            );
        }
        else
        {
            logger.LogInformation(
                $"[{LOG_PREFIX}] (PERFORMANCE) <{EscapeLogText(operation)}> 执行时间: {durationMilliseconds}ms"
            );
        }
    }
    #endregion
}
