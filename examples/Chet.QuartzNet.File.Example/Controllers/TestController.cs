using Microsoft.AspNetCore.Mvc;

namespace Chet.QuartzNet.File.Example.Controllers
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 健康检查接口
        /// </summary>
        /// <returns></returns>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.Now });
        }

        /// <summary>
        /// 获取应用信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("info")]
        public IActionResult Info()
        {
            var info = new
            {
                appName = "Quartz.Net Example",
                version = "1.0.0",
                description = "Quartz.Net 可视化管理示例项目",
                features = new[]
                {
                    "可视化管理 Quartz 作业",
                    "ClassJob 模式支持",
                    "文件存储和数据库存储",
                    "Razor Class Library 打包",
                    "无侵入集成",
                    "Basic 简易授权"
                },
                quartzUI = "/quartz-ui"
            };

            return Ok(info);
        }
    }
}