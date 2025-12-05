# 集成式 EFCore 使用指南

## 1. 概述

Chet.QuartzNet.EFCore 包已经集成到 Chet.QuartzNet.UI 包中，您无需单独安装 EFCore 包即可使用数据库存储和迁移功能。

## 2. 功能可用性

### 2.1 可用功能

- ✅ 数据库迁移的应用（Update-Database）
- ✅ 自动迁移（通过代码方式）
- ✅ 数据库存储的所有功能
- ✅ 所有数据库提供程序支持（SQL Server、MySQL、PostgreSQL、SQLite）

### 2.2 限制

- ❌ 无法直接使用 `Add-Migration` 命令创建新的迁移文件
- ❌ 无法自定义或扩展现有迁移

## 3. 数据库迁移使用方法

### 3.1 自动迁移（推荐）

由于我们已经修改了内部代码，将 `EnsureCreatedAsync` 替换为 `MigrateAsync`，系统会在应用启动时自动应用所有未应用的迁移：

```csharp
// 在 Program.cs 中配置数据库存储
builder.Services.AddQuartzUI();
builder.Services.AddQuartzUIDatabaseFromConfiguration(builder.Configuration);
// 或者使用特定数据库的配置方法
// builder.Services.AddQuartzUISqlServer(connectionString);
```

### 3.2 手动迁移（通过命令行）

您仍然可以使用 EF Core 命令行工具来应用迁移：

1. 安装 EF Core 工具：

```bash
dotnet tool install --global dotnet-ef
```

2. 应用现有迁移：

```bash
dotnet ef database update --project YourProjectName
```

## 4. 配置示例

### 4.1 基本配置

```csharp
// Program.cs
using Chet.QuartzNet.UI.Extensions;
using Chet.QuartzNet.EFCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加 Quartz UI 服务
builder.Services.AddQuartzUI();

// 配置数据库存储（从配置文件）
builder.Services.AddQuartzUIDatabaseFromConfiguration(builder.Configuration);

// 配置 Basic 认证（可选）
builder.Services.AddQuartzUIBasicAuthentication(builder.Configuration);

var app = builder.Build();

// 配置中间件
app.UseAuthorization();
app.UseQuartzUIBasicAuthorized();
app.UseQuartz();
app.MapControllers();

app.Run();
```

### 4.2 配置文件示例

```json
// appsettings.json
{
  "ConnectionStrings": {
    "QuartzUI": "Data Source=QuartzUI.db"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "SQLite"
  }
}
```

## 5. 数据库迁移说明

### 5.1 迁移文件的位置

迁移文件（Migrations）已经包含在 Chet.QuartzNet.UI 包的 EFCore 组件中，您无需单独管理这些文件。

### 5.2 应用迁移的方式

#### 5.2.1 自动迁移

系统会在应用启动时自动应用所有未应用的迁移，这是推荐的方式。

#### 5.2.2 手动命令

您可以使用 EF Core 命令行工具来应用迁移：

```bash
# 使用 dotnet ef 命令
dotnet ef database update --project YourProjectName

# 或者使用 Package Manager Console
Update-Database
```

### 5.3 迁移的工作原理

1. 迁移文件包含在 `Chet.QuartzNet.EFCore.dll` 中（该 DLL 已集成到 UI 包）
2. 当调用 `Update-Database` 或 `MigrateAsync` 时，EF Core 会自动找到并应用这些迁移
3. 迁移的版本信息会存储在数据库的 `__EFMigrationsHistory` 表中

## 6. 注意事项

### 6.1 数据库提供程序依赖

您仍然需要安装相应的数据库提供程序包：

```bash
# SQL Server
Install-Package Microsoft.EntityFrameworkCore.SqlServer

# MySQL
Install-Package Pomelo.EntityFrameworkCore.MySql

# PostgreSQL
Install-Package Npgsql.EntityFrameworkCore.PostgreSQL

# SQLite
Install-Package Microsoft.EntityFrameworkCore.Sqlite
```

### 6.2 连接字符串配置

确保在 `appsettings.json` 中正确配置了连接字符串：

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Server=(localdb)\\mssqllocaldb;Database=QuartzUI;Trusted_Connection=True;"
  }
}
```

### 6.3 数据库权限

确保数据库用户具有以下权限：
- 创建表和索引
- 修改表结构
- 插入、更新、删除数据

## 7. 常见问题解答

### Q: 为什么我不能使用 Add-Migration 命令？

A: 因为 EFCore 包没有单独发布，您无法直接创建新的迁移。如果需要自定义迁移，您需要从源代码构建项目。

### Q: 自动迁移是否会影响性能？

A: 自动迁移只会在应用启动时检查一次数据库版本，对性能的影响可以忽略不计。

### Q: 我可以使用哪个版本的数据库提供程序？

A: 您可以使用任何与 .NET 8.0 兼容的数据库提供程序版本，但建议使用与 UI 包依赖项版本一致的提供程序。

## 8. 总结

虽然 Chet.QuartzNet.EFCore 包没有单独发布，但由于它已经完全集成到 Chet.QuartzNet.UI 包中，您仍然可以使用所有数据库存储和迁移功能。自动迁移功能会在应用启动时自动应用所有未应用的迁移，这是推荐的使用方式。