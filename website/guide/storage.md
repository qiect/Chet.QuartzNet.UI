---
description: 对比文件存储与数据库存储，了解支持的数据库、连接配置、表结构与切换方式。
---

# 存储模式

## 1. 两种模式

| 维度 | 文件存储 | 数据库存储 |
| --- | --- | --- |
| 依赖 | 无 | 对应 NuGet 驱动包 + 数据库 |
| 配置 | 零配置 | 连接字符串 + Provider |
| 数据位置 | `App_Data/QuartzJobs/` 下的 JSON | `quartz_*` 表 |
| 适用规模 | 单机、轻量应用 | 中大型、生产环境 |
| 多实例共享 | 不支持 | 支持 |
| 备份 | 直接复制目录 | 数据库备份 |
| 迁移 | 可迁移到任意数据库 | 可迁移到文件或其他数据库 |

两种模式共用同一套 API 与管理界面，切换时不需要修改业务代码。

## 2. 文件存储

### 2.1 配置

```json
{
  "QuartzUI": {
    "StorageType": "File",
    "FileStoragePath": "App_Data/QuartzJobs",
    "JwtSecret": "...",
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

`StorageType` 省略时默认为文件存储。

### 2.2 数据文件

```text
App_Data/QuartzJobs/
├── jobs.json           # 作业定义与状态
├── logs.json           # 执行日志
├── notifications.json  # 通知历史
└── settings.json       # 系统设置
```

- 首次启动自动创建目录与文件；
- 升级版本时自动兼容旧数据，新增字段默认为空；
- 迁移前建议先停止应用并备份整个目录。

### 2.3 适用建议

- 单体应用、任务量不大；
- 想零依赖快速验证或内部工具；
- 可以接受「单实例写入」的约束。

如果在多实例 / 容器多副本环境中共用同一个文件目录，可能出现并发写入冲突，此时应使用数据库存储或为每个实例配置独立目录。

## 3. 数据库存储

### 3.1 安装驱动

| 数据库 | NuGet 包 | `DatabaseProvider` |
| --- | --- | --- |
| MySQL | `Chet.QuartzNet.EFCore.MySQL` | `mysql` |
| PostgreSQL | `Chet.QuartzNet.EFCore.PostgreSQL` | `postgresql` |
| SQL Server | `Chet.QuartzNet.EFCore.SqlServer` | `sqlserver` |
| SQLite | `Chet.QuartzNet.EFCore.SQLite` | `sqlite` |

```bash
dotnet add package Chet.QuartzNet.EFCore.SQLite
```

### 3.2 配置示例

::: code-group

```json [MySQL]
{
  "ConnectionStrings": {
    "QuartzUI": "Server=localhost;Database=quartzui;User=root;Password=123456;"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "mysql"
  }
}
```

```json [PostgreSQL]
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

```json [SQL Server]
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

```json [SQLite]
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

:::

### 3.3 表结构

| 表名 | 说明 |
| --- | --- |
| `quartz_jobs` | 作业定义、Cron、状态、依赖、重试与并发配置 |
| `quartz_job_logs` | 作业执行日志与结果 |
| `quartz_settings` | 系统设置（键值） |
| `quartz_notifications` | 通知发送历史 |

数据库表由 EF Core 迁移自动创建与升级。升级 NuGet 包后，如包含结构变更，需要执行：

```bash
dotnet ef database update
```

> 数据库结构变更通常由 EFCore 扩展包提供。升级主包时请同步升级对应扩展包，否则新增字段可能写入失败。

### 3.4 适用建议

- 生产环境或任务量较大；
- 需要多实例共享同一份作业数据；
- 已有数据库运维体系（备份、监控、权限）；
- 希望后续在不同数据库之间迁移。

## 4. 如何在两种模式间切换

推荐使用内置的**数据迁移**功能，而不是手工拷贝数据：

1. 安装目标数据库驱动包；
2. 在 `QuartzUI:Migration` 中配置目标数据库配置档（或直接使用当前数据库）；
3. 打开「系统配置 → 数据迁移」，选择源与目标，先执行预检；
4. 选择写入模式（合并 / 覆盖 / 替换）后开始迁移；
5. 迁移完成后按提示修改 `StorageType`、`DatabaseProvider` 与连接字符串并重启。

详见 [数据迁移](/guide/migration)。
