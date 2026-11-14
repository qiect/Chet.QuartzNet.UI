using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chet.QuartzNet.EFCore.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryAndDrainFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RerunOnRecovery",
                table: "quartz_jobs",
                type: "boolean",
                nullable: true,
                comment: "中断后是否自动重跑(null=跟随全局)");

            migrationBuilder.AddColumn<Guid>(
                name: "BootId",
                table: "quartz_job_logs",
                type: "uuid",
                nullable: true,
                comment: "进程启动标识");

            migrationBuilder.AddColumn<Guid>(
                name: "CompensationLogId",
                table: "quartz_job_logs",
                type: "uuid",
                nullable: true,
                comment: "补偿产生的日志ID");

            migrationBuilder.AddColumn<int>(
                name: "CompensationStatus",
                table: "quartz_job_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "补偿状态");

            migrationBuilder.AddColumn<DateTime>(
                name: "HeartbeatTime",
                table: "quartz_job_logs",
                type: "timestamp without time zone",
                nullable: true,
                comment: "最后心跳时间");

            migrationBuilder.AddColumn<string>(
                name: "InstanceId",
                table: "quartz_job_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                comment: "执行实例标识");

            migrationBuilder.AddColumn<DateTime>(
                name: "InterruptedAt",
                table: "quartz_job_logs",
                type: "timestamp without time zone",
                nullable: true,
                comment: "判定中断时间");

            migrationBuilder.AddColumn<int>(
                name: "RecoveryAttempt",
                table: "quartz_job_logs",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                comment: "第几次尝试");

            migrationBuilder.AddColumn<Guid>(
                name: "RecoverySourceLogId",
                table: "quartz_job_logs",
                type: "uuid",
                nullable: true,
                comment: "补偿来源日志ID");

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "quartz_job_logs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "跳过补偿原因");

            migrationBuilder.AddColumn<int>(
                name: "TriggerType",
                table: "quartz_job_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "触发来源");

            migrationBuilder.AddColumn<string>(
                name: "TriggeredBy",
                table: "quartz_job_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "操作人");

            migrationBuilder.CreateIndex(
                name: "idx_log_compensation",
                table: "quartz_job_logs",
                columns: new[] { "Status", "CompensationStatus" });

            migrationBuilder.CreateIndex(
                name: "idx_log_recovery",
                table: "quartz_job_logs",
                columns: new[] { "Status", "HeartbeatTime" });

            migrationBuilder.CreateIndex(
                name: "idx_log_recovery_source",
                table: "quartz_job_logs",
                column: "RecoverySourceLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_log_compensation",
                table: "quartz_job_logs");

            migrationBuilder.DropIndex(
                name: "idx_log_recovery",
                table: "quartz_job_logs");

            migrationBuilder.DropIndex(
                name: "idx_log_recovery_source",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "RerunOnRecovery",
                table: "quartz_jobs");

            migrationBuilder.DropColumn(
                name: "BootId",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "CompensationLogId",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "CompensationStatus",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "HeartbeatTime",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "InterruptedAt",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "RecoveryAttempt",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "RecoverySourceLogId",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "quartz_job_logs");

            migrationBuilder.DropColumn(
                name: "TriggeredBy",
                table: "quartz_job_logs");
        }
    }
}
