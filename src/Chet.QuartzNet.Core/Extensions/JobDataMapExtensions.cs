using System.Text.Json;
using Chet.QuartzNet.Core.Consts;
using Quartz;

namespace Chet.QuartzNet.Core.Extensions;

/// <summary>
/// 作业数据映射扩展类
/// </summary>
public static class JobDataMapExtensions
{
    /// <summary>
    /// 从JobDataMap中获取作业数据JSON
    /// </summary>
    /// <param name="jobDataMap">作业数据映射</param>
    /// <returns>作业数据JSON字符串</returns>
    public static string GetJobDataJson(this JobDataMap jobDataMap)
    {
        return jobDataMap.Keys.Any(p => p.Equals(QuartzJobConst.JobData))
            ? jobDataMap.GetString(QuartzJobConst.JobData)
            : null;
    }

    /// <summary>
    /// 从JobDataMap中获取强类型作业数据
    /// </summary>
    /// <typeparam name="T">作业数据类型</typeparam>
    /// <param name="jobDataMap">作业数据映射</param>
    /// <returns>强类型作业数据</returns>
    public static T GetJobData<T>(this JobDataMap jobDataMap)
        where T : class
    {
        var json = jobDataMap.GetJobDataJson();
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json);
    }
}
