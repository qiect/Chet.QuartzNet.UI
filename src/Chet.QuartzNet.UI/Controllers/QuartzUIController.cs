using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Chet.QuartzNet.UI.Controllers;

/// <summary>
/// QuartzUI管理控制器
/// 提供作业管理的所有API接口
/// </summary>
[Route("api/quartz")]
[ApiController]
[Authorize(Policy = "QuartzUIPolicy")]
public class QuartzUIController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<QuartzUIController> _logger;
    private readonly QuartzUIOptions _quartzUIOptions;

    public QuartzUIController(IQuartzJobService jobService,
                              ILogger<QuartzUIController> logger,
                              IOptions<QuartzUIOptions> quartzUIOptions)
    {
        _jobService = jobService;
        _logger = logger;
        _quartzUIOptions = quartzUIOptions.Value;
    }

    #region 调度器

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
            return Ok(ApiResponseDto<SchedulerStatusDto>.ErrorResponse("获取调度器状态失败: " + ex.Message));
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

    #endregion

    #region 作业管理

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
            _logger.LogFailure("获取作业列表", ex);
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
            _logger.LogFailure("获取作业详情", ex);
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
                _logger.LogSuccess("添加作业", "作业: {jobName}.{jobGroup}", jobDto.JobName, jobDto.JobGroup);
            }
            else
            {
                _logger.LogWarn("添加作业", $"添加作业失败: {jobDto.JobName}.{jobDto.JobGroup}, 原因: {result.Message}");
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
                _logger.LogSuccess("更新作业", "作业: {jobName}.{jobGroup}", jobDto.JobName, jobDto.JobGroup);
            }
            else
            {
                _logger.LogWarn("更新作业", $"更新作业失败: {jobDto.JobName}.{jobDto.JobGroup}, 原因: {result.Message}");
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
                _logger.LogWarn("删除作业", $"删除作业失败: {jobName}.{jobGroup}, 原因: {result.Message}");
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
    public async Task<ActionResult<ApiResponseDto<bool>>> BatchDeleteJobs([FromBody] List<BatchDeleteRequest> jobs)
    {
        try
        {
            // 转换为服务需要的格式
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
                _logger.LogWarn("暂停作业", $"暂停作业失败: {jobName}.{jobGroup}, 原因: {result.Message}");
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
                _logger.LogWarn("恢复作业", $"恢复作业失败: {jobName}.{jobGroup}, 原因: {result.Message}");
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
    public async Task<ActionResult<ApiResponseDto<bool>>> TriggerJob(string jobName, string jobGroup)
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
                _logger.LogFailure("立即触发作业", $"触发作业失败: {jobName}.{jobGroup}, 错误信息: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("立即触发作业", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("触发作业失败: " + ex.Message));
        }
    }

    #endregion

    #region 日志管理

    /// <summary>
    /// 获取作业日志
    /// </summary>
    [HttpPost("GetJobLogs")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>>> GetJobLogs([FromBody] QuartzJobLogQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobLogsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业日志", ex);
            return Ok(ApiResponseDto<PagedResponseDto<QuartzJobLogDto>>.ErrorResponse("获取作业日志失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 清空作业日志
    /// </summary>
    [HttpPost("ClearLogs")]
    public async Task<ActionResult<ApiResponseDto<bool>>> ClearLogs([FromBody] QuartzJobLogQueryDto query)
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

    #endregion

    #region 通知配置

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PushPlus配置</returns>
    [HttpGet("GetPushPlusConfig")]
    public async Task<ActionResult<ApiResponseDto<PushPlusConfigDto>>> GetPushPlusConfig(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.GetPushPlusConfigAsync(cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("获取PushPlus配置");
            }
            else
            {
                _logger.LogWarn("获取PushPlus配置", $"获取PushPlus配置失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取PushPlus配置", ex);
            return Ok(ApiResponseDto<PushPlusConfigDto>.ErrorResponse("获取PushPlus配置失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    /// <param name="config">PushPlus配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    [HttpPost("SavePushPlusConfig")]
    public async Task<ActionResult<ApiResponseDto<bool>>> SavePushPlusConfig([FromBody] PushPlusConfigDto config, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.SavePushPlusConfigAsync(config, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("保存PushPlus配置");
            }
            else
            {
                _logger.LogWarn("保存PushPlus配置", $"保存PushPlus配置失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("保存PushPlus配置", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("保存PushPlus配置失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 发送测试通知
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    [HttpPost("SendTestNotification")]
    public async Task<ActionResult<ApiResponseDto<bool>>> SendTestNotification(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.SendTestNotificationAsync(cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("发送测试通知");
            }
            else
            {
                _logger.LogWarn("发送测试通知", $"发送测试通知失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("发送测试通知", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("发送测试通知失败: " + ex.Message));
        }
    }

    #endregion

    #region 通知消息

    /// <summary>
    /// 获取通知消息列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息列表</returns>
    [HttpPost("GetNotifications")]
    public async Task<ActionResult<ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>>> GetNotifications([FromBody] NotificationQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.GetNotificationsAsync(queryDto, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("获取通知消息列表");
            }
            else
            {
                _logger.LogWarn("获取通知消息列表", $"获取通知消息列表失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息列表", ex);
            return Ok(ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>.ErrorResponse("获取通知消息列表失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取通知消息详情
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息详情</returns>
    [HttpGet("GetNotification")]
    public async Task<ActionResult<ApiResponseDto<QuartzNotificationDto>>> GetNotification(Guid notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.GetNotificationAsync(notificationId, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("获取通知消息详情", notificationId.ToString());
            }
            else
            {
                _logger.LogWarn("获取通知消息详情", $"获取通知消息详情失败, 通知ID: {notificationId}, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息详情", ex);
            return Ok(ApiResponseDto<QuartzNotificationDto>.ErrorResponse("获取通知消息详情失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("DeleteNotification")]
    public async Task<ActionResult<ApiResponseDto<bool>>> DeleteNotification(Guid notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.DeleteNotificationAsync(notificationId, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("删除通知消息", notificationId.ToString());
            }
            else
            {
                _logger.LogWarn("删除通知消息", $"删除通知消息失败, 通知ID: {notificationId}, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("删除通知消息", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("删除通知消息失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 清空通知消息
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>清空结果</returns>
    [HttpPost("ClearNotifications")]
    public async Task<ActionResult<ApiResponseDto<bool>>> ClearNotifications([FromBody] NotificationQueryDto queryDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _jobService.ClearNotificationsAsync(queryDto, cancellationToken);
            if (result.Success)
            {
                _logger.LogSuccess("清空通知消息");
            }
            else
            {
                _logger.LogWarn("清空通知消息", $"清空通知消息失败, 原因: {result.Message}");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("清空通知消息", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("清空通知消息失败: " + ex.Message));
        }
    }

    #endregion

    #region 认证

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("Login")]
    [AllowAnonymous]
    public ActionResult<ApiResponseDto<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            // 验证用户名密码
            if (request.UserName != _quartzUIOptions.UserName || request.Password != _quartzUIOptions.Password)
            {
                _logger.LogWarn("Login", $"登录失败: 用户名或密码错误 - 尝试用户名: {request.UserName}");
                return Ok(ApiResponseDto<LoginResponseDto>.ErrorResponse("用户名或密码错误"));
            }

            // 生成JWT token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_quartzUIOptions.JwtSecret);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, request.UserName),
                new Claim(ClaimTypes.Role, "QuartzUIAdmin")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_quartzUIOptions.JwtExpiresInMinutes),
                Issuer = _quartzUIOptions.JwtIssuer,
                Audience = _quartzUIOptions.JwtAudience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 构造响应
            var response = new LoginResponseDto
            {
                AccessToken = tokenString,
                TokenType = "Bearer",
                ExpiresIn = _quartzUIOptions.JwtExpiresInMinutes * 60,
                UserName = request.UserName
            };

            _logger.LogSuccess("Login", $"用户名: {request.UserName}");
            return Ok(ApiResponseDto<LoginResponseDto>.SuccessResponse(response, "登录成功"));
        }
        catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("IDX10653") || ex.Message.Contains("IDX10720"))
        {
            _logger.LogFailure("Login", ex);
            return BadRequest(new { message = "JWT 密钥配置错误，请联系管理员" });
        }
        catch (Exception ex)
        {
            _logger.LogFailure("Login", ex);
            return Ok(ApiResponseDto<LoginResponseDto>.ErrorResponse("登录失败: " + ex.Message));
        }
    }

    #endregion

    #region 统计分析

    /// <summary>
    /// 获取作业统计数据
    /// </summary>
    [HttpPost("GetJobStats")]
    public async Task<ActionResult<ApiResponseDto<JobStatsDto>>> GetJobStats([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobStatsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业统计数据", ex);
            return Ok(ApiResponseDto<JobStatsDto>.ErrorResponse("获取作业统计数据失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业状态分布数据
    /// </summary>
    [HttpPost("GetJobStatusDistribution")]
    public async Task<ActionResult<ApiResponseDto<List<JobStatusDistributionDto>>>> GetJobStatusDistribution([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobStatusDistributionAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业状态分布数据", ex);
            return Ok(ApiResponseDto<List<JobStatusDistributionDto>>.ErrorResponse("获取作业状态分布数据失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业执行趋势数据
    /// </summary>
    [HttpPost("GetJobExecutionTrend")]
    public async Task<ActionResult<ApiResponseDto<List<JobExecutionTrendDto>>>> GetJobExecutionTrend([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobExecutionTrendAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业执行趋势数据", ex);
            return Ok(ApiResponseDto<List<JobExecutionTrendDto>>.ErrorResponse("获取作业执行趋势数据失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业类型分布数据
    /// </summary>
    [HttpPost("GetJobTypeDistribution")]
    public async Task<ActionResult<ApiResponseDto<List<JobTypeDistributionDto>>>> GetJobTypeDistribution([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobTypeDistributionAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业类型分布数据", ex);
            return Ok(ApiResponseDto<List<JobTypeDistributionDto>>.ErrorResponse("获取作业类型分布数据失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取作业执行耗时数据
    /// </summary>
    [HttpPost("GetJobExecutionTime")]
    public async Task<ActionResult<ApiResponseDto<List<JobExecutionTimeDto>>>> GetJobExecutionTime([FromBody] StatsQueryDto query)
    {
        try
        {
            var result = await _jobService.GetJobExecutionTimeAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取作业执行耗时数据", ex);
            return Ok(ApiResponseDto<List<JobExecutionTimeDto>>.ErrorResponse("获取作业执行耗时数据失败: " + ex.Message));
        }
    }

    #endregion

    #region 扩展

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
                _logger.LogWarn("ValidateCronExpression", $"验证Cron表达式失败: {cronExpression}, 原因: {result.Message}");
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
            return Ok(ApiResponseDto<List<string>>.ErrorResponse("获取作业类列表失败: " + ex.Message));
        }
    }

    #endregion
}