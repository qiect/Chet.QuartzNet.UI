using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chet.QuartzNet.EFCore.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDisallowConcurrentExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisallowConcurrentExecution",
                table: "quartz_jobs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                comment: "禁止并发执行");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisallowConcurrentExecution",
                table: "quartz_jobs");
        }
    }
}