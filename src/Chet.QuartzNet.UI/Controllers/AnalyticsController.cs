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
public class AnalyticsController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IQuartzJobService jobService, ILogger<AnalyticsController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取统计分析概览聚合数据（合并作业统计+状态分布+执行趋势+热力图）
    /// </summary>
    [HttpPost("GetAnalyticsOverview")]
    public async Task<ActionResult<ApiResponseDto<AnalyticsOverviewDto>>> GetAnalyticsOverview(
        [FromBody] StatsQueryDto query
    )
    {
        try
        {
            var result = await _jobService.GetAnalyticsOverviewAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取统计分析概览数据", ex);
            return Ok(
                ApiResponseDto<AnalyticsOverviewDto>.ErrorResponse(
                    "获取统计分析概览数据失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取作业性能分析聚合数据（合并健康概览+耗时排行）
    /// </summary>
    [HttpPost("GetAnalyticsJobPerformance")]
    public async Task<
        ActionResult<ApiResponseDto<AnalyticsJobPerformanceDto>>
    > GetAnalyticsJobPerformance([FromBody] JobPerformanceQueryDto query)
    {
        try
        {
            var result = await _jobService.GetAnalyticsJobPerformanceAsync(query, query.TopCount);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业性能分析数据", ex);
            return Ok(
                ApiResponseDto<AnalyticsJobPerformanceDto>.ErrorResponse(
                    "获取作业性能分析数据失败: " + ex.Message
                )
            );
        }
    }
}