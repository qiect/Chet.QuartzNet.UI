namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// 登录请求DTO
/// </summary>
public class LoginRequestDto
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
