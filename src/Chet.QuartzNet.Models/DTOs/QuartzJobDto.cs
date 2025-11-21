using Chet.QuartzNet.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 条件范围验证属性
/// </summary>
public class ConditionalRangeAttribute : RangeAttribute
{
    private readonly string _dependentProperty;
    private readonly object _targetValue;

    public ConditionalRangeAttribute(double minimum, double maximum, string dependentProperty, object targetValue) : base(minimum, maximum)
    {
        _dependentProperty = dependentProperty;
        _targetValue = targetValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // 获取依赖属性的值
        var dependentPropertyValue = validationContext.ObjectType.GetProperty(_dependentProperty)?.GetValue(validationContext.ObjectInstance);

        // 只有当依赖属性的值等于目标值时，才进行范围验证
        if (dependentPropertyValue != null && dependentPropertyValue.Equals(_targetValue))
        {
            return base.IsValid(value, validationContext);
        }

        // 否则验证通过
        return ValidationResult.Success;
    }
}

/// <summary>
/// Quartz作业DTO
/// </summary>
public class QuartzJobDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    [Required(ErrorMessage = "作业名称不能为空")]
    [StringLength(100, ErrorMessage = "作业名称长度不能超过100个字符")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    [Required(ErrorMessage = "作业分组不能为空")]
    [StringLength(100, ErrorMessage = "作业分组长度不能超过100个字符")]
    public string JobGroup { get; set; } = "DEFAULT";

    /// <summary>
    /// 触发器名称（可选，若为空则自动根据作业名称生成）
    /// </summary>
    [StringLength(100, ErrorMessage = "触发器名称长度不能超过100个字符")]
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发器分组（可选，默认与作业分组相同）
    /// </summary>
    [StringLength(100, ErrorMessage = "触发器分组长度不能超过100个字符")]
    public string? TriggerGroup { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    [Required(ErrorMessage = "Cron表达式不能为空")]
    [StringLength(200, ErrorMessage = "Cron表达式长度不能超过200个字符")]
    public string CronExpression { get; set; } = "0 0/1 * * * ?";

    /// <summary>
    /// 作业描述
    /// </summary>
    [StringLength(500, ErrorMessage = "作业描述长度不能超过500个字符")]
    public string? Description { get; set; }

    /// <summary>
    /// 作业类型枚举
    /// </summary>
    public JobTypeEnum JobTypeEnum { get; set; } = JobTypeEnum.DLL;

    /// <summary>
    /// 作业类型（类名或API URL）
    /// </summary>
    [Required(ErrorMessage = "作业类型不能为空")]
    [StringLength(500, ErrorMessage = "作业类型长度不能超过500个字符")]
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// 作业数据（JSON格式）
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// API请求方法（GET/POST等）
    /// </summary>
    [StringLength(10, ErrorMessage = "API请求方法长度不能超过10个字符")]
    public string? ApiMethod { get; set; } = "GET";

    /// <summary>
    /// API请求头（JSON格式）
    /// </summary>
    public string? ApiHeaders { get; set; }

    /// <summary>
    /// API请求体（JSON格式）
    /// </summary>
    public string? ApiBody { get; set; }

    /// <summary>
    /// API超时时间（毫秒）
    /// </summary>
    [ConditionalRange(1, 3600, nameof(JobTypeEnum), JobTypeEnum.API, ErrorMessage = "API超时时间必须在1秒到1小时之间")]
    public int ApiTimeout { get; set; } = 30; // 默认30秒

    /// <summary>
    /// 跳过SSL验证
    /// </summary>
    public bool SkipSslValidation { get; set; } = false;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Quartz作业查询DTO
/// </summary>
public class QuartzJobQueryDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// 作业分组
    /// </summary>
    public string? JobGroup { get; set; }

    /// <summary>
    /// 作业状态
    /// </summary>
    public JobStatus? Status { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 排序字段
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 排序方向（asc或desc）
    /// </summary>
    public string? SortOrder { get; set; }
}

/// <summary>
/// Quartz作业响应DTO
/// </summary>
public class QuartzJobResponseDto
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 作业分组
    /// </summary>
    public string JobGroup { get; set; } = string.Empty;

    /// <summary>
    /// 触发器名称
    /// </summary>
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>
    /// 触发器分组
    /// </summary>
    public string TriggerGroup { get; set; } = string.Empty;

    /// <summary>
    /// Cron表达式
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 作业描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 作业类型枚举
    /// </summary>
    public JobTypeEnum JobTypeEnum { get; set; }

    /// <summary>
    /// 作业类型（类名或API URL）
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// 作业数据（JSON格式）
    /// </summary>
    public string? JobData { get; set; }

    /// <summary>
    /// API请求方法
    /// </summary>
    public string? ApiMethod { get; set; }

    /// <summary>
    /// API请求头
    /// </summary>
    public string? ApiHeaders { get; set; }

    /// <summary>
    /// API请求体
    /// </summary>
    public string? ApiBody { get; set; }

    /// <summary>
    /// API请求超时时间（秒）
    /// </summary>
    public int ApiTimeout { get; set; }

    /// <summary>
    /// 是否跳过SSL验证
    /// </summary>
    public bool SkipSslValidation { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 作业状态
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string? CreateBy { get; set; }

    /// <summary>
    /// 更新人
    /// </summary>
    public string? UpdateBy { get; set; }

    /// <summary>
    /// 下次执行时间
    /// </summary>
    public DateTime? NextRunTime { get; set; }

    /// <summary>
    /// 上次执行时间
    /// </summary>
    public DateTime? PreviousRunTime { get; set; }
}

/// <summary>
/// 分页响应DTO
/// </summary>
/// <typeparam name="T"></typeparam>
public class PagedResponseDto<T>
{
    /// <summary>
    /// 数据列表
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 总条数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 页码
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// API响应DTO
/// </summary>
/// <typeparam name="T"></typeparam>
public class ApiResponseDto<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 错误码
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 成功响应
    /// </summary>
    public static ApiResponseDto<T> SuccessResponse(T data, string message = "操作成功")
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 失败响应
    /// </summary>
    public static ApiResponseDto<T> ErrorResponse(string message, string? errorCode = null)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}