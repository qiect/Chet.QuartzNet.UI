using Chet.QuartzNet.EFCore.PostgreSQL.Extensions;
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// 添加QuartzUI服务（包含邮件配置）
builder.Services.AddQuartzUI(options =>
{
    // 从配置文件读取邮件设置
    var emailConfig = builder.Configuration.GetSection("QuartzUI:EmailOptions");
    if (emailConfig.Exists())
    {
        emailConfig.Bind(options.EmailOptions);
    }
});
builder.Services.AddQuartzUIPostgreSQL(builder.Configuration);
// 从配置文件中添加数据库支持（SQL Server）
builder.Services.AddQuartzClassJobs();
// 添加Basic认证服务
builder.Services.AddQuartzUIAuthentication(builder.Configuration);

var app = builder.Build();

// 启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();

app.Run();
