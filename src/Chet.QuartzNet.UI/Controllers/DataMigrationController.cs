using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Controllers;

[Route("api/quartz")]
[ApiController]
[Authorize(Policy = "QuartzUIPolicy")]
public class DataMigrationController : ControllerBase
{
    private readonly FileDataMigrationService _migrationService;
    private readonly ILogger<DataMigrationController> _logger;

    public DataMigrationController(
        FileDataMigrationService migrationService,
        ILogger<DataMigrationController> logger
    )
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取数据迁移状态
    /// </summary>
    [HttpGet("GetMigrationStatus")]
    public ActionResult<ApiResponseDto<DataMigrationStatusDto>> GetMigrationStatus()
    {
        try
        {
            var status = _migrationService.GetStatus();
            return Ok(ApiResponseDto<DataMigrationStatusDto>.SuccessResponse(status));
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取迁移状态", ex);
            return Ok(
                ApiResponseDto<DataMigrationStatusDto>.ErrorResponse(
                    "获取迁移状态失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 触发文件数据迁移到数据库
    /// </summary>
    [HttpPost("TriggerMigration")]
    public ActionResult<ApiResponseDto<bool>> TriggerMigration(
        [FromBody] TriggerMigrationRequestDto? request = null
    )
    {
        try
        {
            var force = request?.Force ?? false;
            var triggered = _migrationService.TriggerMigration(force);

            if (triggered)
            {
                _logger.LogSuccess("触发文件数据迁移");
                return Ok(
                    ApiResponseDto<bool>.SuccessResponse(true, "迁移任务已触发")
                );
            }

            var status = _migrationService.GetStatus();
            if (status.IsRunning)
            {
                return Ok(
                    ApiResponseDto<bool>.ErrorResponse("迁移任务正在运行中，请稍后再试")
                );
            }

            if (status.IsCompleted && status.IsSuccess)
            {
                return Ok(
                    ApiResponseDto<bool>.ErrorResponse(
                        "迁移已完成，如需重新迁移请点击「强制重新迁移」按钮"
                    )
                );
            }

            return Ok(ApiResponseDto<bool>.ErrorResponse("无法触发迁移任务"));
        }
        catch (Exception ex)
        {
            _logger.LogFailure("触发文件数据迁移", ex);
            return Ok(
                ApiResponseDto<bool>.ErrorResponse("触发迁移失败: " + ex.Message)
            );
        }
    }
}