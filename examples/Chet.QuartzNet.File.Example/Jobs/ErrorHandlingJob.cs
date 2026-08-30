using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.File.Example.Jobs
{
    /// <summary>
    /// 错误处理作业
    /// </summary>
    [QuartzJob(
        "ErrorHandlingJob",
        "DEFAULT",
        "0 0/3 * * * ?",
        Description = "错误处理作业，每3分钟执行一次"
    )]
    public class ErrorHandlingJob : IJob
    {
        private readonly ILogger<ErrorHandlingJob> _logger;

        public ErrorHandlingJob(ILogger<ErrorHandlingJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("错误处理作业开始执行: {Time}", DateTime.Now);

            try
            {
                // 模拟业务逻辑
                await Task.Delay(500);

                // 模拟随机异常
                if (DateTime.Now.Second % 2 == 0)
                //if (true)
                {
                    throw new InvalidOperationException("模拟业务异常");
                }

                _logger.LogInformation("错误处理作业执行成功: {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "错误处理作业执行失败: {Time}", DateTime.Now);
                // 可以在这里添加自定义错误处理逻辑，如发送通知等

                // 重新抛出异常，让Quartz处理重试逻辑
                throw;
            }
        }
    }
}
