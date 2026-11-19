using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 作业删除请求DTO
/// </summary>
public class BatchDeleteRequest
{
    /// <summary>
    /// 作业名称
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// 作业分组
    /// </summary>
    public string JobGroup { get; set; }
}
