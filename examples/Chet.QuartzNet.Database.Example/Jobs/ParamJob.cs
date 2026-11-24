using Chet.QuartzNet.Core.Attributes;
using Chet.QuartzNet.Core.Extensions;
using Quartz;

namespace Chet.QuartzNet.Database.Example.Jobs
{
    /// <summary>
    /// 带参数的作业
    /// </summary>
    [QuartzJob("ParamJob", "DEFAULT", "0 0/2 * * * ?", Description = "带参数的作业，每2分钟执行一次")]
    public class ParamJob : IJob
    {
        private readonly ILogger<ParamJob> _logger;

        public ParamJob(ILogger<ParamJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("带参数的作业开始执行: {Time}", DateTime.Now);

            // 使用新的作业数据获取方式
            var jobDataJson = context.JobDetail.JobDataMap.GetJobDataJson();
            _logger.LogInformation("作业数据JSON: {JobDataJson}", jobDataJson);

            // 作业未配置参数时 GetJobDataJson 返回 null，直接反序列化会抛 ArgumentNullException
            if (string.IsNullOrEmpty(jobDataJson))
            {
                _logger.LogInformation("作业未配置参数，跳过参数解析");
                await Task.Delay(800);
                _logger.LogInformation("带参数的作业执行完成: {Time}", DateTime.Now);
                return;
            }

            var jobDataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jobDataJson);

            if (jobDataDict != null)
            {
                foreach (var kvp in jobDataDict)
                {
                    _logger.LogInformation("获取到参数: {Key} = {Value}", kvp.Key, kvp.Value);
                }
            }

            await Task.Delay(800);
            _logger.LogInformation("带参数的作业执行完成: {Time}", DateTime.Now);
        }
    }
}