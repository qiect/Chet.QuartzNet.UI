# Chet.QuartzNet.UI 数据迁移生成指南

## 1. 项目架构

本项目采用模块化设计，为不同数据库提供独立的支持包，实现了迁移文件的分离管理。主要包含以下数据库支持项目：

| 数据库类型 | 项目名称 | 包名称 |
|---------|-------|------|
| PostgreSQL | Chet.QuartzNet.EFCore.PostgreSql | Chet.QuartzNet.EFCore.PostgreSql |
| MySQL | Chet.QuartzNet.EFCore.MySql | Chet.QuartzNet.EFCore.MySql |
| SQLite | Chet.QuartzNet.EFCore.SQLite | Chet.QuartzNet.EFCore.SQLite |
| SQL Server | Chet.QuartzNet.EFCore.SqlServer | Chet.QuartzNet.EFCore.SqlServer |

## 2. 扩展方法使用

### 2.1 统一扩展方法

项目采用了**统一的 API 设计**，所有数据库类型均可通过同一个 `AddQuartzUI` 方法进行配置，系统会根据配置自动选择对应的数据库存储方式。

#### 配置文件示例
```json
{
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "postgres" // 可选，自动根据连接字符串判断
  },
  "ConnectionStrings": {
    "QuartzUI": "数据库连接字符串"
  }
}
```

### 2.2 基本使用示例

```csharp
// 添加 Quartz UI 服务（自动根据配置选择存储方式）
builder.Services.AddQuartzUI(builder.Configuration);

// 可选：ClassJob 自动扫描注册
builder.Services.AddQuartzClassJobs();

// 启用中间件
app.UseQuartz();
```

### 2.3 数据库连接字符串示例

#### PostgreSQL
```json
"ConnectionStrings": {
  "QuartzUI": "Host=localhost;Port=5432;Database=quartzui;Username=postgres;Password=password;"
}
```

#### MySQL
```json
"ConnectionStrings": {
  "QuartzUI": "Server=localhost;Database=quartzui;User=root;Password=password;"
}
```

#### SQLite
```json
"ConnectionStrings": {
  "QuartzUI": "Data Source=quartzui.db;"
}
```

#### SQL Server
```json
"ConnectionStrings": {
  "QuartzUI": "Server=(localdb)\\mssqllocaldb;Database=quartzui;Trusted_Connection=True;"
}
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
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSql.csproj --startup-project src/Chet.QuartzNet.EFCore.PostgreSql/Chet.QuartzNet.EFCore.PostgreSql.csproj
```

#### 3.3.2 MySQL

```bash
dotnet ef migrations add InitialCreate --project src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySql.csproj --startup-project src/Chet.QuartzNet.EFCore.MySql/Chet.QuartzNet.EFCore.MySql.csproj
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
3. **添加服务**：在 `Program.cs` 中调用统一的 `AddQuartzUI` 方法
4. **运行应用**：应用启动时会自动创建数据库和表结构

### 6.1 完整示例（ASP.NET Core）

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加控制器支持
builder.Services.AddControllers();

// 添加 Quartz UI 服务（自动根据配置选择数据库存储）
builder.Services.AddQuartzUI(builder.Configuration);

// 可选：ClassJob 自动扫描注册
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

// 启用 Quartz UI 中间件
app.UseQuartz();

app.MapControllers();

app.Run();
```

## 7. 迁移生成状态

| 数据库类型 | 迁移生成状态 |
|---------|----------|
| PostgreSQL | ✅ 已完成 |
| MySQL | ✅ 已完成 |
| SQLite | ✅ 已完成 |
| SQL Server | ✅ 已完成 |

**说明**：所有数据库类型的初始迁移已生成，包含作业、触发器、通知等核心表结构。

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
dotnet ef migrations add <NewMigrationName> --project src/Chet.QuartzNet.EFCore.<DatabaseType>/Chet.QuartzNet.EFCore.<DatabaseType>.csproj --startup-project src/Chet.QuartzNet.EFCore.<DatabaseType>/Chet.QuartzNet.EFCore.<DatabaseType>.csproj
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

## 12. 统一API设计常见问题

### 12.1 如何确认系统使用了正确的数据库提供者？

**解决方案**：
- 检查应用启动日志，系统会输出使用的存储类型和数据库提供者
- 确保连接字符串格式正确，系统会自动根据连接字符串判断数据库类型
- 可在配置文件中显式指定 `DatabaseProvider` 选项

### 12.2 可以在运行时切换数据库类型吗？

**解决方案**：
- 不支持运行时切换数据库类型，需要重新启动应用
- 如需切换数据库，修改配置文件后重新启动应用即可
