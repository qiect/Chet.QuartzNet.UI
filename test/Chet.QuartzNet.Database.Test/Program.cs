using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 配置Quartz UI
builder.Services.AddQuartzUI(builder.Configuration);
// 添加ClassJob自动注册
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

// 然后启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();


app.Run();

