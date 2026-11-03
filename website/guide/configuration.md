---
description: 完整配置参考：存储、JWT 认证、文件路径、数据库迁移配置与配置优先级。
---

# 配置说明

## 1. 配置文件位置

Chet.QuartzNet.UI 读取宿主应用的 ASP.NET Core 配置系统，因此可以使用：

- `appsettings.json`
- `appsettings.{Environment}.json`
- 环境变量
- 命令行参数

所有配置集中在 `QuartzUI` 节下；数据库连接字符串放在 `ConnectionStrings` 节。

## 2. 最小配置

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

## 3. 配置项说明

### 3.1 认证

| 配置项 | 类型 | 说明 |
| --- | --- | --- |
| `JwtSecret` | string | JWT 签名密钥，生产环境务必替换 |
| `JwtExpiresInMinutes` | int | Token 有效期（分钟），默认 360 |
| `JwtIssuer` | string | JWT 签发者 |
| `JwtAudience` | string | JWT 接收方 |
| `UserName` | string | 登录用户名 |
| `Password` | string | 登录密码 |

::: warning 安全提示
内置认证是单账号的轻量方案，用于保护管理界面，不等同于完整的用户体系。若需要多用户、角色、审计等能力，应在网关或宿主应用中接入统一的身份认证。
:::

### 3.2 存储

| 配置项 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `StorageType` | string | `File` | 存储类型：`File` / `Database` |
| `DatabaseProvider` | string | 空 | 数据库提供程序：`mysql`、`postgresql`、`sqlserver`、`sqlite` |
| `FileStoragePath` | string | `App_Data/QuartzJobs` | 文件存储目录；数据库模式下仍作为文件迁移的默认路径 |

数据库存储还需要：

```json
{
  "ConnectionStrings": {
    "QuartzUI": "你的数据库连接字符串"
  }
}
```

### 3.3 数据迁移

`QuartzUI:Migration` 用于配置迁移面板中可选的数据库与文件来源：

```json
{
  "QuartzUI": {
    "Migration": {
      "BatchSize": 500,
      "ContinueOnError": false,
      "MaxErrorSamples": 20,
      "PauseSchedulerDuringMigration": false,
      "AllowReplace": false,
      "AllowAdHocConnectionString": false,
      "Profiles": {
        "sqlite-dev": {
          "Provider": "SQLite",
          "ConnectionString": "Data Source=quartzui.db"
        }
      },
      "FileSources": [
        {
          "Name": "历史文件数据",
          "Path": "D:\\QuartzBackup\\QuartzJobs"
        }
      ]
    }
  }
}
```

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Profiles` | 空 | 数据库配置档，键为配置档名称 |
| `FileSources` | 空 | 额外允许迁移的文件源目录 |
| `BatchSize` | 500 | 每批读写条数（1–2000） |
| `ContinueOnError` | false | 写入失败时是否继续后续批次 |
| `MaxErrorSamples` | 20 | 错误样本上限（1–200） |
| `PauseSchedulerDuringMigration` | false | 迁移期间是否暂停调度器 |
| `AllowReplace` | false | 是否允许「替换」写入模式 |
| `AllowAdHocConnectionString` | false | 是否允许 API 直接提交连接字符串 |

迁移的完整操作与安全建议见 [数据迁移](/guide/migration)。

## 4. 文件存储配置

```json
{
  "QuartzUI": {
    "StorageType": "File",
    "FileStoragePath": "App_Data/QuartzJobs",
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 360,
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

- 相对路径按应用工作目录解析；
- 目录不存在时自动创建；
- 作业、日志、通知、系统设置分别以 JSON 文件形式保存。

## 5. 数据库存储配置

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

| 提供程序 | `DatabaseProvider` | 连接字符串示例 |
| --- | --- | --- |
| MySQL | `mysql` | `Server=localhost;Database=quartzui;User=root;Password=123456;` |
| PostgreSQL | `postgresql` | `Host=localhost;Port=5432;Database=quartzui;Username=postgres;Password=123456;` |
| SQL Server | `sqlserver` | `Data Source=localhost;Initial Catalog=quartzui;User Id=sa;Password=123456;TrustServerCertificate=true;` |
| SQLite | `sqlite` | `Data Source=quartzui.db` |

存储选型建议见 [存储模式](/guide/storage)。

## 6. 界面中的系统配置

以下内容不在 `appsettings.json` 中，而是保存在存储里，可在「系统配置」页修改：

- 服务名称、环境标识、服务描述；
- 日志保留天数、日志最大条数、清理间隔；
- 数据迁移入口。

这些配置会随数据迁移一起复制，详见 [系统配置](/modules/system-config)。
