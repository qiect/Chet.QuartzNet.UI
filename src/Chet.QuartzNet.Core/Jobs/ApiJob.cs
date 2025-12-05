using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Chet.QuartzNet.Core.Jobs;

/// <summary>
/// API作业执行逻辑
/// </summary>
public class ApiJob : IJob
{
    private readonly IJobStorage _jobStorage;
    private readonly ILogger<ApiJob> _logger;
    // 创建一个静态HttpClient实例，避免频繁创建和销毁
    private static readonly HttpClient _httpClient = new HttpClient();
    // 当需要跳过SSL验证时使用的处理程序
    private static readonly HttpClientHandler _sslHandler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
    };
    // 使用处理程序的HttpClient实例（静态复用）
    private static readonly HttpClient _sslHttpClient = new HttpClient(_sslHandler);

    public ApiJob(IJobStorage jobStorage, ILogger<ApiJob> logger)
    {
        _jobStorage = jobStorage;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobName = context.JobDetail.Key.Name;
        var jobGroup = context.JobDetail.Key.Group;

        // 获取作业信息
        var jobInfo = await _jobStorage.GetJobAsync(jobName, jobGroup, context.CancellationToken);
        if (jobInfo == null)
        {
            _logger.LogError("API作业执行失败: 作业信息不存在 - {JobKey}", $"{jobGroup}.{jobName}");
            throw new JobExecutionException("作业信息不存在");
        }

        // 检查作业是否被暂停
        if (jobInfo.Status == JobStatus.Paused)
        {
            _logger.LogWarning("API作业执行被跳过: 作业已被暂停 - {JobKey}", $"{jobGroup}.{jobName}");
            return; // 直接返回，不执行作业
        }

        try
        {
            _logger.LogInformation("开始执行API作业: {JobKey}", $"{jobGroup}.{jobName}");

            // 验证作业类型
            if (jobInfo.JobType != JobTypeEnum.API)
            {
                throw new InvalidOperationException($"作业类型不是API: {jobInfo.JobType}");
            }

            // 验证必要的API信息
            if (string.IsNullOrEmpty(jobInfo.JobClassOrApi)) // JobClassOrApi存储API URL
            {
                throw new InvalidOperationException("API URL不能为空");
            }

            if (string.IsNullOrEmpty(jobInfo.ApiMethod))
            {
                throw new InvalidOperationException("API请求方法不能为空");
            }

            // 选择合适的HttpClient实例，不修改共享实例的属性
            var httpClient = jobInfo.SkipSslValidation && jobInfo.JobClassOrApi.StartsWith("https://")
                ? _sslHttpClient
                : _httpClient;

            // 计算超时时间
            var timeoutSeconds = jobInfo.ApiTimeout > 0 ? jobInfo.ApiTimeout : 60;
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // 创建请求消息
            var request = new HttpRequestMessage(new HttpMethod(jobInfo.ApiMethod.ToUpper()), jobInfo.JobClassOrApi);

            // 添加请求头
            if (!string.IsNullOrEmpty(jobInfo.ApiHeaders))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(jobInfo.ApiHeaders);
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            {
                                if (request.Content != null)
                                {
                                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(header.Value);
                                }
                            }
                            else if (header.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                            {
                                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(header.Value));
                            }
                            else
                            {
                                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析API请求头失败: {JobKey}", $"{jobGroup}.{jobName}");
                }
            }

            // 添加请求体
            if (!string.IsNullOrEmpty(jobInfo.ApiBody))
            {
                request.Content = new StringContent(jobInfo.ApiBody, Encoding.UTF8, "application/json");
            }

            // 使用CancellationTokenSource来设置请求超时，而不是修改HttpClient的Timeout属性
            using var cts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cts.Token);

            // 发送请求
            var response = await httpClient.SendAsync(request, linkedCts.Token);

            // 读取响应内容
            var responseContent = await response.Content.ReadAsStringAsync();

            // 检查响应状态码
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("API作业执行成功: {JobKey}", $"{jobGroup}.{jobName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API作业执行失败: {JobKey}", $"{jobGroup}.{jobName}");
            throw new JobExecutionException(ex);
        }
    }
}