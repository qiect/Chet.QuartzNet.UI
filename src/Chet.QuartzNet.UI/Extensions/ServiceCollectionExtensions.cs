using System.Reflection;
using Chet.QuartzNet.Core.Configuration;
using Chet.QuartzNet.Core.Helpers;
using Chet.QuartzNet.Core.Interfaces;
using Chet.QuartzNet.Core.Recovery;
using Chet.QuartzNet.Core.Scheduling;
using Chet.QuartzNet.Core.Services;
using Chet.QuartzNet.EFCore.MigrationSupport;
using Chet.QuartzNet.Models.DTOs;
using Chet.QuartzNet.Models.Entities;
using Chet.QuartzNet.UI.HostedServices;
using Chet.QuartzNet.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;

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
    public static IServiceCollection AddQuartzUI(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<QuartzUIOptions>? configureOptions = null
    )
    {
        // 获取或创建日志记录器
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger("Chet.QuartzNet.UI");

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

        // 数据迁移服务（支持文件与数据库之间互转）
        services.TryAddSingleton<MigrationStoreFactory>();
        services.TryAddSingleton<StorageMigrationService>();
        services.TryAddSingleton<FileDataMigrationService>();

        // 认证
        AddQuartzUIAuthentication(services, options);

        // 注册RCL中的控制器，配置JSON序列化确保DateTimeOffset带时区偏移
        services
            .AddControllers()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly)
            .AddJsonOptions(options =>
                JsonSerializationConfig.ConfigureMvcJsonOptions(
                    options.JsonSerializerOptions
                )
            );

        return services;
    }

    /// <summary>
    /// 绑定Quartz配置项
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    private static QuartzUIOptions BindQuartzOptions(
        IConfiguration configuration,
        Action<QuartzUIOptions>? configureOptions
    )
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
    private static void RegisterDatabaseStorage(
        IServiceCollection services,
        IConfiguration configuration,
        QuartzUIOptions options
    )
    {
        var connectionString = configuration.GetConnectionString("QuartzUI");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "未找到 QuartzUI 数据库连接字符串 (ConnectionStrings:QuartzUI)"
            );
        }

        var providerMap = new Dictionary<DatabaseProvider, (string TypeName, string MethodName)>
        {
            {
                DatabaseProvider.MySql,
                (
                    "Chet.QuartzNet.EFCore.MySQL.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.MySQL",
                    "AddQuartzUIMySql"
                )
            },
            {
                DatabaseProvider.PostgreSql,
                (
                    "Chet.QuartzNet.EFCore.PostgreSQL.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.PostgreSQL",
                    "AddQuartzUIPostgreSQL"
                )
            },
            {
                DatabaseProvider.SqlServer,
                (
                    "Chet.QuartzNet.EFCore.SqlServer.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.SqlServer",
                    "AddQuartzUISqlServer"
                )
            },
            {
                DatabaseProvider.SQLite,
                (
                    "Chet.QuartzNet.EFCore.SQLite.Extensions.ServiceCollectionExtensions, Chet.QuartzNet.EFCore.SQLite",
                    "AddQuartzUISQLite"
                )
            },
        };

        if (!providerMap.TryGetValue(options.DatabaseProvider, out var providerMeta))
        {
            throw new ArgumentException($"不支持的数据库提供程序: {options.DatabaseProvider}");
        }

        var providerType = Type.GetType(providerMeta.TypeName, throwOnError: false);
        if (providerType == null)
        {
            throw new InvalidOperationException(
                $"未找到数据库提供程序程序集，请确保已引用对应 NuGet 包：{providerMeta.TypeName}"
            );
        }

        var methods = providerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        var method = methods.FirstOrDefault(m =>
            m.Name == providerMeta.MethodName
            && m.GetParameters().Length >= 2
            && m.GetParameters()[0].ParameterType == typeof(IServiceCollection)
            && m.GetParameters()[1].ParameterType == typeof(string)
        );

        if (method == null)
        {
            throw new InvalidOperationException(
                $"在 {providerType.FullName} 中未找到合适的 {providerMeta.MethodName} 方法，请更新对应数据库包版本。"
            );
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

        // 中断恢复与排水调度
        services.TryAddSingleton<RuntimeInstance>();
        services.TryAddSingleton<JobExecutionRegistry>();
        services.TryAddSingleton<RecoveryOptionsProvider>();
        services.TryAddSingleton<RecoverySummaryStore>();
        services.TryAddSingleton<RecoveryCoordinator>();
        services.TryAddSingleton<SchedulerInstance>();
        services.TryAddSingleton<DrainCoordinator>();

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
            // 统一由 SchedulerInstance 持有当前实例：调度器关闭后工厂会创建新实例，
            // 若各处直接向工厂索取，会把「已停止」的调度器显示成「待机」
            var schedulerInstance = provider.GetRequiredService<SchedulerInstance>();
            return schedulerInstance.Current
                ?? schedulerInstance.GetSchedulerAsync(schedulerFactory).GetAwaiter().GetResult();
        });

        // 应用关闭时的等待策略由 GracefulShutdownHostedService（DrainCoordinator）按
        // 系统配置的 ShutdownMode 统一处理：排水模式会等到执行中任务收尾或超时，
        // 立即停止模式则直接返回。这里必须为 false，否则 Quartz 宿主服务会无条件
        // 无超时地等待所有作业完成，导致「立即停止」策略失效。
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = false;
        });

        services.AddHostedService<JobSchedulerInitializer>();
        services.AddHostedService<JobHeartbeatHostedService>();
        services.AddHostedService<RecoveryHostedService>();
        services.AddHostedService<LogCleanupHostedService>();
        // 必须最后注册：停止时最先执行，赶在 Quartz 关闭前完成排水等待
        services.AddHostedService<GracefulShutdownHostedService>();
    }

    /// <summary>
    /// 注册JWT认证服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="options"></param>
    private static void AddQuartzUIAuthentication(
        IServiceCollection services,
        QuartzUIOptions options
    )
    {
        if (!options.EnableJwtAuth)
        {
            return;
        }

        services
            .AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = "QuartzUIJwt";
                authOptions.DefaultChallengeScheme = "QuartzUIJwt";
            })
            .AddJwtBearer(
                "QuartzUIJwt",
                bearerOptions =>
                {
                    bearerOptions.TokenValidationParameters =
                        new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = options.JwtIssuer,
                            ValidAudience = options.JwtAudience,
                            IssuerSigningKey =
                                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                                    System.Text.Encoding.UTF8.GetBytes(options.JwtSecret)
                                ),
                        };
                }
            );

        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy(
                "QuartzUIPolicy",
                policy =>
                {
                    policy.AuthenticationSchemes.Add("QuartzUIJwt");
                    policy.RequireAuthenticatedUser();
                }
            );
        });
    }

    /// <summary>
    /// 添加ClassJob支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddQuartzClassJobs(this IServiceCollection services)
    {
        // 注册ClassJob初始化服务
        services.AddHostedService<ClassJobInitializer>();
        return services;
    }

    /// <summary>
    /// 将文件存储中的数据迁移到数据库存储
    /// 应用启动后自动执行，从 FileJobStorage 读取作业、日志、设置、通知数据，
    /// 通过当前注册的 IJobStorage（数据库存储）写入，跳过已存在的记录
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">可选：覆写文件存储路径等配置（默认从 IConfiguration 读取）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddFileDataToDatabase(
        this IServiceCollection services,
        Action<QuartzUIOptions>? configureOptions = null
    )
    {
        if (configureOptions != null)
        {
            services.PostConfigure(configureOptions);
        }

        services.AddHostedService<FileDataMigrator>();
        return services;
    }

    /// <summary>
    /// ClassJob初始化服务，用于在应用启动时将标记了QuartzJobAttribute特性的作业保存到存储
    /// </summary>
    private class ClassJobInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClassJobInitializer> _logger;

        public ClassJobInitializer(
            IServiceScopeFactory scopeFactory,
            ILogger<ClassJobInitializer> logger
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInfoStructured("初始化ClassJob到作业存储");

                // 获取所有标记了QuartzJobAttribute特性的作业类型
                var attributeType = typeof(Chet.QuartzNet.Core.Attributes.QuartzJobAttribute);
                var jobTypes = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "无法加载程序集 {AssemblyName} 中的类型",
                                a.FullName
                            );
                            return ex.Types.Where(t => t != null);
                        }
                    })
                    .Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Where(t => t.GetCustomAttributes(attributeType, false)?.Any() == true)
                    .ToList();

                _logger.LogInfo(
                    "ClassJob初始化",
                    "找到 {JobCount} 个标记了QuartzJobAttribute特性的作业类型",
                    jobTypes.Count
                );

                // 创建一个作用域来获取IJobStorage
                using (var scope = _scopeFactory.CreateScope())
                {
                    var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                    foreach (var jobType in jobTypes)
                    {
                        var attributes =
                            jobType.GetCustomAttributes(attributeType, false)
                            ?? Array.Empty<object>();
                        var attribute =
                            attributes.FirstOrDefault()
                            as Chet.QuartzNet.Core.Attributes.QuartzJobAttribute;
                        if (attribute == null)
                        {
                            _logger.LogWarn(
                                "ClassJob初始化",
                                "无法获取QuartzJobAttribute特性: {JobType}",
                                jobType.FullName
                            );
                            continue;
                        }

                        // 检查作业是否已经存在于存储中
                        var existingJob = await jobStorage.GetJobAsync(
                            attribute.Name,
                            attribute.Group,
                            cancellationToken
                        );
                        if (existingJob != null)
                        {
                            _logger.LogInfo(
                                "ClassJob初始化",
                                "作业已存在于存储中，跳过: {JobKey}",
                                $"{attribute.Name}.{attribute.Group}"
                            );
                            continue;
                        }

                        // 创建作业信息并保存到存储
                        var jobInfo = new QuartzJobInfo
                        {
                            JobName = attribute.Name,
                            JobGroup = attribute.Group,
                            TriggerName = $"{attribute.Name}_Trigger",
                            TriggerGroup = attribute.Group,
                            CronExpression = attribute.CronExpression, // 使用特性中的Cron表达式
                            Description = attribute.Description,
                            JobType = JobTypeEnum.DLL,
                            JobClassOrApi = jobType.FullName ?? string.Empty,
                            Status = JobStatus.Normal,
                            IsEnabled = attribute.Enabled, // 使用特性中配置的启用状态
                            CreateTime = DateTime.Now,
                            DisallowConcurrentExecution = jobType.GetCustomAttributes<DisallowConcurrentExecutionAttribute>(true)?.Any() ?? false // 使用Quartz.NET内置的DisallowConcurrentExecution特性
                        };

                        await jobStorage.AddJobAsync(jobInfo, cancellationToken);
                        _logger.LogSuccess(
                            "ClassJob初始化",
                            "ClassJob添加到存储: {JobKey}, 启用状态: {IsEnabled}, Cron表达式: {CronExpression}",
                            $"{attribute.Name}.{attribute.Group}",
                            attribute.Enabled,
                            attribute.CronExpression
                        );

                    }

                    // 新增的作业统一交给作业服务按存储内容加载，
                    // 不再通过反射调用私有调度方法（启用状态由服务内部判断）
                    var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();
                    var reloadResult = await jobService.ReloadJobsAsync(cancellationToken);
                    if (reloadResult.Success)
                    {
                        _logger.LogSuccess(
                            "ClassJob初始化",
                            reloadResult.Message ?? "ClassJob调度完成"
                        );
                    }
                    else
                    {
                        _logger.LogWarn(
                            "ClassJob初始化",
                            reloadResult.Message ?? "ClassJob调度失败"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogFailure("初始化ClassJob", ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// JobScheduler初始化服务，用于在应用启动时将存储中的作业重新调度到Quartz调度器
    /// </summary>
    private class JobSchedulerInitializer : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobSchedulerInitializer> _logger;

        public JobSchedulerInitializer(
            IServiceScopeFactory scopeFactory,
            ILogger<JobSchedulerInitializer> logger
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInfoStructured("初始化作业调度");

                // 创建一个作用域来获取服务
                using (var scope = _scopeFactory.CreateScope())
                {
                    // 统一交给作业服务按存储内容重新加载（已存在触发器、禁用、暂停的作业会自动跳过），
                    // 避免通过反射调用私有调度方法
                    var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();
                    var reloadResult = await jobService.ReloadJobsAsync(cancellationToken);
                    if (reloadResult.Success)
                    {
                        _logger.LogSuccess("作业调度", reloadResult.Message ?? "作业调度完成");
                    }
                    else
                    {
                        _logger.LogWarn("作业调度", reloadResult.Message ?? "作业调度失败");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogFailure("初始化作业调度", ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 文件数据迁移服务，将 FileJobStorage 中的数据迁移到数据库存储
    /// </summary>
    private class FileDataMigrator : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FileDataMigrator> _logger;
        private readonly IOptions<QuartzUIOptions> _options;

        public FileDataMigrator(
            IServiceScopeFactory scopeFactory,
            ILogger<FileDataMigrator> logger,
            IOptions<QuartzUIOptions> options
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var options = _options.Value;

            // 检查文件存储路径是否存在
            if (
                string.IsNullOrWhiteSpace(options.FileStoragePath)
                || !Directory.Exists(Path.GetFullPath(options.FileStoragePath))
            )
            {
                _logger.LogInfo(
                    "文件数据迁移",
                    "文件存储路径不存在，跳过迁移: {Path}",
                    options.FileStoragePath
                );
                return;
            }

            if (options.StorageType != StorageType.Database)
            {
                _logger.LogWarn(
                    "文件数据迁移",
                    "当前存储类型不是数据库，跳过迁移。请将 StorageType 设置为 Database 后重试"
                );
                return;
            }

            _logger.LogInfoStructured("开始文件数据迁移到数据库");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var migrationService =
                    scope.ServiceProvider.GetRequiredService<StorageMigrationService>();

                var success = await migrationService.RunAsync(
                    new TriggerMigrationRequestDto(),
                    cancellationToken
                );

                if (success)
                {
                    _logger.LogSuccess("文件数据迁移", "文件数据迁移到数据库完成");
                }
                else
                {
                    _logger.LogWarn("文件数据迁移", "文件数据迁移未成功，请查看迁移状态");
                }
            }
            catch (Exception ex)
            {
                _logger.LogFailure("文件数据迁移", ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 作业日志自动清理服务，按系统配置中的保留天数/最大条数定期清理日志
    /// </summary>
    private class LogCleanupHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogCleanupHostedService> _logger;

        public LogCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<LogCleanupHostedService> logger
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInfoStructured("启动作业日志自动清理服务");
            _ = RunCleanupLoopAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 应用启动后稍作延迟，避免与存储初始化、作业加载竞争
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var intervalHours = 6;
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var jobService = scope.ServiceProvider.GetRequiredService<IQuartzJobService>();
                        var jobStorage = scope.ServiceProvider.GetRequiredService<IJobStorage>();

                        var configResult = await jobService.GetSystemConfigAsync(cancellationToken);
                        SystemConfigDto config =
                            configResult.Data ?? new SystemConfigDto();
                        intervalHours = config.LogCleanupIntervalHours > 0
                            ? config.LogCleanupIntervalHours
                            : 6;

                        var clearedCount = 0;
                        if (config.LogRetentionDays > 0)
                        {
                            clearedCount += await jobStorage.ClearExpiredLogsAsync(
                                config.LogRetentionDays,
                                config.InterruptedLogRetentionDays,
                                cancellationToken
                            );
                        }

                        if (config.LogMaxCount > 0)
                        {
                            clearedCount += await jobStorage.ClearOverflowLogsAsync(
                                config.LogMaxCount,
                                true,
                                cancellationToken
                            );
                        }

                        _logger.LogInfo(
                            "作业日志自动清理",
                            "清理完成，共清理 {Count} 条日志，下次清理间隔 {Interval} 小时",
                            clearedCount,
                            intervalHours
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogFailure("作业日志自动清理", ex);
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromHours(intervalHours), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 应用关闭
            }
            catch (Exception ex)
            {
                _logger.LogFailure("作业日志自动清理", ex);
            }
        }
    }
}