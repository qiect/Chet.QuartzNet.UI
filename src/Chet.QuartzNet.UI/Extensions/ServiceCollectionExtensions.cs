using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Services;
using Chet.QuartzNet.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Chet.QuartzNet.UI.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    private static ILogger? _logger = null;

    /// <summary>
    /// 添加QuartzUI服务（文件存储版本）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzUI(this IServiceCollection services, Action<QuartzUIOptions>? configureOptions = null)
    {
        // 配置选项
        services.Configure<QuartzUIOptions>(options =>
        {
            options.StorageType = StorageType.File;
            configureOptions?.Invoke(options);
        });

        // 注册EmailOptions为独立服务，供EmailNotificationService使用
        services.AddSingleton(provider =>
        {
            var quartzUIOptions = provider.GetRequiredService<IOptions<QuartzUIOptions>>().Value;
            return quartzUIOptions.EmailOptions;
        });

        // 注册文件存储
        services.TryAddScoped<IJobStorage, FileJobStorage>();
        // 注册Quartz服务
        services.TryAddScoped<IQuartzJobService, QuartzJobService>();
        // 注册邮件通知服务
        services.TryAddScoped<IEmailNotificationService, EmailNotificationService>();
        // 注册作业监听器
        services.TryAddScoped<QuartzJobListener>();
        // 注册作业类扫描器
        services.TryAddSingleton<JobClassScanner>();
        // 注册邮件通知服务
        services.TryAddScoped<IEmailNotificationService, EmailNotificationService>();
        // 注册作业监听器
        services.TryAddScoped<QuartzJobListener>();
        // 注册作业类扫描器
        services.TryAddSingleton<JobClassScanner>();
        // 注册Quartz
        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = 10;
            });

            // 注册作业监听器
            q.AddJobListener<QuartzJobListener>(GroupMatcher<JobKey>.AnyGroup());
        });

        // 注册IScheduler服务
        services.AddSingleton<IScheduler>(provider =>
        {
            var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
            return schedulerFactory.GetScheduler().Result;
        });
        // 注册QuartzHostedService
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });
        // 注册作业调度初始化服务，用于在应用启动时将存储中的作业重新调度到Quartz调度器
        services.AddHostedService<JobSchedulerInitializer>();

        return services;
    }

    /// <summary>
    /// 添加QuartzUI服务（数据库版本）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzUI(this IServiceCollection services, Action<QuartzUIOptions, IServiceProvider> configureOptions)
    {
        services.Configure<QuartzUIOptions>(options =>
        {
            options.StorageType = StorageType.Database;
        });

        // 应用额外的配置
        services.PostConfigure<QuartzUIOptions>(options =>
        {
            configureOptions?.Invoke(options, services.BuildServiceProvider());
        });

        // 注册EmailOptions为独立服务，供EmailNotificationService使用
        services.AddSingleton(provider =>
        {
            var quartzUIOptions = provider.GetRequiredService<IOptions<QuartzUIOptions>>().Value;
            return quartzUIOptions.EmailOptions;
        });

        // 注册Quartz服务
        services.TryAddScoped<IQuartzJobService, QuartzJobService>();

        // 注册Quartz
        services.AddQuartz(q =>
        {
            // q.UseMicrosoftDependencyInjectionJobFactory(); // 已过时，默认就是MicrosoftDependencyInjectionJobFactory
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = 10;
            });

            // 注册作业监听器
            q.AddJobListener<QuartzJobListener>(GroupMatcher<JobKey>.AnyGroup());
        });

        // 注册IScheduler服务
        services.AddSingleton<IScheduler>(provider =>
        {
            var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
            return schedulerFactory.GetScheduler().Result;
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }

    /// <summary>
    /// 添加ClassJob支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzClassJobs(this IServiceCollection services)
    {
        // 获取或创建日志记录器
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger("QuartzClassJobRegistration");

        _logger.LogInformation("开始扫描标记了QuartzJobAttribute特性的ClassJob");

        // 获取所有程序集
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        _logger.LogInformation("找到 {AssemblyCount} 个程序集", assemblies.Length);

        // 扫描并注册所有标记了QuartzJobAttribute特性的类
        var jobTypes = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogWarning(ex, "无法加载程序集 {AssemblyName} 中的类型", a.FullName);
                    return ex.Types.Where(t => t != null);
                }
            })
            .Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && !(t.Namespace?.StartsWith("Chet.QuartzNet") == true))
            .ToList();

        _logger.LogInformation("找到 {JobTypeCount} 个实现IJob接口的类", jobTypes.Count);

        // 筛选出标记了QuartzJobAttribute特性的类
        var attributeType = typeof(Chet.QuartzNet.Core.Attributes.QuartzJobAttribute);
        var attributedJobTypes = jobTypes
            .Where(t => (t.GetCustomAttributes(attributeType, false) ?? Array.Empty<object>()).Any())
            .ToList();

        _logger.LogInformation("找到 {AttributedJobCount} 个标记了QuartzJobAttribute特性的类", attributedJobTypes.Count);

        services.AddQuartz(q =>
        {
            foreach (var jobType in attributedJobTypes)
            {
                var attributes = jobType.GetCustomAttributes(attributeType, false) ?? Array.Empty<object>();
                if (attributes.Any())
                {
                    var attribute = (Chet.QuartzNet.Core.Attributes.QuartzJobAttribute)attributes.First();
                    var jobKey = new JobKey(attribute.Name, attribute.Group);
                    q.AddJob(jobType, jobKey, j => j.WithDescription(attribute.Description).StoreDurably());
                    _logger.LogInformation("注册ClassJob: {JobName} 分组: {JobGroup}, 表达式: {CronExpression}",
                        attribute.Name, attribute.Group, attribute.CronExpression);
                }
            }
        });

        // 注册一个初始化服务，用于在应用启动时将ClassJob保存到作业存储
        services.AddHostedService<ClassJobInitializer>();

        return services;
    }

    /// <summary>
    /// ClassJob初始化服务，用于在应用启动时将自动注册的ClassJob保存到作业存储
    /// </summary>
    private class ClassJobInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClassJobInitializer> _logger;

        public ClassJobInitializer(IServiceScopeFactory scopeFactory, ILogger<ClassJobInitializer> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("开始初始化ClassJob到作业存储");

                // 获取所有标记了QuartzJobAttribute特性的作业类型
                var attributeType = typeof(Chet.QuartzNet.Core.Attributes.QuartzJobAttribute);
                var jobTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            _logger.LogWarning(ex, "无法加载程序集 {AssemblyName} 中的类型", a.FullName);
                            return ex.Types.Where(t => t != null);
                        }
                    })
                    .Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Where(t => t.GetCustomAttributes(attributeType, false)?.Any() == true)
                    .ToList();

                _logger.LogInformation("找到 {JobCount} 个标记了QuartzJobAttribute特性的作业类型", jobTypes.Count);

                // 创建一个作用域来获取IJobStorage
                using (var scope = _scopeFactory.CreateScope())
                {
                    var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                    foreach (var jobType in jobTypes)
                    {
                        var attributes = jobType.GetCustomAttributes(attributeType, false) ?? Array.Empty<object>();
                        var attribute = attributes.FirstOrDefault() as Chet.QuartzNet.Core.Attributes.QuartzJobAttribute;
                        if (attribute == null)
                        {
                            _logger.LogWarning("无法获取QuartzJobAttribute特性: {JobType}", jobType.FullName);
                            continue;
                        }

                        // 检查作业是否已经存在于存储中
                        var existingJob = await jobStorage.GetJobAsync(attribute.Name, attribute.Group, cancellationToken);
                        if (existingJob != null)
                        {
                            _logger.LogInformation("作业已存在于存储中，跳过: {JobName} - {JobGroup}", attribute.Name, attribute.Group);
                            continue;
                        }

                        // 创建作业信息并保存到存储
                        var jobInfo = new QuartzJobInfo
                        {
                            JobName = attribute.Name,
                            JobGroup = attribute.Group,
                            TriggerName = $"{attribute.Name}_Trigger",
                            TriggerGroup = "DEFAULT",
                            CronExpression = attribute.CronExpression, // 使用特性中的Cron表达式
                            Description = attribute.Description,
                            JobType = JobTypeEnum.DLL,
                            JobClassOrApi = jobType.FullName ?? string.Empty,
                            Status = JobStatus.Normal,
                            IsEnabled = attribute.Enabled, // 使用特性中配置的启用状态
                            CreateTime = DateTime.Now
                        };

                        await jobStorage.AddJobAsync(jobInfo, cancellationToken);
                        _logger.LogInformation("ClassJob添加到存储: {JobName} - {JobGroup}, 启用状态: {IsEnabled}, Cron表达式: {CronExpression}",
                            attribute.Name, attribute.Group, attribute.Enabled, attribute.CronExpression);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化ClassJob失败");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 添加JWT认证
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzUIAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册QuartzUI配置
        services.Configure<QuartzUIOptions>(configuration.GetSection("QuartzUI"));

        // 获取配置
        var quartzUIOptions = configuration.GetSection("QuartzUI").Get<QuartzUIOptions>() ?? new QuartzUIOptions();

        // 启用JWT认证
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "QuartzUIJwt";
            options.DefaultChallengeScheme = "QuartzUIJwt";
        })
        .AddJwtBearer("QuartzUIJwt", options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = quartzUIOptions.JwtIssuer,
                ValidAudience = quartzUIOptions.JwtAudience,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(quartzUIOptions.JwtSecret)
                )
            };
        });

        // 添加授权策略
        services.AddAuthorization(options =>
        {
            options.AddPolicy("QuartzUIPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add("QuartzUIJwt");
                policy.RequireAuthenticatedUser();
            });
        });

        return services;
    }

    /// <summary>
    /// 作业调度初始化服务，用于在应用启动时将存储中的作业重新调度到Quartz调度器
    /// </summary>
    private class JobSchedulerInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobSchedulerInitializer> _logger;

        public JobSchedulerInitializer(IServiceScopeFactory scopeFactory, ILogger<JobSchedulerInitializer> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("开始初始化作业调度");

                // 创建一个作用域来获取服务
                using (var scope = _scopeFactory.CreateScope())
                {
                    var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();
                    var scheduler = scope.ServiceProvider.GetRequiredService<IScheduler>();

                    // 获取所有存储的作业
                    var allJobs = await jobStorage.GetAllJobsAsync(cancellationToken);
                    _logger.LogInformation("找到 {JobCount} 个存储的作业", allJobs.Count);

                    foreach (var jobInfo in allJobs)
                    {
                        try
                        {
                            // 检查作业是否已经有触发器
                            var jobKey = new JobKey(jobInfo.JobName, jobInfo.JobGroup);
                            var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);

                            if (triggers.Any())
                            {
                                _logger.LogInformation("作业 {JobKey} 已存在 {TriggerCount} 个触发器，跳过调度",
                                    $"{jobInfo.JobGroup}.{jobInfo.JobName}", triggers.Count);
                                continue;
                            }

                            // 检查作业是否被禁用，如果是禁用状态，不重新调度
                            if (!jobInfo.IsEnabled)
                            {
                                _logger.LogInformation("作业 {JobKey} 处于禁用状态，跳过调度",
                                    $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                                continue;
                            }

                            // 检查作业状态，如果是暂停状态，不重新调度
                            if (jobInfo.Status == Chet.QuartzNet.Models.Entities.JobStatus.Paused)
                            {
                                _logger.LogInformation("作业 {JobKey} 处于暂停状态，跳过调度",
                                    $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                                continue;
                            }

                            // 获取QuartzJobService实例来调用ScheduleJobAsync
                            var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();

                            // 调用ScheduleJobAsync方法重新调度作业
                            // 由于ScheduleJobAsync是私有方法，我们需要使用反射来调用
                            var jobServiceImpl = jobService as QuartzJobService;
                            if (jobServiceImpl != null)
                            {
                                // 调用ScheduleJobAsync方法重新调度作业
                                // 由于ScheduleJobAsync是私有方法，我们需要使用反射来调用
                                var scheduleMethod = typeof(QuartzJobService).GetMethod("ScheduleJobAsync",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                                if (scheduleMethod != null)
                                {
                                    var result = scheduleMethod.Invoke(jobServiceImpl, new object[] { jobInfo, cancellationToken });
                                    if (result is Task task)
                                    {
                                        await task;
                                        _logger.LogInformation("作业 {JobKey} 调度成功", $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                                    }
                                }
                                else
                                {
                                    _logger.LogError("无法找到ScheduleJobAsync方法");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "调度作业失败: {JobKey}", $"{jobInfo.JobGroup}.{jobInfo.JobName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化作业调度失败");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

