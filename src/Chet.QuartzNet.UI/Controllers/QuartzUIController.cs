using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Controllers;

/// <summary>
/// QuartzUI管理控制器
/// 提供作业管理的所有API接口
/// </summary>
[Route("api/quartz")]
[ApiController]
public class QuartzUIController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<QuartzUIController> _logger;
    private readonly IEmailNotificationService _emailService;

    public QuartzUIController(IQuartzJobService jobService, ILogger<QuartzUIController> logger, IEmailNotificationService emailService)
    {
        _jobService = jobService;
        _logger = logger;
        _emailService = emailService;
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
            _logger.LogError(ex, "获取调度器状态失败");
            return Ok(ApiResponseDto<SchedulerStatusDto>.ErrorResponse("获取调度器状态失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业列表
    /// </summary>
    [HttpPost("GetJobs")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>>> GetJobs([FromBody] QuartzJobQueryDto query)
    {
        try
        {
            var jobs = await _jobService.GetJobsAsync(query);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业列表失败");
            return Ok(ApiResponseDto<PagedResponseDto<QuartzJobResponseDto>>.ErrorResponse("获取作业列表失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业详情
    /// </summary>
    [HttpGet("GetJob")]
    public async Task<ActionResult<ApiResponseDto<QuartzJobResponseDto>>> GetJob(string jobName, string jobGroup)
    {
        try
        {
            var job = await _jobService.GetJobDetailAsync(jobName, jobGroup);
            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业详情失败: {JobName}/{JobGroup}", jobName, jobGroup);
            return Ok(ApiResponseDto<QuartzJobResponseDto>.ErrorResponse("获取作业详情失败: " + ex.Message));
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
                _logger.LogInformation("添加作业成功: {JobName}/{JobGroup}", jobDto.JobName, jobDto.JobGroup);
            }
            else
            {
                _logger.LogWarning("添加作业失败: {JobName}/{JobGroup}, 原因: {Message}", jobDto.JobName, jobDto.JobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "添加作业失败: {JobName}/{JobGroup}", jobDto.JobName, jobDto.JobGroup);
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
                _logger.LogInformation("更新作业成功: {JobName}/{JobGroup}", jobDto.JobName, jobDto.JobGroup);
            }
            else
            {
                _logger.LogWarning("更新作业失败: {JobName}/{JobGroup}, 原因: {Message}", jobDto.JobName, jobDto.JobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新作业失败: {JobName}/{JobGroup}", jobDto.JobName, jobDto.JobGroup);
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
                _logger.LogInformation("删除作业成功: {JobName}/{JobGroup}", jobName, jobGroup);
            }
            else
            {
                _logger.LogWarning("删除作业失败: {JobName}/{JobGroup}, 原因: {Message}", jobName, jobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除作业失败: {JobName}/{JobGroup}", jobName, jobGroup);
            return Ok(ApiResponseDto<bool>.ErrorResponse("删除作业失败: " + ex.Message));
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
                _logger.LogInformation("暂停作业成功: {JobName}/{JobGroup}", jobName, jobGroup);
            }
            else
            {
                _logger.LogWarning("暂停作业失败: {JobName}/{JobGroup}, 原因: {Message}", jobName, jobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停作业失败: {JobName}/{JobGroup}", jobName, jobGroup);
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
                _logger.LogInformation("恢复作业成功: {JobName}/{JobGroup}", jobName, jobGroup);
            }
            else
            {
                _logger.LogWarning("恢复作业失败: {JobName}/{JobGroup}, 原因: {Message}", jobName, jobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复作业失败: {JobName}/{JobGroup}", jobName, jobGroup);
            return Ok(ApiResponseDto<bool>.ErrorResponse("恢复作业失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 立即触发作业
    /// </summary>
    [HttpPost("TriggerJob")]
    public async Task<ActionResult<ApiResponseDto<bool>>> TriggerJob(string jobName, string jobGroup)
    {
        try
        {
            var result = await _jobService.TriggerJobAsync(jobName, jobGroup);
            if (result.Success)
            {
                _logger.LogInformation("触发作业成功: {JobName}/{JobGroup}", jobName, jobGroup);
            }
            else
            {
                _logger.LogError("触发作业失败: {JobName}/{JobGroup}, 错误信息: {ErrorMessage}", jobName, jobGroup, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发作业失败: {JobName}/{JobGroup}", jobName, jobGroup);
            return Ok(ApiResponseDto<bool>.ErrorResponse("触发作业失败: " + ex.Message));
        }
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
                _logger.LogInformation("验证Cron表达式成功: {CronExpression}", cronExpression);
            }
            else
            {
                _logger.LogWarning("验证Cron表达式失败: {CronExpression}, 原因: {Message}", cronExpression, result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证Cron表达式失败: {CronExpression}", cronExpression);
            return Ok(ApiResponseDto<bool>.ErrorResponse("验证Cron表达式失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取所有实现了IJob接口的类名列表
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
            _logger.LogError(ex, "获取作业类列表失败");
            return Ok(ApiResponseDto<List<string>>.ErrorResponse("获取作业类列表失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业日志
    /// </summary>
    [HttpGet("GetJobLogs")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>>> GetJobLogs(
        string? jobName = null,
        string? jobGroup = null,
        LogStatus? status = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int pageIndex = 1,
        int pageSize = 20,
        string? sortBy = null,
        string? sortOrder = null)
    {
        try
        {
            var result = await _jobService.GetJobLogsAsync(
                jobName, jobGroup, status, startTime, endTime, pageIndex, pageSize, sortBy, sortOrder);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取作业日志失败");
            return Ok(ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>.ErrorResponse("获取作业日志失败: " + ex.Message));
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
                _logger.LogInformation("调度器启动成功");
            }
            else
            {
                _logger.LogWarning("调度器启动失败, 原因: {Message}", result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动调度器失败");
            return Ok(ApiResponseDto<bool>.ErrorResponse("启动调度器失败: " + ex.Message));
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
                _logger.LogInformation("调度器停止成功");
            }
            else
            {
                _logger.LogWarning("调度器停止失败, 原因: {Message}", result.Message);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止调度器失败");
            return Ok(ApiResponseDto<bool>.ErrorResponse("停止调度器失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 测试邮件配置
    /// </summary>
    [HttpPost("TestEmailConfiguration")]
    public async Task<ActionResult<ApiResponseDto<bool>>> TestEmailConfiguration()
    {
        try
        {
            var result = await _emailService.TestEmailConfigurationAsync();
            if (result)
            {
                _logger.LogInformation("邮件配置测试成功");
                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "邮件配置测试成功，请检查收件箱"));
            }
            else
            {
                _logger.LogWarning("邮件配置测试失败");
                return Ok(ApiResponseDto<bool>.ErrorResponse("邮件配置测试失败，请检查邮件配置"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件配置测试失败");
            return Ok(ApiResponseDto<bool>.ErrorResponse("邮件配置测试失败: " + ex.Message));
        }
    }
}