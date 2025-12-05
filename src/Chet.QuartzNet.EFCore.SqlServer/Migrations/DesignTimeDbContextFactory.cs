using Chet.QuartzNet.EFCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chet.QuartzNet.EFCore.SqlServer.Migrations;

/// <summary>
/// 设计时DbContext工厂，用于生成迁移文件
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QuartzDbContext>
{
    public QuartzDbContext CreateDbContext(string[] args)
    {
        // 直接硬编码连接字符串，仅用于生成迁移
        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=quartzui;Trusted_Connection=True;MultipleActiveResultSets=true";

        // 配置DbContext
        var optionsBuilder = new DbContextOptionsBuilder<QuartzDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions =>
        {
            sqlServerOptions.MigrationsAssembly("Chet.QuartzNet.EFCore.SqlServer");
        });

        return new QuartzDbContext(optionsBuilder.Options);
    }
}
