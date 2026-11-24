using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Controllers;

[Route("api/quartz")]
[ApiController]
[Authorize(Policy = "QuartzUIPolicy")]
public class LogController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<LogController> _logger;

    public LogController(IQuartzJobService jobService, ILogger<LogController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取作业日志
    /// </summary>
    [HttpPost("GetJobLogs")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>>> GetJobLogs(
        [FromBody] QuartzJobLogQueryDto query
    )
    {
        try
        {
            var result = await _jobService.GetJobLogsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业日志", ex);
            return Ok(
                ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>.ErrorResponse(
                    "获取作业日志失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取单条日志详情
    /// </summary>
    [HttpGet("GetJobLog")]
    public async Task<ActionResult<ApiResponseDto<QuartzJobLog>>> GetJobLog(
        Guid logId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.GetJobLogAsync(logId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取日志详情", ex);
            return Ok(ApiResponseDto<QuartzJobLog>.ErrorResponse("获取日志详情失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 手动重新执行一条中断日志
    /// </summary>
    [HttpPost("RerunInterruptedLog")]
    public async Task<ActionResult<ApiResponseDto<bool>>> RerunInterruptedLog(
        [FromBody] RerunInterruptedLogRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var triggeredBy = User?.Identity?.Name;
            var result = await _jobService.RerunInterruptedLogAsync(
                request.LogId,
                triggeredBy,
                cancellationToken
            );
            if (result.Success)
            {
                _logger.LogSuccess("重新执行中断日志", request.LogId.ToString());
            }
            else
            {
                _logger.LogWarn(
                    "重新执行中断日志",
                    $"失败, 原因: {result.Message}, 错误码: {result.ErrorCode}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("重新执行中断日志", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("重新执行失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 手动中断一条运行中的作业日志
    /// </summary>
    [HttpPost("InterruptJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> InterruptJob(
        [FromBody] InterruptRunningLogRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.InterruptRunningJobAsync(
                request.LogId,
                cancellationToken
            );
            if (result.Success)
            {
                _logger.LogSuccess("中断作业", request.LogId.ToString());
            }
            else
            {
                _logger.LogWarn(
                    "中断作业",
                    $"失败, 原因: {result.Message}, 错误码: {result.ErrorCode}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("中断作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("中断作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 清空作业日志
    /// </summary>
    [HttpPost("ClearLogs")]
    public async Task<ActionResult<ApiResponseDto<bool>>> ClearLogs(
        [FromBody] QuartzJobLogQueryDto query
    )
    {
        try
        {
            var result = await _jobService.ClearJobLogsAsync(query);
            if (result.Success)
            {
                _logger.LogSuccess("清空作业日志");
            }
            else
            {
                _logger.LogWarn("清空作业日志", $"清空作业日志失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清空作业日志", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("清空作业日志失败: " + ex.Message));
        }
    }
}