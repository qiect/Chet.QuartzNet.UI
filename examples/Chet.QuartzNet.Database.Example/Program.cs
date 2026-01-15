using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// 添加QuartzUI服务（配置驱动，自动根据 StorageType/DatabaseProvider 选择存储）
builder.Services.AddQuartzUI(builder.Configuration);
// 添加ClassJob自动注册
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/quartz-ui"));

// 启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();

app.Run();
