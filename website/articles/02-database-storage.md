---
title: "数据库存储模式详解"
description: "从文件存储切换到数据库存储：驱动安装、四种数据库配置、表结构、自动迁移与多实例注意事项。"
---

# 数据库存储模式详解

文件存储适合单机与快速验证，但数据量上来后，每次读写都要操作整份 JSON 文件，也不方便备份与统计。数据库存储把作业、日志、设置、通知拆到四张表，查询、分页和清理都交给数据库。

## 1. 两种存储的差异

| 维度 | 文件存储 | 数据库存储 |
| ---- | -------- | ---------- |
| 依赖 | 无 | 数据库 + 对应 EFCore 扩展包 |
| 配置 | 零配置 | 连接字符串 + `StorageType` + `DatabaseProvider` |
| 数据位置 | `App_Data/QuartzJobs/` 下的 JSON 文件 | `quartz_jobs` 等四张表 |
| 读写方式 | 整文件读写 | 按行读写，支持分页与聚合 |
| 多实例 | 同一目录并发写有风险 | 共享作业定义、日志与配置 |
| 适用场景 | 单机、内部工具、快速验证 | 生产环境、任务量较大 |

两种模式共用 `AddQuartzUI()` 与同一套界面，切换时不需要改业务代码。

## 2. 安装扩展包

按使用的数据库安装对应的包（版本与主包保持一致）：

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

## 3. 配置

公共部分是 `ConnectionStrings:QuartzUI` 与 `QuartzUI:StorageType`，差异只在 `DatabaseProvider` 和连接字符串。

### 3.1 MySQL

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
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

### 3.2 PostgreSQL

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Host=localhost;Port=5432;Database=quartzui;Username=postgres;Password=123456;"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "postgresql"
  }
}
```

### 3.3 SQL Server

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Data Source=localhost;Initial Catalog=quartzui;User Id=sa;Password=123456;TrustServerCertificate=true;"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "sqlserver"
  }
}
```

### 3.4 SQLite

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Data Source=quartzui.db"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "sqlite"
  }
}
```

`DatabaseProvider` 可选值：`mysql`、`postgresql`、`sqlserver`、`sqlite`（枚举绑定不区分大小写）。

## 4. 代码无需改动

```csharp
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddQuartzUI(builder.Configuration);
builder.Services.AddQuartzClassJobs();

var app = builder.Build();
app.MapControllers();
app.UseQuartz();
app.Run();
```

`AddQuartzUI()` 会读取 `StorageType`：为 `Database` 时按 `DatabaseProvider` 注册对应 DbContext 与存储实现，否则使用文件存储。

## 5. 表结构与自动迁移

启动时 `EFCoreJobStorage.InitializeAsync()` 会调用 `Database.MigrateAsync()`，自动创建表并应用扩展包内已包含的迁移，**不需要手动执行 `dotnet ef database update`**。

| 表名 | 内容 |
| ---- | ---- |
| `quartz_jobs` | 作业定义、Cron、状态、依赖、重试与并发配置 |
| `quartz_job_logs` | 执行日志、耗时、异常、触发来源、恢复信息 |
| `quartz_settings` | 键值型系统设置 |
| `quartz_notifications` | 通知发送历史 |

升级时需要注意：

1. 主包与 EFCore 扩展包版本必须一致，否则新字段可能写入失败；
2. 生产环境升级前先备份数据库；
3. 老版本代码读取新库会忽略多余列，结构本身可回滚。

## 6. 从文件迁移到数据库

有两条路径，按版本选择：

### 6.1 v2.2.0 的自动迁移

适合一次性、简单搬迁。切换为数据库存储后，在 `Program.cs` 追加：

```csharp
builder.Services.AddQuartzUI(builder.Configuration);
builder.Services.AddFileDataToDatabase();   // 启动时把 FileStoragePath 下的数据写入数据库
```

行为说明：

- 文件路径不存在 → 跳过；
- `StorageType` 不是 `Database` → 记录警告并跳过；
- 已存在的记录按主键跳过，不会重复插入；
- 只搬运数据，不修改配置。

### 6.2 v2.6.0 起的迁移向导

入口在「系统设置 → 数据迁移」，支持文件与四种数据库任意互转，提供预检、进度、三种写入模式与迁移后校验。推荐优先使用这条路径，原因见 [数据迁移](/guide/migration)。

## 7. 多实例注意事项

这是选型时最容易误解的一点：

- 数据库存储共享的是**作业定义、日志、系统设置与通知数据**；
- Quartz 仍使用 `UseInMemoryStore()`，每个实例都会加载并调度全部作业；
- 因此多副本同时运行时，同一个 Cron 作业会被多个实例各执行一次。

如果部署多个副本，需要自行保证只有一个实例承担调度（例如通过配置开关禁用其他实例的调度器），或者接受重复执行并在业务侧做幂等。它并不是 Quartz 集群模式。

## 8. 常见问题

| 问题 | 说明 |
| ---- | ---- |
| 启动报“未找到 QuartzUI 数据库连接字符串配置” | 检查 `ConnectionStrings:QuartzUI` 是否存在 |
| 表没有自动创建 | 确认已安装对应扩展包，且 `StorageType` 为 `Database` |
| 升级后写入失败 | 主包与 EFCore 扩展包版本不一致，同步升级并重启 |
| 切换数据库类型 | 修改 `DatabaseProvider` 与连接字符串后重启；数据需通过迁移向导搬运 |
| 数据库集群是否支持 | 数据库自身可以集群，但调度不是集群模式，见第 7 节 |
| 文件与数据库能否同时使用 | 不能，同一时刻只有一种存储生效 |

## 相关阅读

- [存储模式](/guide/storage)
- [配置说明](/guide/configuration)
- [数据迁移](/guide/migration)
- [v2.6.0 存储迁移](/articles/15-v2.6.0)
