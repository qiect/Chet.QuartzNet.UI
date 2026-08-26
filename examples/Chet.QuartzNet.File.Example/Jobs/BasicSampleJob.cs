using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.File.Example.Jobs
{
    /// <summary>
    /// 基础示例作业
    /// </summary>
    [QuartzJob(
        "BasicSampleJob",
        "DEFAULT",
        "0/1 * * * * ?",
        Description = "基础示例作业，每1分钟执行一次"
    )]
    public class BasicSampleJob : IJob
    {
        private readonly ILogger<BasicSampleJob> _logger;

        public BasicSampleJob(ILogger<BasicSampleJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("基础示例作业开始执行: {Time}", DateTime.Now);
            await Task.Delay(10000);
            _logger.LogInformation("基础示例作业执行完成: {Time}", DateTime.Now);
        }
    }
}
