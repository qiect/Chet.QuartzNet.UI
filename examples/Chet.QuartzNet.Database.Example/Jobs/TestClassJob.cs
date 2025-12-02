using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.Database.Example.Jobs
{
    /// <summary>
    /// 测试ClassJob实现
    /// </summary>
    [QuartzJob("TestClassJob", "ExampleGroup", "0/10 * * * * ?", "测试ClassJob，每10秒执行一次")]
    public class TestClassJob : IJob
    {
        private readonly ILogger<TestClassJob> _logger;

        /// <summary>
        /// 构造函数，注入日志记录器
        /// </summary>
        /// <param name="logger"></param>
        public TestClassJob(ILogger<TestClassJob> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 作业执行方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("TestClassJob 开始执行: {Time}", DateTime.Now);

            try
            {
                // 从作业数据中获取参数
                var jobDataMap = context.JobDetail.JobDataMap;
                if (jobDataMap.ContainsKey("TestParam"))
                {
                    var testParam = jobDataMap.GetString("TestParam");
                    _logger.LogInformation("TestClassJob 获取到参数: {Param}", testParam);
                }

                // 模拟作业执行逻辑
                await Task.Delay(1000);

                _logger.LogInformation("TestClassJob 执行完成: {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestClassJob 执行失败: {Time}", DateTime.Now);
                throw;
            }
        }
    }
}