using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 配置CORS - 允许所有来源（开发环境）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});



// 配置Quartz UI - 文件存储版本（适合轻量应用）
builder.Services.AddQuartzUI();
// builder.Services.AddQuartzUI(options =>
// {
//     // 从配置中读取邮件设置
//     var emailSection = builder.Configuration.GetSection("QuartzUI:EmailOptions");
//     if (emailSection.Exists())
//     {
//         emailSection.Bind(options.EmailOptions);
//     }
// });
// 添加Basic认证服务
builder.Services.AddQuartzUIBasicAuthentication(builder.Configuration);
// 添加ClassJob自动注册
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 使用CORS策略
app.UseCors("AllowAll");

// 先启用认证中间件
app.UseQuartzUIBasicAuthorized();
// 然后启用Quartz UI中间件
app.UseQuartz();

app.MapControllers();

app.Run();