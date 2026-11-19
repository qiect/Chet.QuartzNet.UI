using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 作业状态枚举
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// 正常
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 暂停
    /// </summary>
    Paused = 1,

    /// <summary>
    /// 完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 3,

    /// <summary>
    /// 阻塞
    /// </summary>
    Blocked = 4,
}
