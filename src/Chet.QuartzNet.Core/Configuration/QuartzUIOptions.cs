namespace Chet.QuartzNet.Core.Configuration;

/// <summary>
/// Quartz UI 配置选项
/// </summary>
public class QuartzUIOptions
{
    /// <summary>
    /// UI路由前缀，默认为 "/QuartzUI"
    /// </summary>
    public string RoutePrefix { get; set; } = "/quartz-ui";

    /// <summary>
    /// 是否启用JWT认证
    /// </summary>
    public bool EnableJwtAuth { get; set; } = true;

    /// <summary>
    /// 用户名，默认为 "Admin"
    /// </summary>
    public string UserName { get; set; } = "Admin";

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = "123456";

    /// <summary>
    /// JWT密钥
    /// </summary>
    public string JwtSecret { get; set; } = "your-secret-key-change-this-in-production";

    /// <summary>
    /// JWT过期时间（分钟），默认30分钟
    /// </summary>
    public int JwtExpiresInMinutes { get; set; } = 30;

    /// <summary>
    /// JWT签发者
    /// </summary>
    public string JwtIssuer { get; set; } = "Chet";

    /// <summary>
    /// JWT受众
    /// </summary>
    public string JwtAudience { get; set; } = "Chet.QuartzNet.UI";

    /// <summary>
    /// 存储类型，默认为 File
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.File;

    /// <summary>
    /// 文件存储路径，默认为 "App_Data/QuartzJobs"
    /// </summary>
    public string FileStoragePath { get; set; } = "App_Data/QuartzJobs";

    /// <summary>
    /// 是否启用文件存储备份，默认为 true
    /// </summary>
    public bool EnableFileBackup { get; set; } = true;

    /// <summary>
    /// 文件备份路径，默认为 "App_Data/QuartzJobs/Backup"
    /// </summary>
    public string FileBackupPath { get; set; } = "App_Data/QuartzJobs/Backup";

    /// <summary>
    /// 最大备份文件数，默认为 10
    /// </summary>
    public int MaxBackupFiles { get; set; } = 10;

    /// <summary>
    /// 是否自动启动调度器，默认为 true
    /// </summary>
    public bool AutoStartScheduler { get; set; } = true;

    /// <summary>
    /// 调度器名称，默认为 "QuartzUI_Scheduler"
    /// </summary>
    public string SchedulerName { get; set; } = "QuartzUI_Scheduler";

    /// <summary>
    /// 线程池大小，默认为 10
    /// </summary>
    public int ThreadPoolSize { get; set; } = 10;

    /// <summary>
    /// 是否启用远程管理，默认为 false
    /// </summary>
    public bool EnableRemoteManagement { get; set; } = false;

    /// <summary>
    /// 远程管理端口，默认为 555
    /// </summary>
    public int RemoteManagementPort { get; set; } = 555;

    /// <summary>
    /// 作业扫描间隔（秒），默认为 60
    /// </summary>
    public int JobScanInterval { get; set; } = 60;

    /// <summary>
    /// 数据库提供程序，默认为 MySql
    /// </summary>
    public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.MySql;

    /// <summary>
    /// 邮件通知配置
    /// </summary>
    public EmailOptions EmailOptions { get; set; } = new EmailOptions();
}

/// <summary>
/// 存储类型枚举
/// </summary>
public enum StorageType
{
    /// <summary>
    /// 文件存储
    /// </summary>
    File = 0,

    /// <summary>
    /// 数据库存储
    /// </summary>
    Database = 1
}

/// <summary>
/// 数据库提供程序类型
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// MySQL
    /// </summary>
    MySql = 0,

    /// <summary>
    /// PostgreSQL
    /// </summary>
    PostgreSql = 1,

    /// <summary>
    /// SQL Server
    /// </summary>
    SqlServer = 2,

    /// <summary>
    /// SQLite
    /// </summary>
    SQLite = 3
}