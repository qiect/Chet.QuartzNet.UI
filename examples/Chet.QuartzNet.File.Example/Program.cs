using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();


// 添加QuartzUI服务，配置Quartz UI - 文件存储版本（适合轻量应用）
builder.Services.AddQuartzUI(builder.Configuration);
// 添加ClassJob自动注册
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/quartz-ui"));

// 然后启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();

app.Run();