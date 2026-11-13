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
public class SchedulerController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(IQuartzJobService jobService, ILogger<SchedulerController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取调度器状态
    /// </summary>
    [HttpGet("GetSchedulerStatus")]
    public async Task<ActionResult<ApiResponseDto<SchedulerStatusDto>>> GetSchedulerStatus()
    {
        try
        {
            var status = await _jobService.GetSchedulerStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取调度器状态", ex);
            return Ok(
                ApiResponseDto<SchedulerStatusDto>.ErrorResponse(
                    "获取调度器状态失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 启动调度器
    /// </summary>
    [HttpPost("StartScheduler")]
    public async Task<ActionResult<ApiResponseDto<bool>>> StartScheduler()
    {
        try
        {
            var result = await _jobService.StartSchedulerAsync();
            if (result.Success)
            {
                _logger.LogSuccess("启动调度器");
            }
            else
            {
                _logger.LogWarn("启动调度器", $"调度器启动失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("启动调度器", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("启动调度器失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 排水：停止接收新触发，等待执行中任务收尾后自动停止
    /// </summary>
    [HttpPost("DrainScheduler")]
    public async Task<ActionResult<ApiResponseDto<bool>>> DrainScheduler(
        [FromBody] DrainSchedulerRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.DrainSchedulerAsync(
                request.TimeoutSeconds,
                cancellationToken
            );
            if (result.Success)
            {
                _logger.LogSuccess("排水调度器");
            }
            else
            {
                _logger.LogWarn("排水调度器", $"失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("排水调度器", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("排水失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 取消排水并恢复调度
    /// </summary>
    [HttpPost("ResumeScheduler")]
    public async Task<ActionResult<ApiResponseDto<bool>>> ResumeScheduler(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.ResumeSchedulerAsync(cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("恢复调度器");
            }
            else
            {
                _logger.LogWarn("恢复调度器", $"失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("恢复调度器", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("恢复调度失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取本次启动的中断恢复汇总
    /// </summary>
    [HttpGet("GetRecoverySummary")]
    public async Task<ActionResult<ApiResponseDto<RecoverySummaryDto>>> GetRecoverySummary(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.GetRecoverySummaryAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取恢复汇总", ex);
            return Ok(
                ApiResponseDto<RecoverySummaryDto>.ErrorResponse(
                    "获取恢复汇总失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 停止调度器
    /// </summary>
    [HttpPost("StopScheduler")]
    public async Task<ActionResult<ApiResponseDto<bool>>> StopScheduler()
    {
        try
        {
            var result = await _jobService.ShutdownSchedulerAsync();
            if (result.Success)
            {
                _logger.LogSuccess("停止调度器");
            }
            else
            {
                _logger.LogWarn("停止调度器", $"调度器停止失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("停止调度器", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("停止调度器失败: " + ex.Message));
        }
    }
}