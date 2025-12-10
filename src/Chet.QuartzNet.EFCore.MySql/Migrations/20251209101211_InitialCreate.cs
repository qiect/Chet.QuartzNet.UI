using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chet.QuartzNet.EFCore.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quartz_job_logs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "日志ID", collation: "ascii_general_ci"),
                    JobName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "作业名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobGroup = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "作业分组")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "日志状态"),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: false, comment: "开始时间"),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "结束时间"),
                    Duration = table.Column<long>(type: "bigint", nullable: true, comment: "执行耗时(毫秒)"),
                    Message = table.Column<string>(type: "text", nullable: true, comment: "执行结果消息")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Exception = table.Column<string>(type: "text", nullable: true, comment: "异常信息")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true, comment: "错误信息")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: true, comment: "错误堆栈")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Result = table.Column<string>(type: "text", nullable: true, comment: "执行结果")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "触发器名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerGroup = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "触发器分组")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobData = table.Column<string>(type: "text", nullable: true, comment: "执行参数")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_job_logs", x => x.LogId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quartz_jobs",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "作业名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobGroup = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "作业分组")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "触发器名称")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggerGroup = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "DEFAULT", comment: "触发器分组")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CronExpression = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "Cron表达式")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, comment: "作业描述")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobType = table.Column<int>(type: "int", nullable: false, comment: "作业类型"),
                    JobClassOrApi = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, comment: "作业类名或API URL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JobData = table.Column<string>(type: "text", nullable: true, comment: "作业数据(JSON格式)")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApiMethod = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true, defaultValue: "GET", comment: "API请求方法")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApiHeaders = table.Column<string>(type: "text", nullable: true, comment: "API请求头(JSON格式)")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApiBody = table.Column<string>(type: "text", nullable: true, comment: "API请求体(JSON格式)")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApiTimeout = table.Column<int>(type: "int", nullable: false, defaultValue: 30000, comment: "API超时时间(毫秒)"),
                    SkipSslValidation = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "是否跳过SSL验证"),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "开始时间"),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "结束时间"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "作业状态"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true, comment: "是否启用"),
                    NextRunTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "下次执行时间"),
                    PreviousRunTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "上次执行时间"),
                    Remark = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, comment: "备注")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: false, comment: "创建时间"),
                    UpdateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "更新时间"),
                    CreateBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, comment: "创建人")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdateBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, comment: "更新人")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_jobs", x => new { x.JobName, x.JobGroup });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quartz_notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "通知ID", collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "通知标题")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "text", nullable: false, comment: "通知内容")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "发送状态"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true, comment: "错误信息")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TriggeredBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, comment: "触发来源")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: false, comment: "创建时间"),
                    SendTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "发送时间"),
                    Duration = table.Column<long>(type: "bigint", nullable: true, comment: "发送耗时(毫秒)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_notifications", x => x.NotificationId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quartz_settings",
                columns: table => new
                {
                    SettingId = table.Column<Guid>(type: "char(36)", nullable: false, comment: "设置ID", collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "设置键")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "text", nullable: false, comment: "设置值")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, comment: "设置描述")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true, comment: "是否启用"),
                    CreateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: false, comment: "创建时间"),
                    UpdateTime = table.Column<DateTime>(type: "timestamp without time zone(6)", nullable: true, comment: "更新时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quartz_settings", x => x.SettingId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.CreateIndex(
                name: "idx_notification_create_time",
                table: "quartz_notifications",
                column: "CreateTime");

            migrationBuilder.CreateIndex(
                name: "idx_notification_status",
                table: "quartz_notifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_notification_triggered_by",
                table: "quartz_notifications",
                column: "TriggeredBy");

            migrationBuilder.CreateIndex(
                name: "idx_setting_key",
                table: "quartz_settings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quartz_job_logs");

            migrationBuilder.DropTable(
                name: "quartz_jobs");

            migrationBuilder.DropTable(
                name: "quartz_notifications");

            migrationBuilder.DropTable(
                name: "quartz_settings");
        }
    }
}
