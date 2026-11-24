using System.ComponentModel.DataAnnotations;
using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 批量启用/禁用请求DTO
/// </summary>
public class BatchEnableRequest
{
    /// <summary>
    /// 目标作业列表
    /// </summary>
    public List<BatchDeleteRequest> Jobs { get; set; } = new();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }
}
