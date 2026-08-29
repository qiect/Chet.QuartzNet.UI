using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Controllers;

[Route("api/quartz")]
[ApiController]
[Authorize(Policy = "QuartzUIPolicy")]
public class SystemConfigController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<SystemConfigController> _logger;

    public SystemConfigController(IQuartzJobService jobService, ILogger<SystemConfigController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>系统配置</returns>
    [HttpGet("GetSystemConfig")]
    public async Task<ActionResult<ApiResponseDto<SystemConfigDto>>> GetSystemConfig(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.GetSystemConfigAsync(cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("获取系统配置");
            }
            else
            {
                _logger.LogWarn("获取系统配置", $"获取系统配置失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取系统配置", ex);
            return Ok(
                ApiResponseDto<SystemConfigDto>.ErrorResponse("获取系统配置失败: " + ex.Message)
            );
        }
    }

    /// <summary>
    /// 保存系统配置
    /// </summary>
    /// <param name="config">系统配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    [HttpPost("SaveSystemConfig")]
    public async Task<ActionResult<ApiResponseDto<bool>>> SaveSystemConfig(
        [FromBody] SystemConfigDto config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.SaveSystemConfigAsync(config, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("保存系统配置");
            }
            else
            {
                _logger.LogWarn("保存系统配置", $"保存系统配置失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("保存系统配置", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("保存系统配置失败: " + ex.Message));
        }
    }
}