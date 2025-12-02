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
    public QuartzDbContext(DbContextOptions<QuartzDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 作业信息表
    /// </summary>
    public DbSet<QuartzJobInfo> QuartzJobs { get; set; }

    /// <summary>
    /// 作业日志表
    /// </summary>
    public DbSet<QuartzJobLog> QuartzJobLogs { get; set; }

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
            .HasComment("开始时间");

        builder.Property(j => j.EndTime)
            .HasComment("结束时间");

        builder.Property(j => j.NextRunTime)
            .HasComment("下次执行时间");

        builder.Property(j => j.PreviousRunTime)
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
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间");

        builder.Property(j => j.CreateBy)
            .HasMaxLength(100)
            .HasComment("创建人");

        builder.Property(j => j.UpdateTime)
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
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("日志ID");

        builder.Property(l => l.JobName)
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("作业名称");

        builder.Property(l => l.JobGroup)
            .HasMaxLength(200)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("作业分组");

        builder.Property(l => l.TriggerName)
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("触发器名称");

        builder.Property(l => l.TriggerGroup)
            .HasMaxLength(200)
            .IsRequired()
            .HasDefaultValue("DEFAULT")
            .HasComment("触发器分组");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasDefaultValue(LogStatus.Running)
            .HasComment("日志状态");

        builder.Property(l => l.StartTime)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("开始时间");

        builder.Property(l => l.EndTime)
            .HasComment("结束时间");

        builder.Property(l => l.Duration)
            .HasComment("执行耗时(毫秒)");

        builder.Property(l => l.Result)
            .HasColumnType("text")
            .HasComment("执行结果");

        builder.Property(l => l.ErrorMessage)
            .HasColumnType("text")
            .HasComment("错误信息");

        builder.Property(l => l.ErrorStackTrace)
            .HasColumnType("text")
            .HasComment("错误堆栈");

        builder.Property(l => l.CreateTime)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .HasComment("创建时间");

        // 索引配置
        builder.HasIndex(l => new { l.JobName, l.JobGroup }).HasDatabaseName("idx_log_job");
        builder.HasIndex(l => l.Status).HasDatabaseName("idx_log_status");
        builder.HasIndex(l => l.CreateTime).HasDatabaseName("idx_log_create_time");
        builder.HasIndex(l => l.StartTime).HasDatabaseName("idx_log_start_time");
    }
}