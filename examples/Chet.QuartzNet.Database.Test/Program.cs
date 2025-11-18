using Chet.QuartzNet.UI.Extensions;
using Chet.QuartzNet.EFCore.Extensions;

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

// 从配置文件中添加数据库支持（SQL Server）
builder.Services.AddQuartzUIDatabaseFromConfiguration(builder.Configuration);

// 添加Basic认证服务
builder.Services.AddQuartzUIBasicAuthentication(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

// 启用Basic认证中间件
app.UseQuartzUIBasicAuthorized();

// 启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();

app.Run();
