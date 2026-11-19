using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 作业类型枚举
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JobTypeEnum
{
    /// <summary>
    /// DLL作业（默认）
    /// </summary>
    DLL = 0,

    /// <summary>
    /// API作业
    /// </summary>
    API = 1,
}
