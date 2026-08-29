using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chet.QuartzNet.UI.Controllers;

[Route("api/quartz")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IQuartzJobService _jobService;
    private readonly ILogger<AuthController> _logger;
    private readonly QuartzUIOptions _quartzUIOptions;

    public AuthController(
        IQuartzJobService jobService,
        ILogger<AuthController> logger,
        IOptions<QuartzUIOptions> quartzUIOptions
    )
    {
        _jobService = jobService;
        _logger = logger;
        _quartzUIOptions = quartzUIOptions.Value;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    [HttpPost("Login")]
    [AllowAnonymous]
    public ActionResult<ApiResponseDto<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            if (
                request.UserName != _quartzUIOptions.UserName
                || request.Password != _quartzUIOptions.Password
            )
            {
                _logger.LogWarn(
                    "Login",
                    $"登录失败: 用户名或密码错误 - 尝试用户名: {request.UserName}"
                );
                return Ok(ApiResponseDto<LoginResponseDto>.ErrorResponse("用户名或密码错误"));
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_quartzUIOptions.JwtSecret);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, request.UserName),
                new Claim(ClaimTypes.Role, "QuartzUIAdmin"),
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_quartzUIOptions.JwtExpiresInMinutes),
                Issuer = _quartzUIOptions.JwtIssuer,
                Audience = _quartzUIOptions.JwtAudience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            var response = new LoginResponseDto
            {
                AccessToken = tokenString,
                TokenType = "Bearer",
                ExpiresIn = _quartzUIOptions.JwtExpiresInMinutes * 60,
                UserName = request.UserName,
            };

            _logger.LogSuccess("Login", $"用户名: {request.UserName}");
            return Ok(ApiResponseDto<LoginResponseDto>.SuccessResponse(response, "登录成功"));
        }
        catch (ArgumentOutOfRangeException ex)
            when (ex.Message.Contains("IDX10653") || ex.Message.Contains("IDX10720"))
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
}