---
description: 5 分钟完成安装与集成：安装 NuGet 包、注入服务、启用中间件并访问管理界面。
---

# 快速开始

## 1. 环境要求

- .NET 8.0 或 .NET 10.0
- ASP.NET Core 8.0+ / 10.0+
- 任意支持的操作系统（Windows / Linux / macOS）

## 2. 安装 NuGet 包

文件存储模式只需要主包：

```bash
dotnet add package Chet.QuartzNet.UI
```

如果使用数据库存储，再按需安装对应的 EFCore 扩展包（版本请与主包保持一致）：

```bash
# MySQL
dotnet add package Chet.QuartzNet.EFCore.MySQL

# PostgreSQL
dotnet add package Chet.QuartzNet.EFCore.PostgreSQL

# SQL Server
dotnet add package Chet.QuartzNet.EFCore.SqlServer

# SQLite
dotnet add package Chet.QuartzNet.EFCore.SQLite
```

## 3. 注入服务

在 `Program.cs` 中添加：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 注册 Quartz UI（调度器、存储、认证、API）
builder.Services.AddQuartzUI(builder.Configuration);

// 可选：扫描并注册所有标记 [QuartzJob] 的 ClassJob
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

app.UseQuartz();        // 挂载 /quartz-ui 路由与静态资源
app.MapControllers();
app.Run();
```

## 4. 添加配置

在 `appsettings.json` 中加入 `QuartzUI` 配置节：

```json
{
  "QuartzUI": {
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

文件存储模式无需额外配置：

- 作业数据默认保存在应用目录下的 `App_Data/QuartzJobs/`；
- 首次启动会自动创建目录与数据文件；
- 账号密码即上面配置的 `UserName` / `Password`。

> `JwtSecret` 建议在生产环境中替换为随机生成的强密钥，不要直接使用示例值。

## 5. 启动并访问

```bash
dotnet run
```

启动后访问：

```text
http://localhost:5000/quartz-ui
```

使用配置中的账号密码登录即可进入管理界面。

![登录界面](/images/login.png)

## 6. 可选：使用数据库存储

只需补充连接字符串与存储配置：

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Server=localhost;Database=quartzui;User=root;Password=123456;"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "mysql",
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

`DatabaseProvider` 可选值：`mysql`、`postgresql`、`sqlserver`、`sqlite`。

数据库表会在启动时自动创建。更多细节见 [存储模式](/guide/storage)。

## 7. 可选：创建第一个 ClassJob

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "每 5 分钟执行一次")]
public class SampleJob : IJob
{
    private readonly ILogger<SampleJob> _logger;

    public SampleJob(ILogger<SampleJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("SampleJob 开始执行：{Time}", DateTime.Now);
        await Task.Delay(1000);
        _logger.LogInformation("SampleJob 执行完成");
    }
}
```

启动后作业会自动出现在作业列表中，无需在界面手动创建。详见 [ClassJob 代码作业](/guide/class-job)。

## 8. 下一步

- [配置说明](/guide/configuration)：完整配置项与认证说明。
- [作业管理](/modules/job)：了解作业类型、Cron 与重试。
- [通知配置](/guide/notification)：配置失败告警。
- [常见问题](/guide/faq)：集成过程中的高频问题。
