# Chet.QuartzNet.UI 可视化作业调度管理系统

Chet.QuartzNet.UI 是一个基于 .NET 8.0 和VbenAdmin 框架开发的可视化作业调度管理系统，提供了完整的任务调度管理功能，支持文件存储和数据库存储两种模式。该组件库旨在简化 Quartz.Net 的使用门槛，提供直观、易用的可视化管理界面，使开发人员能够轻松地创建、管理、监控和调试定时作业，无需深入了解 Quartz.Net 的复杂 API。

通过 Chet.QuartzNet.UI，您可以快速集成任务调度功能到现有项目中，实现作业的可视化配置、实时监控和历史记录查询，大大提高开发效率和运维便利性。

## ✨ 功能特性

- 🔧 **可视化管理 Quartz 作业**：通过 Web 界面管理 Quartz 作业、触发器和调度器
- 📊 **实时监控**：实时查看作业执行状态和日志
- 🎯 **ClassJob 模式支持**：支持基于类的作业定义，简化作业创建
- ✅ **ClassJob 自动注册**：自动扫描和注册带有特定特性的作业类
- 💾 **多种存储方式**：支持文件存储和数据库存储（MySQL、PostgreSQL、SQL Server、SQLite）
- 📝 **作业执行历史**：记录作业执行历史和结果
- 🔔 **PushPlus 通知集成**：支持多种渠道的通知推送（微信、企业微信、钉钉、邮件等）
- 📋 **通知模板支持**：支持 HTML、Markdown、纯文本三种通知模板
- 🎛️ **灵活的通知策略**：可配置作业成功/失败、调度器异常时的通知规则
- 📜 **通知历史管理**：完整的通知发送历史记录
- 🔐 **认证保护**：提供 JWT 认证保护管理界面
- 📦 **RCL 打包**：使用 Razor Class Library 打包，无侵入集成
- 🚀 **快速集成**：简单配置即可集成到现有项目
- 🎨 **现代化 UI**：基于 VbenAdmin 框架，界面美观易用
- 📱 **响应式设计**：支持移动端访问

## 📦 安装

### NuGet 安装

根据需要选择安装的包：

```bash
# 主包（包含核心功能和文件存储）
dotnet add package Chet.QuartzNet.UI

# 如果需要数据库存储支持（核心包）
# 数据库扩展包（根据需要选择其中一个）
dotnet add package Chet.QuartzNet.EFCore.MySql        # MySQL 支持
dotnet add package Chet.QuartzNet.EFCore.PostgreSql    # PostgreSQL 支持
dotnet add package Chet.QuartzNet.EFCore.SqlServer   # SQL Server 支持
dotnet add package Chet.QuartzNet.EFCore.SQLite      # SQLite 支持
```

### 依赖要求

- .NET 8.0
- ASP.NET Core 8.0+
- Quartz.NET 3.8.1+ - 作业调度核心库
- Entity Framework Core 8.0+（可选，用于数据库存储）

## 🚀 快速开始

### 1. 存储模式选择

系统支持两种存储模式：**文件存储**和**数据库存储**，均可通过同一套 API 进行配置和使用。

#### 1.1 文件存储模式（轻量级应用）

**添加代码**
需要再 `Program.cs` 中添加以下代码：

```csharp
// Program.cs
// 添加 Quartz UI 服务（文件存储模式）
builder.Services.AddQuartzUI(builder.Configuration);
// 可选：ClassJob 自动扫描注册
builder.Services.AddQuartzClassJobs();
// 启用中间件
app.UseQuartz();
```
**配置说明**：
需要在 `appsettings.json` 中添加以下配置：

```json
// appsettings.json
  "QuartzUI": {
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
```

**配置说明**：
- 文件存储模式无需额外配置，系统会自动使用文件存储
- 自动读取 `QuartzUI` 节中的 JWT 认证配置
- 作业数据存储在应用目录下的 `App_Data/QuartzJobs/` 文件夹下

#### 1.2 数据库存储模式（中大型应用）

**添加代码**
需要再 `Program.cs` 中添加以下代码：

```csharp
// Program.cs
// 添加 Quartz UI 服务（数据库存储模式）
builder.Services.AddQuartzUI(builder.Configuration);
// 可选：ClassJob 自动扫描注册
builder.Services.AddQuartzClassJobs();
// 启用中间件
app.UseQuartz();
```

**配置说明**：
需要在 `appsettings.json` 中添加以下配置：

```json
// appsettings.json
  "ConnectionStrings": {
    "QuartzUI": "Server=localhost;Database=quartzui;User=root;Password=123456;" //MySQL 数据库连接字符串
    //"QuartzUI": "Data Source=quartzui.db"
  },
  "QuartzUI": {
    "StorageType": "Database", // 指定使用数据库存储
    "DatabaseProvider": "mysql", // 可选，数据库提供者：mysql、postgresql、sqlserver、sqlite
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
```

**数据库支持**：
- ✅ MySQL
- ✅ PostgreSQL
- ✅ SQL Server
- ✅ SQLite


### 2. 访问管理界面

启动应用后，访问 `/quartz-ui` 即可进入管理界面。

## 📋 ClassJob 使用示例

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

## 🎯 界面功能

### 分析页
- 📊 **作业统计概览**：总作业数、启用/禁用作业数、正在执行作业数、成功/失败执行次数
- 📈 **作业状态分布**：各状态作业数量和百分比的饼图展示
- 📋 **作业类型分布**：各类型作业数量和百分比的饼图展示
- 📉 **作业执行趋势**：作业执行趋势的折线图展示
- ⏱️ **作业执行耗时**：作业执行耗时的柱状图展示

### 作业管理
- 📋 作业列表展示（支持分页、搜索、筛选）
- ➕ 添加新作业（支持Cron表达式验证）
- ✏️ 编辑现有作业
- 🔄 触发作业（立即执行）
- ⏸️ 暂停/恢复作业
- 🗑️ 删除作业
- 📊 查看作业状态（正常、暂停、完成、错误、阻塞）

### 日志管理
- 📜 查看作业执行历史
- 🔍 按状态筛选日志（运行中、成功、失败）
- ⏱️ 查看执行耗时
- ❌ 查看错误信息
- 📋 分页显示日志记录
- 🗑️ 删除日志记录

### 通知管理
- 🔔 **通知配置**：配置 PushPlus Token、推送渠道、消息模板等
- 🎛️ **通知策略**：设置作业成功/失败、调度器异常时的通知规则
- 📋 **通知历史**：查看所有通知发送记录
- 🔍 **通知筛选**：按状态、触发来源筛选通知
- 🗑️ **通知清理**：删除单条或批量清空通知记录
- 📤 **测试通知**：发送测试通知验证配置是否正确

### 调度器状态
- 🟢 实时显示调度器运行状态
- 📈 自动刷新状态信息
- 📊 显示当前活跃作业数量

### 作业类型支持
- 🎯 DLL 模式：基于类的作业定义
- ⚙️ API 模式：基于 API 调用的作业定义

## 🔧 数据库配置

系统支持通过不同的扩展包来支持多种数据库。您需要根据实际使用的数据库安装对应的扩展包：

| 数据库类型 | 扩展包名称 | 安装命令 |
|-----------|-----------|----------|
| MySQL | Chet.QuartzNet.EFCore.MySql | `Install-Package Chet.QuartzNet.EFCore.MySql` 或 `dotnet add package Chet.QuartzNet.EFCore.MySql` |
| PostgreSQL | Chet.QuartzNet.EFCore.PostgreSQL | `Install-Package Chet.QuartzNet.EFCore.PostgreSQL` 或 `dotnet add package Chet.QuartzNet.EFCore.PostgreSQL` |
| SQL Server | Chet.QuartzNet.EFCore.SqlServer | `Install-Package Chet.QuartzNet.EFCore.SqlServer` 或 `dotnet add package Chet.QuartzNet.EFCore.SqlServer` |
| SQLite | Chet.QuartzNet.EFCore.SQLite | `Install-Package Chet.QuartzNet.EFCore.SQLite` 或 `dotnet add package Chet.QuartzNet.EFCore.SQLite` |


## 📁 项目结构

```
Chet.QuartzNet.UI/
├── src/
│   ├── Chet.QuartzNet.EFCore/                # EF Core 数据访问层核心
│   │   ├── Data/                           # 数据库上下文
│   │   ├── Extensions/                      # 扩展方法
│   │   └── Services/                        # 数据库存储服务
│   ├── Chet.QuartzNet.EFCore.MySql/          # MySQL 数据库支持
│   ├── Chet.QuartzNet.EFCore.PostgreSql/     # PostgreSQL 数据库支持
│   ├── Chet.QuartzNet.EFCore.SQLite/         # SQLite 数据库支持
│   ├── Chet.QuartzNet.EFCore.SqlServer/     # SQL Server 数据库支持
│   ├── Chet.QuartzNet.Models/               # 数据模型和 DTO
│   │   ├── DTOs/                            # 数据传输对象
│   │   └── Entities/                         # 实体类
│   ├── Chet.QuartzNet.UI/                   # UI 组件和控制器
│   │   ├── Controllers/                     # API 控制器
│   │   ├── Extensions/                      # 扩展方法
│   │   ├── Middleware/                      # 中间件
│   │   └── wwwroot/                         # 静态资源
│   └── Chet.QuartzNet.Web/                  # Web 应用示例
├── examples/
│   ├── Chet.QuartzNet.Database.Example/      # 数据库存储示例项目
│   └── Chet.QuartzNet.File.Example/          # 文件存储示例项目
├── README.md                                # 项目根目录文档
├── LICENSE                                  # 许可证文件
└── Chet.QuartzNet.UI.sln                    # 解决方案文件
```

## 📝 更新说明

### [1.3.0] - 2025-12-17

#### 修复
- 修复使用数据库存储方式时 Nuget 包源的问题

#### 兼容性
- 与现有版本完全兼容
- 无需数据库迁移或配置更改

### [1.2.4] - 2025-12-16

#### 优化
- 更换 Logo
- 打包时添加 README.md 文件

#### 兼容性
- 与现有版本完全兼容
- 无需数据库迁移或配置更改

### [1.1.4] - 2025-12-14

#### 修复
- 修复使用数据库存储方式时迁移失败的问题

#### 优化
- 操作时添加提醒

#### 兼容性
- 与现有版本完全兼容
- 无需数据库迁移或配置更改


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
- [VbenAdmin](https://www.vben.pro/) - 美观的UI组件库
- [.NET](https://dotnet.microsoft.com/) - 强大的开发平台

---

**⭐ 如果这个项目对您有帮助，请给个 Star 支持一下！**
