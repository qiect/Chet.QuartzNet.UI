using System.Text.Json.Serialization;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 系统配置响应DTO：持久化配置 + 当前运行期生效值
/// </summary>
public class SystemConfigResponseDto : SystemConfigDto
{
    /// <summary>
    /// 当前进程启动时加载的生效配置
    /// </summary>
    public SystemConfigDto? EffectiveConfig { get; set; }

    /// <summary>
    /// 是否存在需要重启才能生效的修改
    /// </summary>
    public bool RestartRequired { get; set; }

    /// <summary>
    /// 当前进程启动时间
    /// </summary>
    public DateTime? BootTime { get; set; }
}
