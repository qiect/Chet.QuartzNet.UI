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
        logger.LogInformation($"[{LOG_PREFIX}] <{operation}> {message}", args);
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
            logger.LogInformation($"[{LOG_PREFIX}] (SUCCESS) <{operation}> 操作成功");
        }
        else
        {
            logger.LogInformation($"[{LOG_PREFIX}] (SUCCESS) <{operation}> 操作成功, {details}");
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
        logger.LogInformation($"[{LOG_PREFIX}] (SUCCESS) <{operation}> 操作成功, {message}", args);
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
            logger.LogWarning($"[{LOG_PREFIX}] (WARNING) <{operation}> 操作警告");
        }
        else
        {
            logger.LogWarning($"[{LOG_PREFIX}] (WARNING) <{operation}> 操作警告, {details}");
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
        logger.LogWarning($"[{LOG_PREFIX}] (WARNING) <{operation}> {message}", args);
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
        logger.LogError(exception, $"[{LOG_PREFIX}] (FAILURE) <{operation}> 操作失败");
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
        logger.LogError(exception, $"[{LOG_PREFIX}] (FAILURE) <{operation}> 操作失败, {details}");
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
        logger.LogError($"[{LOG_PREFIX}] (FAILURE) <{operation}> 操作失败, {details}");
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
                $"[{LOG_PREFIX}] (PERFORMANCE) <{operation}> 性能警告, 执行时间过长 - {durationMilliseconds}ms"
            );
        }
        else
        {
            logger.LogInformation(
                $"[{LOG_PREFIX}] (PERFORMANCE) <{operation}> 执行时间: {durationMilliseconds}ms"
            );
        }
    }
    #endregion
}
