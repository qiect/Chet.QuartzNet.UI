using Chet.QuartzNet.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection;

namespace Chet.QuartzNet.EFCore.Data;

/// <summary>
/// Quartz数据库上下文
/// </summary>
public class QuartzDbContext : DbContext
{
    private readonly bool _isPostgreSql;

    public QuartzDbContext(DbContextOptions<QuartzDbContext> options) : base(options)
    {
        // 检测当前是否使用PostgreSQL数据库
        _isPostgreSql = options.Extensions.Any(ext => ext.GetType().FullName?.Contains("Npgsql") == true);
    }

    /// <summary>
    /// 作业信息表
    /// </summary>
    public DbSet<QuartzJobInfo> QuartzJobs { get; set; }

    /// <summary>
    /// 作业日志表
    /// </summary>
    public DbSet<QuartzJobLog> QuartzJobLogs { get; set; }

    /// <summary>
    /// 系统设置表
    /// </summary>
    public DbSet<QuartzSetting> QuartzSettings { get; set; }

    /// <summary>
    /// 通知消息表
    /// </summary>
    public DbSet<QuartzNotification> QuartzNotifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用实体配置
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}

/// <summary>
/// 作业信息实体配置
/// </summary>
public class QuartzJobInfoConfiguration : IEntityTypeConfiguration<QuartzJobInfo>
{
    public void Configure(EntityTypeBuilder<QuartzJobInfo> builder)
    {
        builder.ToTable("quartz_jobs");

        builder.HasKey(j => new { j.JobName, j.JobGroup });

        builder.Property(j => j.JobName)
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("作业名称");

        builder.Property(j => j.JobGroup)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("作业分组");

        builder.Property(j => j.TriggerName)
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("触发器名称");

        builder.Property(j => j.TriggerGroup)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("触发器分组");

        builder.Property(j => j.CronExpression)
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Cron表达式");

        builder.Property(j => j.Description)
            .HasMaxLength(500)
            .HasComment("作业描述");

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasComment("作业类型");

        builder.Property(j => j.JobClassOrApi)
            .HasMaxLength(500)
            .IsRequired()
            .HasComment("作业类名或API URL");

        builder.Property(j => j.JobData)
            .HasColumnType("text")
            .HasComment("作业数据(JSON格式)");

        // API相关字段配置
        builder.Property(j => j.ApiMethod)
            .HasMaxLength(10)
            .HasDefaultValue("GET")
            .HasComment("API请求方法");

        builder.Property(j => j.ApiHeaders)
            .HasColumnType("text")
            .HasComment("API请求头(JSON格式)");

        builder.Property(j => j.ApiBody)
            .HasColumnType("text")
            .HasComment("API请求体(JSON格式)");

        builder.Property(j => j.ApiTimeout)
            .IsRequired()
            .HasDefaultValue(30000)
            .HasComment("API超时时间(毫秒)");

        builder.Property(j => j.SkipSslValidation)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("是否跳过SSL验证");

        builder.Property(j => j.StartTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("开始时间");

        builder.Property(j => j.EndTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("结束时间");

        builder.Property(j => j.NextRunTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("下次执行时间");

        builder.Property(j => j.PreviousRunTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("上次执行时间");

        builder.Property(j => j.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("是否启用");

        builder.Property(j => j.Status)
            .IsRequired()
            .HasDefaultValue(JobStatus.Normal)
            .HasComment("作业状态");

        builder.Property(j => j.CreateTime)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasComment("创建时间");

        builder.Property(j => j.CreateBy)
            .HasMaxLength(100)
            .HasComment("创建人");

        builder.Property(j => j.UpdateTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("更新时间");

        builder.Property(j => j.UpdateBy)
            .HasMaxLength(100)
            .HasComment("更新人");

        builder.Property(j => j.Remark)
            .HasMaxLength(500)
            .HasComment("备注");

        // 索引配置
        builder.HasIndex(j => j.Status).HasDatabaseName("idx_job_status");
        builder.HasIndex(j => j.IsEnabled).HasDatabaseName("idx_job_enabled");
        builder.HasIndex(j => j.NextRunTime).HasDatabaseName("idx_job_next_run_time");
        builder.HasIndex(j => j.CreateTime).HasDatabaseName("idx_job_create_time");
    }
}

/// <summary>
/// 作业日志实体配置
/// </summary>
public class QuartzJobLogConfiguration : IEntityTypeConfiguration<QuartzJobLog>
{
    public void Configure(EntityTypeBuilder<QuartzJobLog> builder)
    {
        builder.ToTable("quartz_job_logs");

        builder.HasKey(l => l.LogId);

        builder.Property(l => l.LogId)
            .IsRequired()
            .HasComment("日志ID");

        builder.Property(l => l.JobName)
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("作业名称");

        builder.Property(l => l.JobGroup)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("作业分组");

        builder.Property(l => l.TriggerName)
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("触发器名称");

        builder.Property(l => l.TriggerGroup)
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("触发器分组");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasDefaultValue(LogStatus.Running)
            .HasComment("日志状态");

        builder.Property(l => l.StartTime)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasComment("开始时间");

        builder.Property(l => l.EndTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("结束时间");

        builder.Property(l => l.Duration)
            .HasComment("执行耗时(毫秒)");

        builder.Property(l => l.Message)
            .HasColumnType("text")
            .HasComment("执行结果消息");

        builder.Property(l => l.Exception)
            .HasColumnType("text")
            .HasComment("异常信息");

        builder.Property(l => l.Result)
            .HasColumnType("text")
            .HasComment("执行结果");

        builder.Property(l => l.ErrorMessage)
            .HasColumnType("text")
            .HasComment("错误信息");

        builder.Property(l => l.ErrorStackTrace)
            .HasColumnType("text")
            .HasComment("错误堆栈");

        builder.Property(l => l.JobData)
            .HasColumnType("text")
            .HasComment("执行参数");

        builder.Property(l => l.CreateTime)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasComment("创建时间");

        // 索引配置
        builder.HasIndex(l => new { l.JobName, l.JobGroup }).HasDatabaseName("idx_log_job");
        builder.HasIndex(l => l.Status).HasDatabaseName("idx_log_status");
        builder.HasIndex(l => l.CreateTime).HasDatabaseName("idx_log_create_time");
        builder.HasIndex(l => l.StartTime).HasDatabaseName("idx_log_start_time");
    }
}

/// <summary>
/// 系统设置实体配置
/// </summary>
public class QuartzSettingConfiguration : IEntityTypeConfiguration<QuartzSetting>
{
    public void Configure(EntityTypeBuilder<QuartzSetting> builder)
    {
        builder.ToTable("quartz_settings");

        builder.HasKey(s => s.SettingId);

        builder.Property(s => s.SettingId)
            .IsRequired()
            .HasComment("设置ID");

        builder.Property(s => s.Key)
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("设置键");

        builder.Property(s => s.Value)
            .HasColumnType("text")
            .IsRequired()
            .HasComment("设置值");

        builder.Property(s => s.Description)
            .HasMaxLength(500)
            .HasComment("设置描述");

        builder.Property(s => s.Enabled)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("是否启用");

        builder.Property(s => s.CreateTime)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasComment("创建时间");

        builder.Property(s => s.UpdateTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("更新时间");

        // 索引配置
        builder.HasIndex(s => s.Key).IsUnique().HasDatabaseName("idx_setting_key");
    }
}

/// <summary>
/// 通知消息实体配置
/// </summary>
public class QuartzNotificationConfiguration : IEntityTypeConfiguration<QuartzNotification>
{
    public void Configure(EntityTypeBuilder<QuartzNotification> builder)
    {
        builder.ToTable("quartz_notifications");

        builder.HasKey(n => n.NotificationId);

        builder.Property(n => n.NotificationId)
            .IsRequired()
            .HasComment("通知ID");

        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("通知标题");

        builder.Property(n => n.Content)
            .HasColumnType("text")
            .IsRequired()
            .HasComment("通知内容");

        builder.Property(n => n.Status)
            .IsRequired()
            .HasDefaultValue(NotificationStatus.Pending)
            .HasComment("发送状态");

        builder.Property(n => n.ErrorMessage)
            .HasColumnType("text")
            .HasComment("错误信息");

        builder.Property(n => n.TriggeredBy)
            .HasMaxLength(100)
            .HasComment("触发来源");

        builder.Property(n => n.CreateTime)
            .IsRequired()
            .HasColumnType("timestamp without time zone")
            .HasComment("创建时间");

        builder.Property(n => n.SendTime)
            .HasColumnType("timestamp without time zone")
            .HasComment("发送时间");

        builder.Property(n => n.Duration)
            .HasComment("发送耗时(毫秒)");

        // 索引配置
        builder.HasIndex(n => n.Status).HasDatabaseName("idx_notification_status");
        builder.HasIndex(n => n.CreateTime).HasDatabaseName("idx_notification_create_time");
        builder.HasIndex(n => n.TriggeredBy).HasDatabaseName("idx_notification_triggered_by");
    }
}