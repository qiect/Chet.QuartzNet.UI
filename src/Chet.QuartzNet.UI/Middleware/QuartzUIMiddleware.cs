using Chet.QuartzNet.Core.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.IO;

namespace Chet.QuartzNet.UI.Middleware;

/// <summary>
/// Quartz UI中间件
/// 处理UI页面的请求和静态资源服务
/// </summary>
public class QuartzUIMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly IFileProvider _fileProvider;
    private readonly QuartzUIOptions _options;

    public QuartzUIMiddleware(RequestDelegate next, IWebHostEnvironment env, IOptions<QuartzUIOptions> options)
    {
        _next = next;
        _env = env;
        _options = options.Value;

        // 获取嵌入的程序集文件提供程序
        var assembly = Assembly.GetExecutingAssembly();
        var embeddedFileNamespace = "Chet.QuartzNet.UI.wwwroot";
        _fileProvider = new EmbeddedFileProvider(assembly, embeddedFileNamespace);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (path?.StartsWith("/quartz-ui") == true)
        {

            // 处理根路径，返回主页面
            if (path == "/quartz-ui" || path == "/quartz-ui/")
            {
                await ServeQuartzUIIndexPage(context);
                return;
            }

            // 处理静态资源请求
            if (path.StartsWith("/quartz-ui/"))
            {
                var filePath = path.Substring("/quartz-ui/".Length);
                if (string.IsNullOrEmpty(filePath))
                {
                    await ServeQuartzUIIndexPage(context);
                    return;
                }

                await ServeQuartzUIStaticFile(context, filePath);
                return;
            }
        }

        // 其他请求继续传递
        await _next(context);
    }

    /// <summary>
    /// 提供quartz-ui主页面
    /// </summary>
    private async Task ServeQuartzUIIndexPage(HttpContext context)
    {
        try
        {
            var indexFile = _fileProvider.GetFileInfo("quartz_ui.index.html");
            if (indexFile.Exists)
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                using var stream = indexFile.CreateReadStream();
                using var reader = new StreamReader(stream);
                var html = await reader.ReadToEndAsync();

                // 替换基础路径
                html = html.Replace("{{BASE_PATH}}", "/quartz-ui");

                await context.Response.WriteAsync(html);
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("quartz-ui页面未找到");
            }
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("加载页面失败");
        }
    }

    /// <summary>
    /// 提供quartz-ui静态文件
    /// </summary>
    private async Task ServeQuartzUIStaticFile(HttpContext context, string filePath)
    {
        try
        {
            var fileInfo = _fileProvider.GetFileInfo($"quartz_ui.{filePath}");
            if (!fileInfo.Exists)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("文件未找到");
                return;
            }

            // 设置正确的MIME类型
            var contentType = GetContentType(filePath);
            context.Response.ContentType = contentType;

            // 设置缓存头
            context.Response.Headers["Cache-Control"] = "public, max-age=3600";
            context.Response.Headers["ETag"] = $"\"{fileInfo.LastModified.Ticks}\"";

            using var stream = fileInfo.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("加载文件失败");
        }
    }
    /// <summary>
    /// 根据文件扩展名获取MIME类型
    /// </summary>
    private string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            _ => "application/octet-stream"
        };
    }
}