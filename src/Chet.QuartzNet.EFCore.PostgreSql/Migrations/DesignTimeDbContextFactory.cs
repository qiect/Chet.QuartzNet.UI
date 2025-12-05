using Chet.QuartzNet.EFCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chet.QuartzNet.EFCore.PostgreSQL.Migrations;

/// <summary>
/// 设计时DbContext工厂，用于生成迁移文件
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuartzDbContext>
{
    public QuartzDbContext CreateDbContext(string[] args)
    {
        // 直接硬编码连接字符串，仅用于生成迁移
        var connectionString = "Host=localhost;Port=5432;Database=quartzui;Username=postgres;Password=password";

        // 配置DbContext
        var optionsBuilder = new DbContextOptionsBuilder<QuartzDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Chet.QuartzNet.EFCore.PostgreSQL");
        });

        return new QuartzDbContext(optionsBuilder.Options);
    }
}