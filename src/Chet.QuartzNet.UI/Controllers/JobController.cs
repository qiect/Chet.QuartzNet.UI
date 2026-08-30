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
public class JobController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<JobController> _logger;

    public JobController(IQuartzJobService jobService, ILogger<JobController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取作业列表
    /// </summary>
    [HttpPost("GetJobs")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>>> GetJobs(
        [FromBody] QuartzJobQueryDto query
    )
    {
        try
        {
            var jobs = await _jobService.GetJobsAsync(query);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业列表", ex);
            return Ok(
                ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.ErrorResponse(
                    "获取作业列表失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取作业详情
    /// </summary>
    [HttpGet("GetJob")]
    public async Task<ActionResult<ApiResponseDto<QuartzJobResponseDto>>> GetJob(
        string jobName,
        string jobGroup
    )
    {
        try
        {
            var job = await _jobService.GetJobDetailAsync(jobName, jobGroup);
            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业详情", ex);
            return Ok(
                ApiResponseDto<QuartzJobResponseDto>.ErrorResponse(
                    "获取作业详情失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 添加作业
    /// </summary>
    [HttpPost("AddJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> AddJob([FromBody] QuartzJobDto jobDto)
    {
        try
        {
            var result = await _jobService.AddJobAsync(jobDto);
            if (result.Success)
            {
                _logger.LogSuccess(
                    "添加作业",
                    "作业: {jobName}.{jobGroup}",
                    jobDto.JobName,
                    jobDto.JobGroup
                );
            }
            else
            {
                _logger.LogWarn(
                    "添加作业",
                    $"添加作业失败: {jobDto.JobName}.{jobDto.JobGroup}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("添加作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("添加作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 更新作业
    /// </summary>
    [HttpPut("UpdateJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> UpdateJob([FromBody] QuartzJobDto jobDto)
    {
        try
        {
            var result = await _jobService.UpdateJobAsync(jobDto);
            if (result.Success)
            {
                _logger.LogSuccess(
                    "更新作业",
                    "作业: {jobName}.{jobGroup}",
                    jobDto.JobName,
                    jobDto.JobGroup
                );
            }
            else
            {
                _logger.LogWarn(
                    "更新作业",
                    $"更新作业失败: {jobDto.JobName}.{jobDto.JobGroup}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("更新作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("更新作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 删除作业
    /// </summary>
    [HttpDelete("DeleteJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> DeleteJob(string jobName, string jobGroup)
    {
        try
        {
            var result = await _jobService.DeleteJobAsync(jobName, jobGroup);
            if (result.Success)
            {
                _logger.LogSuccess("删除作业", $"作业: {jobName}.{jobGroup}");
            }
            else
            {
                _logger.LogWarn(
                    "删除作业",
                    $"删除作业失败: {jobName}.{jobGroup}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("删除作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("删除作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 批量删除作业
    /// </summary>
    [HttpPost("BatchDeleteJobs")]
    public async Task<ActionResult<ApiResponseDto<bool>>> BatchDeleteJobs(
        [FromBody] List<BatchDeleteRequest> jobs
    )
    {
        try
        {
            var jobTuples = jobs.Select(j => (j.JobName, j.JobGroup)).ToList();
            var result = await _jobService.BatchDeleteJobsAsync(jobTuples);
            if (result.Success)
            {
                _logger.LogSuccess("批量删除作业", $"批量删除作业成功，共删除 {jobs.Count} 个作业");
            }
            else
            {
                _logger.LogWarn("批量删除作业", $"批量删除作业失败: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("批量删除作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("批量删除作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 暂停作业
    /// </summary>
    [HttpPost("PauseJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> PauseJob(string jobName, string jobGroup)
    {
        try
        {
            var result = await _jobService.PauseJobAsync(jobName, jobGroup);
            if (result.Success)
            {
                _logger.LogSuccess("暂停作业", $"作业: {jobName}.{jobGroup}");
            }
            else
            {
                _logger.LogWarn(
                    "暂停作业",
                    $"暂停作业失败: {jobName}.{jobGroup}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("暂停作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("暂停作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 恢复作业
    /// </summary>
    [HttpPost("ResumeJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> ResumeJob(string jobName, string jobGroup)
    {
        try
        {
            var result = await _jobService.ResumeJobAsync(jobName, jobGroup);
            if (result.Success)
            {
                _logger.LogSuccess("恢复作业", $"作业: {jobName}.{jobGroup}");
            }
            else
            {
                _logger.LogWarn(
                    "恢复作业",
                    $"恢复作业失败: {jobName}.{jobGroup}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("恢复作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("恢复作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 立即触发作业
    /// </summary>
    [HttpPost("TriggerJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> TriggerJob(
        string jobName,
        string jobGroup
    )
    {
        try
        {
            var result = await _jobService.TriggerJobAsync(jobName, jobGroup);
            if (result.Success)
            {
                _logger.LogSuccess("立即触发作业", $"作业: {jobName}.{jobGroup}");
            }
            else
            {
                _logger.LogFailure(
                    "立即触发作业",
                    $"触发作业失败: {jobName}.{jobGroup}, 错误信息: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("立即触发作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("触发作业失败: " + ex.Message));
        }
    }
}