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
    /// 获取作业统计数据
    /// </summary>
    [HttpPost("GetJobStats")]
    public async Task<ActionResult<ApiResponseDto<JobStatsDto>>> GetJobStats(
        [FromBody] StatsQueryDto query
    )
    {
        try
        {
            var result = await _jobService.GetJobStatsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业统计数据", ex);
            return Ok(
                ApiResponseDto<JobStatsDto>.ErrorResponse("获取作业统计数据失败: " + ex.Message)
            );
        }
    }

    /// <summary>
    /// 获取作业状态分布数据
    /// </summary>
    [HttpPost("GetJobStatusDistribution")]
    public async Task<
        ActionResult<ApiResponseDto<List<JobStatusDistributionDto>>>
    > GetJobStatusDistribution([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobStatusDistributionAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业状态分布数据", ex);
            return Ok(
                ApiResponseDto<List<JobStatusDistributionDto>>.ErrorResponse(
                    "获取作业状态分布数据失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取作业执行趋势数据
    /// </summary>
    [HttpPost("GetJobExecutionTrend")]
    public async Task<
        ActionResult<ApiResponseDto<List<JobExecutionTrendDto>>>
    > GetJobExecutionTrend([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobExecutionTrendAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业执行趋势数据", ex);
            return Ok(
                ApiResponseDto<List<JobExecutionTrendDto>>.ErrorResponse(
                    "获取作业执行趋势数据失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取作业健康概览数据
    /// </summary>
    [HttpPost("GetJobHealthOverview")]
    public async Task<
        ActionResult<ApiResponseDto<List<JobHealthDto>>>
    > GetJobHealthOverview([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobHealthOverviewAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业健康概览数据", ex);
            return Ok(
                ApiResponseDto<List<JobHealthDto>>.ErrorResponse(
                    "获取作业健康概览数据失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取作业执行热力图数据
    /// </summary>
    [HttpPost("GetJobExecutionHeatmap")]
    public async Task<
        ActionResult<ApiResponseDto<List<JobExecutionHeatmapDto>>>
    > GetJobExecutionHeatmap([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobExecutionHeatmapAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业执行热力图数据", ex);
            return Ok(
                ApiResponseDto<List<JobExecutionHeatmapDto>>.ErrorResponse(
                    "获取作业执行热力图数据失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取耗时基线分析数据
    /// </summary>
    [HttpPost("GetTopSlowJobs")]
    public async Task<
        ActionResult<ApiResponseDto<List<TopSlowJobDto>>>
    > GetTopSlowJobs([FromBody] StatsQueryDto query, int topCount = 10)
    {
        try
        {
            var result = await _jobService.GetTopSlowJobsAsync(query, topCount);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取耗时基线分析数据", ex);
            return Ok(
                ApiResponseDto<List<TopSlowJobDto>>.ErrorResponse(
                    "获取耗时基线分析数据失败: " + ex.Message
                )
            );
        }
    }
}