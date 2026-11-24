using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 作业依赖项
/// </summary>
public class JobDependencyDto
{
    /// <summary>
    /// 依赖作业名称
    /// </summary>
    [Required(ErrorMessage = "依赖作业名称不能为空")]
    [StringLength(100, ErrorMessage = "依赖作业名称长度不能超过100个字符")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 依赖作业分组
    /// </summary>
    [Required(ErrorMessage = "依赖作业分组不能为空")]
    [StringLength(100, ErrorMessage = "依赖作业分组长度不能超过100个字符")]
    public string JobGroup { get; set; } = string.Empty;
}
