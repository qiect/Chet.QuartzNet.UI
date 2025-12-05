using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.UI.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chet.QuartzNet.UI.Extensions;

/// <summary>
/// 应用程序构建器扩展
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 使用QuartzUI中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>应用程序构建器</returns>
    public static IApplicationBuilder UseQuartz(this IApplicationBuilder app)
    {
        // 初始化存储 - 延迟到服务解析时执行
        try
        {
            app.UseAuthentication();
            app.UseAuthorization();
            // 创建作用域来解析IJobStorage（因为它是作用域服务）
            using var scope = app.ApplicationServices.CreateScope();
            var storage = scope.ServiceProvider.GetService<IJobStorage>();
            if (storage != null)
            {
                storage.InitializeAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            // 如果存储初始化失败，记录错误但继续启动
            var logger = app.ApplicationServices.GetService<ILogger<IApplicationBuilder>>();
            logger?.LogWarning(ex, "初始化作业存储失败，将使用默认状态");
        }

        // 启用QuartzUI
        app.UseMiddleware<QuartzUIMiddleware>();

        return app;
    }
}