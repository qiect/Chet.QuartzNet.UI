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
public class NotificationController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(IQuartzJobService jobService, ILogger<NotificationController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// 获取PushPlus配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>PushPlus配置</returns>
    [HttpGet("GetPushPlusConfig")]
    public async Task<ActionResult<ApiResponseDto<PushPlusConfigDto>>> GetPushPlusConfig(
        CancellationToken cancellationToken = default
    )
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
                _logger.LogWarn(
                    "获取PushPlus配置",
                    $"获取PushPlus配置失败, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取PushPlus配置", ex);
            return Ok(
                ApiResponseDto<PushPlusConfigDto>.ErrorResponse(
                    "获取PushPlus配置失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 保存PushPlus配置
    /// </summary>
    /// <param name="config">PushPlus配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存结果</returns>
    [HttpPost("SavePushPlusConfig")]
    public async Task<ActionResult<ApiResponseDto<bool>>> SavePushPlusConfig(
        [FromBody] PushPlusConfigDto config,
        CancellationToken cancellationToken = default
    )
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
                _logger.LogWarn(
                    "保存PushPlus配置",
                    $"保存PushPlus配置失败, 原因: {result.Message}"
                );
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
    public async Task<ActionResult<ApiResponseDto<bool>>> SendTestNotification(
        CancellationToken cancellationToken = default
    )
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

    /// <summary>
    /// 获取通知消息列表
    /// </summary>
    /// <param name="queryDto">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息列表</returns>
    [HttpPost("GetNotifications")]
    public async Task<
        ActionResult<ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>>
    > GetNotifications(
        [FromBody] NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
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
                _logger.LogWarn(
                    "获取通知消息列表",
                    $"获取通知消息列表失败, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息列表", ex);
            return Ok(
                ApiResponseDto<PagedResponseDto<QuartzNotificationDto>>.ErrorResponse(
                    "获取通知消息列表失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 获取通知消息详情
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>通知消息详情</returns>
    [HttpGet("GetNotification")]
    public async Task<ActionResult<ApiResponseDto<QuartzNotificationDto>>> GetNotification(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
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
                _logger.LogWarn(
                    "获取通知消息详情",
                    $"获取通知消息详情失败, 通知ID: {notificationId}, 原因: {result.Message}"
                );
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取通知消息详情", ex);
            return Ok(
                ApiResponseDto<QuartzNotificationDto>.ErrorResponse(
                    "获取通知消息详情失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 删除通知消息
    /// </summary>
    /// <param name="notificationId">通知ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    [HttpDelete("DeleteNotification")]
    public async Task<ActionResult<ApiResponseDto<bool>>> DeleteNotification(
        Guid notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _jobService.DeleteNotificationAsync(
                notificationId,
                cancellationToken
            );
            if (result.Success)
            {
                _logger.LogSuccess("删除通知消息", notificationId.ToString());
            }
            else
            {
                _logger.LogWarn(
                    "删除通知消息",
                    $"删除通知消息失败, 通知ID: {notificationId}, 原因: {result.Message}"
                );
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
    public async Task<ActionResult<ApiResponseDto<bool>>> ClearNotifications(
        [FromBody] NotificationQueryDto queryDto,
        CancellationToken cancellationToken = default
    )
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
}