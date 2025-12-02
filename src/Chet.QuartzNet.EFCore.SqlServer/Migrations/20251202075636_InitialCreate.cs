using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chet.QuartzNet.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quartz_job_logs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 50, nullable: false, comment: "日志ID"),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "作业名称"),
                    JobGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "DEFAULT", comment: "作业分组"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "日志状态"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP", comment: "开始时间"),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "结束时间"),
                    Duration = table.Column<long>(type: "bigint", nullable: true, comment: "执行耗时(毫秒)"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Exception = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true, comment: "错误信息"),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: true, comment: "错误堆栈"),
                    Result = table.Column<string>(type: "text", nullable: true, comment: "执行结果"),
                    TriggerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "触发器名称"),
                    TriggerGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "DEFAULT", comment: "触发器分组"),
                    JobData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP", comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_job_logs", x => x.LogId);
                });

            migrationBuilder.CreateTable(
                name: "quartz_jobs",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "作业名称"),
                    JobGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "作业分组"),
                    TriggerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "触发器名称"),
                    TriggerGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "触发器分组"),
                    CronExpression = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "Cron表达式"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "作业描述"),
                    JobType = table.Column<int>(type: "int", nullable: false, comment: "作业类型"),
                    JobClassOrApi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "作业类名或API URL"),
                    JobData = table.Column<string>(type: "text", nullable: true, comment: "作业数据(JSON格式)"),
                    ApiMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, defaultValue: "GET", comment: "API请求方法"),
                    ApiHeaders = table.Column<string>(type: "text", nullable: true, comment: "API请求头(JSON格式)"),
                    ApiBody = table.Column<string>(type: "text", nullable: true, comment: "API请求体(JSON格式)"),
                    ApiTimeout = table.Column<int>(type: "int", nullable: false, defaultValue: 30000, comment: "API超时时间(毫秒)"),
                    SkipSslValidation = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否跳过SSL验证"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "开始时间"),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "结束时间"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "作业状态"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否启用"),
                    NextRunTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "下次执行时间"),
                    PreviousRunTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "上次执行时间"),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "备注"),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP", comment: "创建时间"),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "更新时间"),
                    CreateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "创建人"),
                    UpdateBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "更新人")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_jobs", x => new { x.JobName, x.JobGroup });
                });

            migrationBuilder.CreateIndex(
                name: "idx_log_create_time",
                table: "quartz_job_logs",
                column: "CreateTime");

            migrationBuilder.CreateIndex(
                name: "idx_log_job",
                table: "quartz_job_logs",
                columns: new[] { "JobName", "JobGroup" });

            migrationBuilder.CreateIndex(
                name: "idx_log_start_time",
                table: "quartz_job_logs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "idx_log_status",
                table: "quartz_job_logs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_job_create_time",
                table: "quartz_jobs",
                column: "CreateTime");

            migrationBuilder.CreateIndex(
                name: "idx_job_enabled",
                table: "quartz_jobs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "idx_job_next_run_time",
                table: "quartz_jobs",
                column: "NextRunTime");

            migrationBuilder.CreateIndex(
                name: "idx_job_status",
                table: "quartz_jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quartz_job_logs");

            migrationBuilder.DropTable(
                name: "quartz_jobs");
        }
    }
}
