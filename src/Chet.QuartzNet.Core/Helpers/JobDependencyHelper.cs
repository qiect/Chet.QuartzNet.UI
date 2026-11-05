using System.Text.Json;
using Chet.QuartzNet.Models.DTOs;

namespace Chet.QuartzNet.Core.Helpers;

/// <summary>
/// 作业依赖序列化辅助类
/// </summary>
public static class JobDependencyHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 将依赖列表序列化为 JSON 字符串，空列表返回 null
    /// </summary>
    public static string? Serialize(List<JobDependencyDto>? dependencies)
    {
        if (dependencies == null || dependencies.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(dependencies, JsonOptions);
    }

    /// <summary>
    /// 将 JSON 字符串反序列化为依赖列表，空值或格式错误返回空列表
    /// </summary>
    public static List<JobDependencyDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<JobDependencyDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<JobDependencyDto>>(json, JsonOptions)
                ?? new List<JobDependencyDto>();
        }
        catch (JsonException)
        {
            return new List<JobDependencyDto>();
        }
    }

    /// <summary>
    /// 获取依赖作业的完整键（分组.名称）
    /// </summary>
    public static string GetJobKey(JobDependencyDto dependency) =>
        $"{dependency.JobGroup}.{dependency.JobName}";
}
