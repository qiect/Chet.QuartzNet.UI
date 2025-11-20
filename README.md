# Chet.QuartzNet.UI - Quartz.Net 可视化管理库

Chet.QuartzNet.UI 是一个基于 .NET 8.0 开发的轻量级 Quartz.Net 可视化管理库，提供了完整的任务调度管理功能，支持文件存储和数据库存储两种模式。该组件库旨在简化 Quartz.Net 的使用门槛，提供直观、易用的可视化管理界面，使开发人员能够轻松地创建、管理、监控和调试定时作业，无需深入了解 Quartz.Net 的复杂 API。

通过 Chet.QuartzNet.UI，您可以快速集成任务调度功能到现有项目中，实现作业的可视化配置、实时监控和历史记录查询，大大提高开发效率和运维便利性。

## ✨ 功能特性

- 🔧 **可视化管理 Quartz 作业**：通过 Web 界面管理 Quartz 作业、触发器和调度器
- 📊 **实时监控**：实时查看作业执行状态和日志
- 🎯 **ClassJob 模式支持**：支持基于类的作业定义，简化作业创建
- ✅ **ClassJob 自动注册**：自动扫描和注册带有特定特性的作业类
- 💾 **多种存储方式**：支持文件存储和数据库存储（MySQL、PostgreSQL、SQL Server、SQLite）
- 🔐 **认证保护**：提供 Basic 认证保护管理界面
- 📦 **RCL 打包**：使用 Razor Class Library 打包，无侵入集成
- 🚀 **快速集成**：简单配置即可集成到现有项目
- 🎨 **现代化 UI**：基于 Ant Design Vue，界面美观易用
- 📱 **响应式设计**：支持移动端访问
- 🌙 **暗色主题**：支持系统暗色主题
- 📝 **作业执行历史**：记录作业执行历史和结果
- 🎯 **数据同步功能**：支持数据同步作业示例
- 📊 **报表生成功能**：支持报表生成作业示例
- ⏱️ **灵活的时间调度**：支持 Cron 表达式和多种触发器类型

## 📦 安装

### NuGet 安装

根据需要选择安装的包：

```bash
# 主包（包含核心功能和文件存储）
dotnet add package Chet.QuartzNet.UI

# 如果需要数据库存储支持
dotnet add package Chet.QuartzNet.EFCore

# 数据库提供程序（根据需要选择）
dotnet add package Pomelo.EntityFrameworkCore.MySql        # MySQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL    # PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.SqlServer   # SQL Server
dotnet add package Microsoft.EntityFrameworkCore.Sqlite      # SQLite
```

### 依赖要求

- .NET 8.0
- ASP.NET Core 8.0+
- Quartz.NET 3.8.1+ - 作业调度核心库
- Entity Framework Core 8.0+（可选，用于数据库存储）
- Ant Design Vue - UI 组件库
- Vue.js 3 - 前端框架

## 🚀 快速开始

### 1. 基本配置

在 `Program.cs` 中添加服务：

```csharp
// 添加 Quartz UI 服务（文件存储模式）
builder.Services.AddQuartzUI();

// 添加 ClassJob 支持（可选）
builder.Services.AddQuartzClassJobs();

// 启用中间件
app.UseQuartz();
```

### 2. 数据库存储配置

#### MySQL

```csharp
// 安装 Pomelo.EntityFrameworkCore.MySql
dotnet add package Pomelo.EntityFrameworkCore.MySql

// 配置服务
builder.Services.AddQuartzUIMySql(connectionString);
```

#### PostgreSQL

```csharp
// 安装 Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

// 配置服务
builder.Services.AddQuartzUIPostgreSQL(connectionString);
```

#### SQL Server

```csharp
// 安装 Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.SqlServer

// 配置服务
builder.Services.AddQuartzUISqlServer(connectionString);
```

#### SQLite

```csharp
// 安装 Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

// 配置服务
builder.Services.AddQuartzUISQLite(connectionString);
```

### 3. 访问管理界面

启动应用后，访问 `/quartz-ui` 即可进入管理界面。

## 📋 使用示例

### 创建 ClassJob

使用 `[QuartzJob]` 特性定义作业类：

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "示例作业，每5分钟执行一次")]
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
            // 业务逻辑处理
            await Task.Delay(1000);
            _logger.LogInformation("SampleJob执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SampleJob执行失败");
            throw;
        }
    }
}
```

### 注册 ClassJob

```csharp
// 自动扫描并注册所有标记了 QuartzJob 特性的类
builder.Services.AddQuartzClassJobs();
```

### 认证配置

启用 Basic 认证：

```csharp
// 添加认证服务
builder.Services.AddQuartzUIBasicAuthentication(builder.Configuration);

// 在 appsettings.json 中配置用户名密码
"QuartzUI": {
  "EnableBasicAuth": true,
  "UserName": "admin",
  "Password": "password"
}

// 启用认证中间件（在 UseQuartz 之前）
app.UseQuartzUIBasicAuthorized();
app.UseQuartz();
```

## 🎯 界面功能

### 作业管理
- 📋 作业列表展示（支持分页、搜索、筛选）
- ➕ 添加新作业（支持Cron表达式验证）
- ✏️ 编辑现有作业
- 🔄 触发作业（立即执行）
- ⏸️ 暂停/恢复作业
- 🗑️ 删除作业
- 📊 查看作业状态（正常、暂停、完成、错误、阻塞）
- 📝 查看作业执行历史记录

### 执行日志
- 📜 查看作业执行历史
- 🔍 按状态筛选日志（运行中、成功、失败）
- ⏱️ 查看执行耗时
- ❌ 查看错误信息
- 📋 分页显示日志记录

### 调度器状态
- 🟢 实时显示调度器运行状态
- 📈 自动刷新状态信息
- 📊 显示当前活跃作业数量

### 作业类型支持
- 🎯 ClassJob 模式：基于类的作业定义
- ⚙️ TriggerJob 模式：基于触发器的作业定义
- 🔄 支持多种触发器类型（Cron、Simple、Calendar等）

## 🔧 配置选项

### 数据库配置

支持多种数据库，通过不同的扩展方法配置：

```csharp
// MySQL
services.AddQuartzUIMySql(options);

// PostgreSQL  
services.AddQuartzUIPostgreSQL(options);

// SQL Server
services.AddQuartzUISqlServer(options);
```

### 授权配置

```json
{
  "QuartzUI": {
    "EnableBasicAuth": true,
    "UserName": "自定义用户名",
    "Password": "自定义密码"
  }
}
```

## 📁 项目结构

```
Chet.QuartzNet.UI/
├── src/
│   ├── Chet.QuartzNet.Core/          # 核心服务和功能
│   │   ├── Attributes/               # 作业特性定义
│   │   ├── Configuration/            # 配置类
│   │   ├── Interfaces/               # 接口定义
│   │   ├── Jobs/                     # 作业相关功能
│   │   └── Services/                 # 核心服务实现
│   ├── Chet.QuartzNet.EFCore/        # EF Core 数据访问层
│   │   ├── Data/                     # 数据库上下文
│   │   ├── Extensions/               # 扩展方法
│   │   ├── Migrations/               # 数据库迁移
│   │   └── Services/                 # 数据库存储服务
│   ├── Chet.QuartzNet.Models/        # 数据模型和 DTO
│   │   ├── DTOs/                     # 数据传输对象
│   │   └── Entities/                 # 实体类
│   └── Chet.QuartzNet.UI/            # UI 组件和控制器
│       ├── Controllers/              # API 控制器
│       ├── Extensions/               # 扩展方法
│       ├── Middleware/               # 中间件
│       └── wwwroot/                  # 静态资源
├── examples/
│   ├── Chet.QuartzNet.Example/       # 文件存储示例项目
│   └── Chet.QuartzNet.Test/          # 数据库存储示例项目
├── docs/                             # 项目文档
│   ├── README.md                     # 项目简介和使用文档
│   └── PUBLISHING.md                 # 发布指南
├── README.md                         # 项目根目录文档
├── LICENSE                           # 许可证文件
├── build-nuget.bat                   # Windows 构建脚本
└── Chet.QuartzNet.UI.sln             # 解决方案文件
```

## 🚀 开发计划

- [x] 核心功能实现
- [x] EFCore数据访问层
- [x] Ant Design Vue前端界面
- [x] Basic授权支持
- [x] Razor Class Library打包
- [x] 示例项目
- [x] 邮件通知功能
- [ ] 更多数据库支持
- [ ] 作业分组管理
- [ ] 批量操作功能
- [ ] 作业执行统计图表

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 📝 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🆘 支持与联系

如有问题或建议，请通过以下方式联系：

- 提交 Issue
- 发送邮件
- 参与讨论

## 🙏 致谢

- [Quartz.Net](https://www.quartz-scheduler.net/) - 优秀的任务调度框架
- [Ant Design Vue](https://www.antdv.com/) - 美观的UI组件库
- [.NET](https://dotnet.microsoft.com/) - 强大的开发平台

---

**⭐ 如果这个项目对您有帮助，请给个 Star 支持一下！**
