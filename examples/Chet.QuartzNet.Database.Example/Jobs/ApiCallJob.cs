using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.Database.Example.Jobs
{
    /// <summary>
    /// API调用作业
    /// </summary>
    [QuartzJob("ApiCallJob", "API", "0 0/5 * * * ?", Description = "API调用作业，每5分钟执行一次，演示HTTP请求")]
    public class ApiCallJob : IJob
    {
        private readonly ILogger<ApiCallJob> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ApiCallJob(ILogger<ApiCallJob> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("API调用作业开始执行: {Time}", DateTime.Now);

            try
            {
                // 从工厂获取HttpClient：Handler由工厂池化并定期轮换，避免静态连接长期不刷新DNS
                var httpClient = _httpClientFactory.CreateClient();

                // 调用示例API
                var response = await httpClient.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("API调用成功，响应内容: {Content}", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API调用失败: {Time}", DateTime.Now);
                throw;
            }
        }
    }
}