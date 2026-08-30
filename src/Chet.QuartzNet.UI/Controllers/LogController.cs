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