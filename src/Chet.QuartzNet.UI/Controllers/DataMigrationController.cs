using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Controllers;

/// <summary>
/// 数据迁移控制器
/// 支持文件、MySQL、PostgreSQL、SQLite、SQL Server 之间的互转
/// </summary>
[Route("api/quartz")]
[ApiController]
[Authorize(Policy = "QuartzUIPolicy")]
public class DataMigrationController : ControllerBase
{
    private readonly StorageMigrationService _migrationService;
    private readonly ILogger<DataMigrationController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DataMigrationController(
        StorageMigrationService migrationService,
        ILogger<DataMigrationController> logger
    )
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取数据迁移状态（兼容旧接口）
    /// </summary>
    [HttpGet("GetMigrationStatus")]
    [HttpGet("StorageMigration/GetStatus")]
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
    /// 获取迁移可选项（存储类型、配置档、Provider 安装状态等）
    /// </summary>
    [HttpGet("GetMigrationOptions")]
    [HttpGet("StorageMigration/GetOptions")]
    public ActionResult<ApiResponseDto<MigrationOptionsDto>> GetMigrationOptions()
    {
        try
        {
            var options = _migrationService.GetOptions();
            return Ok(ApiResponseDto<MigrationOptionsDto>.SuccessResponse(options));
        }
        catch (Exception ex)
        {
            _logger.LogFailure("获取迁移选项", ex);
            return Ok(
                ApiResponseDto<MigrationOptionsDto>.ErrorResponse(
                    "获取迁移选项失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 迁移预检，返回源/目标数量与预计写入量
    /// </summary>
    [HttpPost("PreviewMigration")]
    [HttpPost("StorageMigration/Preview")]
    public async Task<ActionResult<ApiResponseDto<MigrationPreviewDto>>> PreviewMigration(
        [FromBody] TriggerMigrationRequestDto? request = null
    )
    {
        try
        {
            var preview = await _migrationService.PreviewAsync(request, HttpContext.RequestAborted);
            return Ok(ApiResponseDto<MigrationPreviewDto>.SuccessResponse(preview));
        }
        catch (Exception ex)
        {
            _logger.LogFailure("迁移预检", ex);
            return Ok(
                ApiResponseDto<MigrationPreviewDto>.ErrorResponse(
                    "迁移预检失败: " + ex.Message
                )
            );
        }
    }

    /// <summary>
    /// 触发数据迁移（兼容旧接口，默认文件存储迁移到当前数据库）
    /// </summary>
    [HttpPost("TriggerMigration")]
    [HttpPost("StorageMigration/Trigger")]
    public ActionResult<ApiResponseDto<bool>> TriggerMigration(
        [FromBody] TriggerMigrationRequestDto? request = null
    )
    {
        try
        {
            var result = _migrationService.Start(request);
            if (result.Started)
            {
                _logger.LogSuccess("触发数据迁移");
                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "迁移任务已触发"));
            }

            return Ok(
                ApiResponseDto<bool>.ErrorResponse(
                    result.Error ?? "无法触发迁移任务"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogFailure("触发数据迁移", ex);
            return Ok(ApiResponseDto<bool>.ErrorResponse("触发迁移失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 取消正在运行的迁移任务
    /// </summary>
    [HttpPost("CancelMigration")]
    [HttpPost("StorageMigration/Cancel")]
    public ActionResult<ApiResponseDto<bool>> CancelMigration()
    {
        var cancelled = _migrationService.Cancel();
        return Ok(
            cancelled
                ? ApiResponseDto<bool>.SuccessResponse(true, "已请求取消迁移")
                : ApiResponseDto<bool>.ErrorResponse("当前没有正在运行的迁移任务")
        );
    }
}
