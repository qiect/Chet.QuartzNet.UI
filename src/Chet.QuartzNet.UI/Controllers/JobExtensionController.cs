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
public class JobExtensionController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<JobExtensionController> _logger;

    public JobExtensionController(IQuartzJobService jobService, ILogger<JobExtensionController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 验证Cron表达式
    /// </summary>
    [HttpGet("ValidateCronExpression")]
    public ActionResult<ApiResponseDto<bool>> ValidateCronExpression(string cronExpression)
    {
        try
        {
            var result = _jobService.ValidateCronExpression(cronExpression);
            if (result.Success)
            {
                _logger.LogSuccess("ValidateCronExpression", cronExpression);
            }
            else
            {
                _logger.LogWarn(
                    "ValidateCronExpression",
                    $"验证Cron表达式失败: {cronExpression}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("ValidateCronExpression", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("验证Cron表达式失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取Cron表达式未来N次执行时间
    /// </summary>
    [HttpGet("GetNextRunTimes")]
    public ActionResult<ApiResponseDto<List<DateTimeOffset>>> GetNextRunTimes(string cronExpression, int count = 10)
    {
        try
        {
            var result = _jobService.GetNextRunTimes(cronExpression, count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("GetNextRunTimes", ex);
            return Ok(ApiResponseDto<List<DateTime>>.ErrorResponse("获取执行时间失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取ClassJob列表
    /// </summary>
    [HttpGet("GetJobClasses")]
    public async Task<ActionResult<ApiResponseDto<List<string>>>> GetJobClasses()
    {
        try
        {
            var result = await _jobService.GetJobClassesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取ClassJob列表", ex);
            return Ok(
                ApiResponseDto<List<string>>.ErrorResponse("获取作业类列表失败: " + ex.Message)
            );
        }
    }
}