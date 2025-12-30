
using Chet.QuartzNet.Core.Attributes;
using Chet.QuartzNet.Core.Extensions;
using Quartz;

namespace Chet.QuartzNet.File.Test.Jobs
{
    /// <summary>
    /// 示例作业类
    /// </summary>
    [QuartzJob("SampleJob", "SmapleExampleGroup", "0 0/5 * * * ?", Description = "这是一个示例作业，每5分钟执行一次")]
    public class SampleJob : IJob
    {
        private readonly ILogger<SampleJob> _logger;

        public SampleJob(ILogger<SampleJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("SampleJob开始执行，执行时间: {ExecuteTime}", DateTime.Now);
            try
            {
                // 模拟业务逻辑处理
                await Task.Delay(1000);

                // 获取作业数据
                var jobDataJson = context.JobDetail.JobDataMap.GetJobDataJson();
                var jobData = context.JobDetail.JobDataMap.GetJobData<SampleParam>();
                if (!string.IsNullOrEmpty(jobDataJson))
                {
                    _logger.LogInformation("获取到作业数据JSON: {JobDataJson}", jobDataJson);

                }

                _logger.LogInformation("SampleJob执行完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SampleJob执行失败");
                throw;
            }
        }
    }
    /// <summary>
    /// 示例1作业类
    /// </summary>
    [QuartzJob("SampleJob1", "SmapleExampleGroup", "0 0/5 * * * ?", Description = "这是一个示例作业，每5分钟执行一次")]
    public class SampleJob1 : IJob
    {
        private readonly ILogger<SampleJob> _logger;

        public SampleJob1(ILogger<SampleJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("SampleJob开始执行，执行时间: {ExecuteTime}", DateTime.Now);

            try
            {
                // 模拟业务逻辑处理
                await Task.Delay(1000);

                // 获取作业数据
                var jobDataJson = context.JobDetail.JobDataMap.GetJobDataJson();
                if (!string.IsNullOrEmpty(jobDataJson))
                {
                    _logger.LogInformation("获取到作业数据JSON: {JobDataJson}", jobDataJson);

                }

                _logger.LogInformation("SampleJob执行完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SampleJob执行失败");
                throw;
            }
        }
    }
    /// <summary>
    /// 示例2作业类
    /// </summary>
    [QuartzJob("SampleJob2", "SmapleExampleGroup", "0 0/5 * * * ?", Description = "这是一个示例作业，每5分钟执行一次")]
    public class SampleJob2 : IJob
    {
        private readonly ILogger<SampleJob> _logger;

        public SampleJob2(ILogger<SampleJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("SampleJob开始执行，执行时间: {ExecuteTime}", DateTime.Now);

            try
            {
                // 模拟业务逻辑处理
                await Task.Delay(1000);

                // 获取作业数据
                var jobDataJson = context.JobDetail.JobDataMap.GetJobDataJson();
                if (!string.IsNullOrEmpty(jobDataJson))
                {
                    _logger.LogInformation("获取到作业数据JSON: {JobDataJson}", jobDataJson);

                }

                _logger.LogInformation("SampleJob执行完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SampleJob执行失败");
                throw;
            }
        }
    }


    /// <summary>
    /// 简单参数
    /// </summary>
    public class SampleParam
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}