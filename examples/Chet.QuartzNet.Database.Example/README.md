# Quartz.Net UI SQL Server 数据库存储示例

本示例展示了如何在 Quartz.Net UI 中使用 SQL Server 数据库作为作业存储。

## 功能特性

- 使用 SQL Server 数据库持久化存储作业信息
- 支持通过配置文件灵活配置数据库连接
- 提供多种作业实现示例
- 包含完整的作业调度、执行和日志功能

## 配置说明

### 1. 数据库配置

在 `appsettings.json` 文件中配置 SQL Server 数据库连接：

```json
"ConnectionStrings": {
  "QuartzUI": "Data Source=172.30.7.208;User Id=sa;Password=Cast!@#45678910;Initial Catalog=QuartzNetTestDb;TrustServerCertificate=true;"
}
```

### 2. Quartz UI 配置

```json
"QuartzUI": {
  "StorageType": "Database",
  "DatabaseProvider": "SqlServer",
}
```

参数说明：
- `StorageType`: 存储类型，设置为 "Database" 表示使用数据库存储
- `DatabaseProvider`: 数据库提供程序，支持 SqlServer、MySQL、SQLite 等

## 程序配置

### 1. 服务注册

在 `Program.cs` 中配置 Quartz UI 和数据库支持：

```csharp
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器服务
builder.Services.AddControllers();

// 添加 Quartz UI 服务（读取 QuartzUI 节 & ConnectionStrings:QuartzUI，自动选择 DatabaseProvider）
builder.Services.AddQuartzUI(builder.Configuration);

```

### 2. 中间件配置

```csharp
var app = builder.Build();

// 启用 Quartz UI 中间件
app.UseQuartz();

app.MapControllers();

app.Run();
```

## 作业编写示例

### 1. 基本作业实现

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

namespace Chet.QuartzNet.UI.Test.Jobs
{
    /// <summary>
    /// 示例作业类
    /// </summary>
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
}
```

### 2. 定时作业示例

```csharp
/// <summary>
/// 数据同步作业类
/// </summary>
[QuartzJob("DataSyncJob", "SYNC", "0 0 2 * * ?", Description = "数据同步作业，每天凌晨2点执行")]
public class DataSyncJob : IJob
{
    private readonly ILogger<DataSyncJob> _logger;

    public DataSyncJob(ILogger<DataSyncJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("DataSyncJob开始执行数据同步");

        try
        {
            // 模拟数据同步过程
            await Task.Delay(5000);
            _logger.LogInformation("DataSyncJob数据同步完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataSyncJob数据同步失败");
            throw;
        }
    }
}
```

## 数据库迁移

详细的数据库迁移指南请参考：[DATABASE_MIGRATION_GUIDE.md](DATABASE_MIGRATION_GUIDE.md)

### 快速迁移命令

```bash
# 创建迁移文件
dotnet ef migrations add InitialCreate --project src\Chet.QuartzNet.EFCore\Chet.QuartzNet.EFCore.csproj --startup-project examples\Chet.QuartzNet.Test\Chet.QuartzNet.UI.Test.csproj

# 执行数据库更新
dotnet ef database update --project src\Chet.QuartzNet.EFCore\Chet.QuartzNet.EFCore.csproj --startup-project examples\Chet.QuartzNet.Test\Chet.QuartzNet.UI.Test.csproj
```

## 使用方法

### 1. 启动应用

```bash
dotnet run --project examples\Chet.QuartzNet.Test\Chet.QuartzNet.UI.Test.csproj
```

### 2. 访问 Quartz UI

打开浏览器访问：`http://localhost:5208/quartz-ui`

使用配置的用户名和密码登录：
- 用户名：Admin
- 密码：123456

### 3. 管理作业

在 Quartz UI 界面中，您可以：
- 添加新作业
- 编辑现有作业
- 删除作业
- 启用/禁用作业
- 立即执行作业
- 查看作业执行历史

## 注意事项

1. **数据库连接**：确保 SQL Server 数据库服务正常运行，且连接字符串正确
2. **权限问题**：确保数据库用户具有创建表、插入、更新、删除等权限
3. **SSL 证书**：如果遇到 SSL 证书问题，可以在连接字符串中添加 `TrustServerCertificate=true` 参数
4. **作业类**：作业类必须实现 `IJob` 接口
5. **依赖注入**：作业类支持通过构造函数注入依赖服务
6. **作业数据**：可以通过 JobDataMap 传递自定义数据给作业

## 常见问题

### 1. 启动时提示表不存在

这通常是因为没有执行数据库迁移命令。请按照上述 "数据库迁移" 部分的步骤执行迁移。

### 2. 数据库连接失败

检查以下几点：
- SQL Server 服务是否正常运行
- 连接字符串中的服务器地址、端口是否正确
- 用户名和密码是否正确
- 防火墙是否允许连接

### 3. 作业不执行

检查以下几点：
- 作业是否已启用
- Cron 表达式是否正确
- 作业类是否正确实现了 IJob 接口
- 日志中是否有错误信息

## 技术栈

- .NET 8.0
- Quartz.Net
- Entity Framework Core
- SQL Server


## 许可证

MIT License
