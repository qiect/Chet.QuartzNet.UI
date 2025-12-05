using Chet.QuartzNet.EFCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chet.QuartzNet.EFCore.SQLite.Migrations;

/// <summary>
/// 设计时DbContext工厂，用于生成迁移文件
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuartzDbContext>
{
    public QuartzDbContext CreateDbContext(string[] args)
    {
        // 直接硬编码连接字符串，仅用于生成迁移
        var connectionString = "Data Source=quartzui.db";

        // 配置DbContext
        var optionsBuilder = new DbContextOptionsBuilder<QuartzDbContext>();
        optionsBuilder.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly("Chet.QuartzNet.EFCore.SQLite");
        });

        return new QuartzDbContext(optionsBuilder.Options);
    }
}
