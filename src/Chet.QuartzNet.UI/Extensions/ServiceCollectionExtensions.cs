using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Services;
using Chet.QuartzNet.Models.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using System.Reflection;

namespace Chet.QuartzNet.UI.Extensions;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    private static ILogger? _logger = null;

    /// <summary>
    /// 添加 Quartz UI（配置驱动，支持文件/数据库存储，内置 JWT 注册）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置（读取 QuartzUI 节与 ConnectionStrings:QuartzUI）</param>
    /// <param name="configureOptions">可选：追加/覆写配置</param>
    public static IServiceCollection AddQuartzUI(this IServiceCollection services, IConfiguration configuration, Action<QuartzUIOptions>? configureOptions = null)
    {
        // 绑定配置到选项，并让 DI 管道也感知
        var options = BindQuartzOptions(configuration, configureOptions);
        services.Configure<QuartzUIOptions>(configuration.GetSection("QuartzUI"));
        if (configureOptions != null)
        {
            services.PostConfigure(configureOptions);
        }


        // 存储注册
        if (options.StorageType == StorageType.Database)
        {
            RegisterDatabaseStorage(services, configuration, options);
        }
        else
        {
            RegisterFileStorage(services, options);
        }

        // 通用服务（作业服务、监听器、Quartz 调度器等）
        RegisterQuartzCore(services, options);

        // 认证
        AddQuartzUIAuthentication(services, options);

        // 注册RCL中的控制器
        services.AddControllers()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// 绑定Quartz配置项
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    private static QuartzUIOptions BindQuartzOptions(IConfiguration configuration, Action<QuartzUIOptions>? configureOptions)
    {
        var options = new QuartzUIOptions();
        configuration.GetSection("QuartzUI").Bind(options);
        configureOptions?.Invoke(options);
        return options;
    }

    /// <summary>
    /// 注册文件存储服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void RegisterFileStorage(IServiceCollection services, QuartzUIOptions options)
    {
        services.TryAddScoped<IJobStorage, FileJobStorage>();
    }

    /// <summary>
    /// 注册数据库存储服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <param name="options"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    private static void RegisterDatabaseStorage(IServiceCollection services, IConfiguration configuration, QuartzUIOptions options)
    {
        var connectionString = configuration.GetConnectionString("QuartzUI");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("未找到 QuartzUI 数据库连接字符串 (ConnectionStrings:QuartzUI)");
        }

        var providerMap = new Dictionary<DatabaseProvider, (string TypeName, string MethodName)>
        {
            { DatabaseProvider.MySql, ("Chet.QuartzNet.EFCore.MySQL.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.MySQL", "AddQuartzUIMySql") },
            { DatabaseProvider.PostgreSql, ("Chet.QuartzNet.EFCore.PostgreSQL.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.PostgreSQL", "AddQuartzUIPostgreSQL") },
            { DatabaseProvider.SqlServer, ("Chet.QuartzNet.EFCore.SqlServer.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.SqlServer", "AddQuartzUISqlServer") },
            { DatabaseProvider.SQLite, ("Chet.QuartzNet.EFCore.SQLite.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.SQLite", "AddQuartzUISQLite") },
        };

        if (!providerMap.TryGetValue(options.DatabaseProvider, out var providerMeta))
        {
            throw new ArgumentException($"不支持的数据库提供程序: {options.DatabaseProvider}");
        }

        var providerType = Type.GetType(providerMeta.TypeName, throwOnError: false);
        if (providerType == null)
        {
            throw new InvalidOperationException($"未找到数据库提供程序程序集，请确保已引用对应 NuGet 包：{providerMeta.TypeName}");
        }

        var methods = providerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        var method = methods.FirstOrDefault(m =>
            m.Name == providerMeta.MethodName &&
            m.GetParameters().Length >= 2 &&
            m.GetParameters()[0].ParameterType == typeof(IServiceCollection) &&
            m.GetParameters()[1].ParameterType == typeof(string));

        if (method == null)
        {
            throw new InvalidOperationException($"在 {providerType.FullName} 中未找到合适的 {providerMeta.MethodName} 方法，请更新对应数据库包版本。");
        }

        // 根据方法参数数量传递相应的参数
        var parameters = method.GetParameters();
        object[] args = new object[parameters.Length];
        args[0] = services;
        args[1] = connectionString;
        // 对于可选参数，传入 null
        for (int i = 2; i < parameters.Length; i++)
        {
            args[i] = null;
        }

        method.Invoke(null, args);
    }

    /// <summary>
    /// 注册QuartzUI核心服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void RegisterQuartzCore(IServiceCollection services, QuartzUIOptions options)
    {
        services.TryAddScoped<IQuartzJobService, QuartzJobService>();
        services.TryAddScoped<QuartzJobListener>();
        services.TryAddSingleton<JobClassScanner>();

        // 添加HTTP客户端支持
        services.AddHttpClient();

        // 注册推送通知服务
        services.TryAddScoped<INotificationService, PushPlusNotificationService>();

        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
            q.UseDefaultThreadPool(tp =>
            {
                tp.MaxConcurrency = options.ThreadPoolSize > 0 ? options.ThreadPoolSize : 10;
            });

            q.AddJobListener<QuartzJobListener>(GroupMatcher<JobKey>.AnyGroup());
        });

        services.AddSingleton<IScheduler>(provider =>
        {
            var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
            return schedulerFactory.GetScheduler().Result;
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        services.AddHostedService<JobSchedulerInitializer>();
    }

    /// <summary>
    /// 注册JWT认证服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void AddQuartzUIAuthentication(IServiceCollection services, QuartzUIOptions options)
    {
        if (!options.EnableJwtAuth)
        {
            return;
        }

        services.AddAuthentication(authOptions =>
        {
            authOptions.DefaultAuthenticateScheme = "QuartzUIJwt";
            authOptions.DefaultChallengeScheme = "QuartzUIJwt";
        })
        .AddJwtBearer("QuartzUIJwt", bearerOptions =>
        {
            bearerOptions.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = options.JwtIssuer,
                ValidAudience = options.JwtAudience,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(options.JwtSecret)
                )
            };
        });

        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy("QuartzUIPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add("QuartzUIJwt");
                policy.RequireAuthenticatedUser();
            });
        });
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

