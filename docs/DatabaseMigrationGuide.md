# 数据库迁移生成指南与使用说明

## 1. 项目架构

本项目采用模块化设计，为不同数据库提供独立的支持包，实现了迁移文件的分离管理。主要包含以下数据库支持项目：

| 数据库类型 | 项目名称 | 包名称 |
|---------|-------|------|
| PostgreSQL | Chet.QuartzNet.EFCore.PostgreSql | Chet.QuartzNet.EFCore.PostgreSql |
| MySQL | Chet.QuartzNet.EFCore.MySql | Chet.QuartzNet.EFCore.MySQL |
| SQLite | Chet.QuartzNet.EFCore.SQLite | Chet.QuartzNet.EFCore.SQLite |
| SQL Server | Chet.QuartzNet.EFCore.SqlServer | Chet.QuartzNet.EFCore.SqlServer |

## 2. 扩展方法使用

### 2.1 核心扩展方法

所有数据库支持包都提供了基于 `IConfiguration` 的扩展方法，用于添加数据库存储支持。

#### 配置文件示例
```json
{
  "QuartzUI": {
    "StorageType": "Database"
  },
  "ConnectionStrings": {
    "QuartzUI": "数据库连接字符串"
  }
}
```

### 2.2 PostgreSQL 使用示例

```csharp
// 添加PostgreSQL支持
services.AddQuartzUIPostgreSQL(configuration);

// 或使用连接字符串直接配置
services.AddQuartzUIPostgreSQL(connectionString);
```

### 2.3 MySQL 使用示例

```csharp
// 添加MySQL支持
services.AddQuartzUIMySql(configuration);

// 或指定服务器版本
services.AddQuartzUIMySql(configuration, "8.0.30");

// 或直接使用连接字符串
services.AddQuartzUIMySql(connectionString, "8.0.30");
```

### 2.4 SQLite 使用示例

```csharp
// 添加SQLite支持
services.AddQuartzUISQLite(configuration);

// 或直接使用连接字符串
services.AddQuartzUISQLite(connectionString);
```

### 2.5 SQL Server 使用示例

```csharp
// 添加SQL Server支持
services.AddQuartzUISqlServer(configuration);

// 或直接使用连接字符串
services.AddQuartzUISqlServer(connectionString);
```

## 3. 迁移文件生成指南

### 3.1 前提条件

- 安装 .NET 8.0 SDK
- 安装 EF Core CLI 工具：`dotnet tool install --global dotnet-ef`
- 确保对应数据库服务器已启动（SQLite除外）

### 3.2 生成迁移文件的通用命令

```bash
dotnet ef migrations add <MigrationName> --project <DatabaseProjectPath> --startup-project <DatabaseProjectPath>
```

### 3.3 为各数据库生成迁移

#### 3.3.1 PostgreSQL

```bash
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSQL.csproj --startup-project src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSQL.csproj
```

#### 3.3.2 MySQL

```bash
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySQL.csproj --startup-project src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySQL.csproj
```

**注意**：MySQL 迁移生成需要实际的 MySQL 服务器运行。

#### 3.3.3 SQLite

```bash
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.SQLite/Chet.QuartzNet.EFCore.SQLite.csproj --startup-project src/Chet.QuartzNet.EFCore.SQLite/Chet.QuartzNet.EFCore.SQLite.csproj
```

**注意**：SQLite 是文件数据库，不需要数据库服务器运行。

#### 3.3.4 SQL Server

```bash
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.SqlServer/Chet.QuartzNet.EFCore.SqlServer.csproj --startup-project src/Chet.QuartzNet.EFCore.SqlServer/Chet.QuartzNet.EFCore.SqlServer.csproj
```

### 3.4 更新数据库

在应用程序启动时，EF Core 会自动应用迁移。也可以使用以下命令手动更新数据库：

```bash
dotnet ef database update --project <DatabaseProjectPath> --startup-project <ApplicationProjectPath>
```

## 4. 设计时 DbContext 工厂

每个数据库项目都包含 `DesignTimeDbContextFactory` 类，用于在设计时生成迁移文件。该类位于 `Migrations` 目录下，包含以下内容：

- 硬编码的连接字符串（仅用于生成迁移）
- 数据库特定的配置
- 迁移程序集配置

### 4.1 修改设计时连接字符串

如果需要修改用于生成迁移的连接字符串，可以编辑对应数据库项目的 `DesignTimeDbContextFactory.cs` 文件：

```csharp
// 示例：修改 PostgreSQL 连接字符串
var connectionString = "Host=localhost;Port=5432;Database=quartzui;Username=postgres;Password=password";
```

## 5. 迁移文件管理

- 每个数据库的迁移文件独立存储在对应项目的 `Migrations` 目录下
- 迁移文件包含：
  - 迁移主文件（如 `20251202075223_InitialCreate.cs`）
  - 迁移设计器文件（如 `20251202075223_InitialCreate.Designer.cs`）
  - 模型快照文件（如 `QuartzDbContextModelSnapshot.cs`）
  - 设计时 DbContext 工厂（`DesignTimeDbContextFactory.cs`）

## 6. 使用流程

1. **安装包**：根据需要使用的数据库，安装对应的 NuGet 包
2. **配置**：在 `appsettings.json` 中配置数据库连接字符串和存储类型
3. **添加服务**：在 `Program.cs` 中调用对应的扩展方法添加数据库支持
4. **运行应用**：应用启动时会自动创建数据库和表结构

### 6.1 完整示例（ASP.NET Core）

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加数据库支持（示例：PostgreSQL）
builder.Services.AddQuartzUIPostgreSQL(builder.Configuration);

// 添加 Quartz UI
builder.Services.AddQuartzUI();

var app = builder.Build();

app.UseQuartzUI();

app.Run();
```

## 7. 迁移生成状态

| 数据库类型 | 迁移生成状态 | 备注 |
|---------|----------|-----|
| PostgreSQL | ✅ 成功 | 已生成 InitialCreate 迁移 |
| MySQL | ⚠️ 失败 | 无法连接到MySQL服务器 |
| SQLite | ✅ 成功 | 已生成 InitialCreate 迁移 |
| SQL Server | ✅ 成功 | 已生成 InitialCreate 迁移 |

## 8. 常见问题

### 8.1 迁移生成失败：无法连接到数据库

**解决方案**：
- 确保数据库服务器已启动
- 检查连接字符串是否正确
- 检查数据库用户是否有足够的权限

### 8.2 迁移生成失败：找不到 DbContext

**解决方案**：
- 确保项目引用了正确的 EFCore 包
- 检查 `DesignTimeDbContextFactory` 是否存在且配置正确
- 确保迁移程序集名称与项目名称一致

### 8.3 运行时无法应用迁移

**解决方案**：
- 确保应用程序有足够的数据库权限
- 检查连接字符串是否正确
- 手动运行 `dotnet ef database update` 命令查看具体错误

## 9. 最佳实践

1. **开发环境**：使用 SQLite 进行开发，无需数据库服务器，便于快速迭代
2. **测试环境**：使用与生产环境相同的数据库类型
3. **生产环境**：根据性能和可靠性要求选择合适的数据库
4. **迁移管理**：定期清理不必要的迁移文件，合并小型迁移
5. **备份**：在应用迁移前备份数据库

## 10. 版本升级

当需要升级数据库结构时：

1. 修改实体类或配置
2. 运行迁移生成命令创建新的迁移文件
3. 测试迁移在开发环境是否正常工作
4. 在生产环境应用迁移

```bash
dotnet ef migrations add <NewMigrationName> --project <DatabaseProjectPath> --startup-project <DatabaseProjectPath>
```

## 11. 回滚迁移

如果迁移应用失败，可以回滚到之前的版本：

```bash
dotnet ef database update <PreviousMigrationName> --project <DatabaseProjectPath> --startup-project <ApplicationProjectPath>
```

或者删除最近的迁移：

```bash
dotnet ef migrations remove --project <DatabaseProjectPath> --startup-project <DatabaseProjectPath>
```
