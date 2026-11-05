using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chet.QuartzNet.EFCore.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Dependencies",
                table: "quartz_jobs",
                type: "text",
                nullable: true,
                comment: "前置依赖作业(JSON格式)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Dependencies",
                table: "quartz_jobs");
        }
    }
}
