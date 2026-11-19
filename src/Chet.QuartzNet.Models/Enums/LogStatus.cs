using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.Entities;

/// <summary>
/// 日志状态枚举
/// </summary>
public enum LogStatus
{
    /// <summary>
    /// 运行中
    /// </summary>
    Running = 0,

    /// <summary>
    /// 成功
    /// </summary>
    Success = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 2,

    /// <summary>
    /// 跳过（前置依赖未满足等场景，本次未执行）
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// 中断（进程退出/崩溃/停机超时，未正常收尾）
    /// </summary>
    Interrupted = 4,
}
