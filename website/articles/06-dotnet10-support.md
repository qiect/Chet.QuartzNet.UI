---
title: "支持 .NET 10 框架"
description: "多目标框架的实现方式、依赖版本差异、在 .NET 10 项目中的接入步骤与 AOT 说明。"
---

# 支持 .NET 10 框架

从 **v2.0.0**（2026-01-15）起，Chet.QuartzNet.UI 同时面向 `net8.0` 与 `net10.0` 编译。这一篇说明它具体是怎么支持的，以及在 .NET 10 项目中如何接入。

## 1. 支持方式

所有包都使用多目标框架：

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

NuGet 会根据宿主项目的目标框架自动选择对应资产。对使用者来说：

- 项目是 `net8.0` → 使用 net8.0 资产；
- 项目是 `net10.0` → 使用 net10.0 资产；
- 项目是 `net9.0` → 没有专门资产，会回退到 net8.0（未单独验证）。

管理界面的静态资源只打包一份，与目标框架无关。

## 2. 依赖版本差异

| 依赖 | net8.0 | net10.0 |
| ---- | ------ | ------- |
| Quartz / Quartz.AspNetCore | 3.22.10 | 3.22.10 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.34 | 10.0.0 |
| Microsoft.EntityFrameworkCore.Design | 8.0.34 | 9.0.23 |
| Pomelo.EntityFrameworkCore.MySql | 8.0.3 | 9.0.0 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 | 10.0.4 |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.34 | 10.0.15 |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.34 | 10.0.15 |

SQLite、SQL Server、PostgreSQL 在 .NET 10 下已声明 EF Core 10.x；MySQL 受 Pomelo 限制（其依赖区间为 `9.0.x`）仍停留在 EF Core 9，因此**不要在同一个应用里同时引用 MySQL 与其他数据库扩展包**，否则还原会报版本冲突。库自身在 net10.0 下按 EF Core 9.0.23 编译，其余提供程序声明的 10.x 运行时向下兼容，已实测可正常运行。

## 3. 在 .NET 10 项目中接入

### 3.1 创建项目

```bash
dotnet new web -n MyQuartzApp -f net10.0
cd MyQuartzApp
```

### 3.2 安装包

```bash
dotnet add package Chet.QuartzNet.UI --version 2.8.1
```

数据库存储再安装对应扩展包，版本保持一致：

```bash
dotnet add package Chet.QuartzNet.EFCore.SQLite --version 2.8.1
```

### 3.3 注册服务

```csharp
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddQuartzUI(builder.Configuration);
builder.Services.AddQuartzClassJobs();   // 可选

var app = builder.Build();

app.MapControllers();
app.UseQuartz();
app.Run();
```

### 3.4 配置

```json
{
  "QuartzUI": {
    "StorageType": "File",
    "FileStoragePath": "App_Data/QuartzJobs",
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

### 3.5 运行

```bash
dotnet run
```

访问 `http://localhost:{端口}/quartz-ui`。端口以 `Properties/launchSettings.json` 或 `ASPNETCORE_URLS` 为准；仓库示例中文件存储为 5001、数据库存储为 5002。

## 4. 升级现有项目

1. 将宿主项目目标框架改为 `net10.0`；
2. 主包与所有 `Chet.QuartzNet.EFCore.*` 扩展包升级到同一版本；
3. 数据库存储模式启动时会自动执行 EF Core 迁移，升级前先备份；
4. 若引用了 `Microsoft.AspNetCore.Authentication.JwtBearer`，版本需与目标框架匹配。

代码层面无需修改：注册方式、配置节、路由都保持不变。

## 5. 可以使用的新语法

支持 .NET 10 不等于必须改代码，但可以使用新的 C# 语法。例如主构造函数：

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "示例作业")]
public class SampleJob(ILogger<SampleJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("SampleJob 执行时间：{Time}", DateTime.Now);
        await Task.CompletedTask;
    }
}
```

这些语法在 net8.0 下同样可用（取决于 LangVersion），与是否支持 .NET 10 没有绑定关系。

## 6. 关于 AOT 与裁剪

**当前不支持 Native AOT，也没有针对裁剪做适配。** 原因很直接：

- Quartz.Net 通过反射创建作业实例；
- ClassJob 扫描依赖 `Assembly.GetTypes()` 与自定义特性；
- EF Core 与各数据库驱动大量使用反射和动态代码。

因此以下配置不建议开启：

```xml
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
```

如果需要更小的发布体积，可以使用自包含发布或 ReadyToRun，但应完整回归作业注册、执行与迁移功能。

## 7. 常见问题

| 问题 | 处理 |
| ---- | ---- |
| 还原包时找不到 net10.0 资产 | 升级到 v2.0.0 及以上版本 |
| 升级后数据库写入报列不存在 | 同步升级 EFCore 扩展包并重启，触发自动迁移 |
| 启动报 JwtBearer 版本冲突 | 将 JwtBearer 对齐到 10.0.0 |
| 发布后作业类扫描不到 | 确认作业类为 `public`、实现了 `IJob` 且程序集已加载 |
| 能否用 .NET 9 | 可以用，但会回退到 net8.0 资产，官方只验证 net8.0 / net10.0 |

## 相关阅读

- [快速开始](/guide/quick-start)
- [部署与升级](/guide/deployment)
- [更新日志 · 2.0.0](/changelog)
