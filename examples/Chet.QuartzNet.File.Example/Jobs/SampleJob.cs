using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.File.Example.Jobs
{
    /// <summary>
    /// 示例作业类
    /// </summary>
    [QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "这是一个示例作业，每5分钟执行一次")]
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
                throw new Exception("模拟异常");
                // 模拟业务逻辑处理
                await Task.Delay(1000);

                // 获取作业数据
                var jobData = context.JobDetail.JobDataMap;
                if (jobData.ContainsKey("customData"))
                {
                    _logger.LogInformation("获取到自定义数据: {CustomData}", jobData.GetString("customData"));
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
    /// 数据同步作业类
    /// </summary>
    [QuartzJob("DataSyncJob", "SYNC", "0 0 2 * * ?", Description = "数据同步作业，每天凌晨2点执行")]
    public class DataSyncJob : IJob
    {
        private readonly ILogger<DataSyncJob> _logger;

        public DataSyncJob(ILogger<DataSyncJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("DataSyncJob开始执行数据同步");

            try
            {
                // 模拟数据同步过程
                await Task.Delay(5000);
                _logger.LogInformation("DataSyncJob数据同步完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSyncJob数据同步失败");
                throw;
            }
        }
    }

    /// <summary>
    /// 报表生成作业类
    /// </summary>
    [QuartzJob("ReportJob", "REPORT", "0 30 8 * * ?", Description = "报表生成作业，每天早上8:30执行")]
    public class ReportJob : IJob
    {
        private readonly ILogger<ReportJob> _logger;

        public ReportJob(ILogger<ReportJob> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("ReportJob开始生成报表");

            try
            {
                // 模拟报表生成过程
                await Task.Delay(3000);
                _logger.LogInformation("ReportJob报表生成完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReportJob报表生成失败");
                throw;
            }
        }
    }
}