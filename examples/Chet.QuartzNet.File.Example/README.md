# Chet.QuartzNet.Example - Quartz.Net 可视化管理示例项目

这是一个基于 Chet.QuartzNet.UI 的示例项目，展示了如何集成和使用 Quartz.Net 可视化管理功能。

## 功能特性

- **可视化管理界面**: 通过 Web 界面管理 Quartz 作业
- **ClassJob 模式支持**: 支持基于类的作业定义
- **多种存储方式**: 支持文件存储和数据库存储
- **JWT 认证**: 提供安全的JWT认证保护
- **ClassJob 自动注册**: 自动扫描和注册带有特定特性的作业类
- **示例作业**: 包含多个示例作业类，展示不同的作业实现方式

## 快速开始

### 1. 文件存储模式（当前示例使用此模式）

```csharp
// Program.cs
// 配置 Quartz UI（默认使用文件存储）
builder.Services.AddQuartzUI();
// 添加 ClassJob 自动注册，扫描当前程序集中的作业类
builder.Services.AddQuartzClassJobs();
```

### 2. 数据库存储模式（推荐用于中大型应用）

#### MySQL 数据库
```csharp
// 安装 Pomelo.EntityFrameworkCore.MySql 包
builder.Services.AddQuartzUIMySql(builder.Configuration.GetConnectionString("QuartzDB"));
```

#### PostgreSQL 数据库
```csharp
// 安装 Npgsql.EntityFrameworkCore.PostgreSQL 包
builder.Services.AddQuartzUIPostgreSQL(builder.Configuration.GetConnectionString("QuartzDB"));
```

#### SQL Server 数据库
```csharp
// 安装 Microsoft.EntityFrameworkCore.SqlServer 包
builder.Services.AddQuartzUISqlServer(builder.Configuration.GetConnectionString("QuartzDB"));
```

#### SQLite 数据库
```csharp
// 安装 Microsoft.EntityFrameworkCore.Sqlite 包
builder.Services.AddQuartzUISQLite(builder.Configuration.GetConnectionString("QuartzDB"));
```

### 3. 配置连接字符串

在 `appsettings.json` 中配置数据库连接字符串：

```json
{
  "ConnectionStrings": {
    "QuartzDB": "server=localhost;database=quartz_db;User Id=root;PWD=password;"
  }
}
```

### 4. 启用 JWT 认证（当前示例已启用）

```csharp
// Program.cs
// 配置 Quartz UI 服务时自动启用 JWT 认证
builder.Services.AddQuartzUI();

// 在 appsettings.json 中配置 JWT 相关选项
"QuartzUI": {
  "EnableJwtAuth": true,
  "UserName": "Admin",
  "Password": "123456",
  "JwtSecret": "your-secret-key-change-this-in-production",
  "JwtExpiresInMinutes": 30,
  "JwtIssuer": "Chet",
  "JwtAudience": "Chet.QuartzNet.UI"
}
```

### 5. 启用中间件

```csharp
// Program.cs
// 先启用认证中间件
app.UseQuartzUIBasicAuthorized();
// 然后启用 Quartz UI 中间件
app.UseQuartz();

```

## 作业定义

### ClassJob 模式（当前示例使用此模式）

使用 `[QuartzJob]` 特性定义作业类，系统会自动扫描并注册带有此特性的作业类：

```csharp
// 示例作业类
[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "这是一个示例作业，每5分钟执行一次")]
public class SampleJob : IJob
{
    private readonly ILogger<SampleJob> _logger;

    public SampleJob(ILogger<SampleJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("SampleJob开始执行，执行时间: {ExecuteTime}", DateTime.Now);

        try
        {
            // 模拟业务逻辑处理
            await Task.Delay(1000);

            // 获取作业数据
            var jobData = context.JobDetail.JobDataMap;
            if (jobData.ContainsKey("customData"))
            {
                _logger.LogInformation("获取到自定义数据: {CustomData}", jobData.GetString("customData"));
            }

            _logger.LogInformation("SampleJob执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SampleJob执行失败");
            throw;
        }
    }
}

// 数据同步作业示例
[QuartzJob("DataSyncJob", "SYNC", "0 0 2 * * ?", Description = "数据同步作业，每天凌晨2点执行")]
public class DataSyncJob : IJob
{
    // 作业实现...
}

// 报表生成作业示例
[QuartzJob("ReportJob", "REPORT", "0 30 8 * * ?", Description = "报表生成作业，每天早上8:30执行")]
public class ReportJob : IJob
{
    // 作业实现...
}
```

### ClassJob 特性参数说明

- **作业名称**: 作业的唯一标识符
- **作业组**: 作业所属的分组
- **Cron 表达式**: 作业执行的时间规则
- **描述**: 作业的说明文字（可选）

### Cron 表达式说明

- `0 0/5 * * *?` - 每5分钟执行一次
- `0 0 2 * *?` - 每天凌晨2点执行
- `0 30 8 * *?` - 每天早上8:30执行

更多 Cron 表达式请参考 [Quartz Cron 表达式文档](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html)

## 访问管理界面

1. 启动应用：
   ```bash
   dotnet run --project examples\Chet.QuartzNet.Example\Chet.QuartzNet.Example.csproj
   ```

2. 访问 Quartz UI：
   ```
   http://localhost:5208/quartz-ui
   ```

3. 输入 Basic 认证信息（当前示例配置）：
   - 用户名：Admin
   - 密码：123456

在 Quartz UI 界面中，您可以：
- 查看所有已注册的作业
- 添加、编辑和删除作业
- 启用/禁用作业
- 立即执行作业
- 查看作业执行历史

## 示例 API

项目提供了以下测试 API：

- `GET /api/weatherforecast` - 获取天气预报数据（示例 API）

## 数据库支持

Chet.QuartzNet.UI 支持多种数据库：

- **MySQL**: 使用 Pomelo.EntityFrameworkCore.MySql
- **PostgreSQL**: 使用 Npgsql.EntityFrameworkCore.PostgreSQL
- **SQL Server**: 使用 Microsoft.EntityFrameworkCore.SqlServer
- **SQLite**: 使用 Microsoft.EntityFrameworkCore.Sqlite

## 依赖包

根据使用的数据库类型，需要安装对应的 EF Core 提供程序包：

```xml
<!-- MySQL -->
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.0" />

<!-- PostgreSQL -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />

<!-- SQL Server -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />

<!-- SQLite -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
```

## 注意事项

1. **存储方式**: 当前示例使用文件存储，作业数据默认保存在应用程序目录的 `quartz-jobs.json` 文件中
2. **数据库存储**: 如果需要使用数据库存储，请参考示例代码中的数据库存储配置部分
3. **ClassJob 自动注册**: 系统会自动扫描并注册当前程序集中带有 `[QuartzJob]` 特性的作业类
4. **认证保护**: 当前示例已启用 JWT 认证，建议在生产环境中始终启用认证保护
5. **依赖注入**: 作业类支持通过构造函数注入依赖服务（如 ILogger）
6. **作业数据**: 可以通过 JobDataMap 传递自定义数据给作业

## 当前项目配置

### appsettings.json 配置

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "QuartzDB": "server=localhost;database=quartz_db;User Id=root;PWD=password;"
  },
  "QuartzUI": {
    "EnableJwtAuth": true,
    "UserName": "Admin",
    "Password": "123456",
    "JwtSecret": "your-secret-key-change-this-in-production",
    "JwtExpiresInMinutes": 30,
    "JwtIssuer": "Chet",
    "JwtAudience": "Chet.QuartzNet.UI"
  }
}
```

### Program.cs 配置

```csharp
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

// 配置 Quartz UI（默认使用文件存储，自动启用JWT认证）
builder.Services.AddQuartzUI();
// 添加 ClassJob 自动注册
builder.Services.AddQuartzClassJobs();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 使用CORS策略
app.UseCors("AllowAll");

// 启用Quartz UI中间件（JWT认证自动集成）
app.UseQuartz();

app.MapControllers();

app.Run();
```

## 许可证

MIT License