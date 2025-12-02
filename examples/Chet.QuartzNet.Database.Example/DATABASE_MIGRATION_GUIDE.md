# 使用NuGet包的数据库存储方式与迁移指南

## 注意事项

**重要提示**：`Chet.QuartzNet.EFCore` 包已经集成到 `Chet.QuartzNet.UI` 包中，您无需单独安装 EFCore 包即可使用数据库存储和迁移功能。详细说明请参考 [集成式 EFCore 使用指南](INTEGRATED_EFCORE_USAGE.md)。

## 1. 安装必要的NuGet包

首先，您需要安装以下三个核心包：

```powershell
# 主包
Install-Package Chet.QuartzNet.UI

# 数据库存储包
Install-Package Chet.QuartzNet.EFCore

# 选择对应的数据库提供程序包（以下示例使用SQL Server）
Install-Package Microsoft.EntityFrameworkCore.SqlServer
```

根据您的数据库类型，选择相应的提供程序包：
- SQL Server: `Microsoft.EntityFrameworkCore.SqlServer`
- MySQL: `MySql.EntityFrameworkCore`
- PostgreSQL: `Npgsql.EntityFrameworkCore.PostgreSQL`
- SQLite: `Microsoft.EntityFrameworkCore.Sqlite`

## 2. 配置数据库存储

在 `Program.cs` 中进行配置：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加QuartzUI服务
builder.Services.AddQuartzUI();

// 配置SQL Server数据库存储
builder.Services.AddQuartzUISqlServer(builder.Configuration.GetConnectionString("QuartzDb"));

// 或者使用其他数据库提供程序：
// builder.Services.AddQuartzUIMySql("connectionString");
// builder.Services.AddQuartzUIPostgreSQL("connectionString");
// builder.Services.AddQuartzUISqlite("connectionString");

var app = builder.Build();

// 配置中间件
app.UseRouting();

// 添加QuartzUI中间件
app.UseQuartzUI();

app.MapControllers();
app.Run();
```

## 3. 执行数据库迁移

### 方法一：使用EF Core迁移命令

1. 安装EF Core工具：
```powershell
Install-Package Microsoft.EntityFrameworkCore.Tools
```

2. 在Package Manager Console中执行迁移命令：
```powershell
# 添加迁移（如果需要）
Add-Migration InitialCreate -Context QuartzDbContext -Project YourProjectName

# 更新数据库
Update-Database -Context QuartzDbContext -Project YourProjectName
```

### 方法二：自动迁移（代码中配置）

从 `Chet.QuartzNet.EFCore` 版本 0.0.3+ 开始，系统默认会在应用启动时自动执行数据库迁移，无需手动添加代码。这是因为我们已经将 `EFCoreJobStorage` 中的 `EnsureCreatedAsync` 方法替换为 `MigrateAsync` 方法，它会自动应用所有未应用的迁移。

如果需要手动控制迁移过程，可以在代码中配置自动迁移：

```csharp
var app = builder.Build();

// 执行数据库迁移
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuartzDbContext>();
    dbContext.Database.Migrate();
}

// 其他中间件配置...
```

## 4. 配置文件设置

在 `appsettings.json` 中添加数据库连接字符串：

```json
{
  "ConnectionStrings": {
    "QuartzDb": "Server=(localdb)\\mssqllocaldb;Database=QuartzDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## 5. 完整代码示例

```csharp
using Chet.QuartzNet.EFCore.Extensions;
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 添加控制器
builder.Services.AddControllersWithViews();

// 配置QuartzUI
builder.Services.AddQuartzUI(options =>
{
    options.StorageType = Core.Configuration.StorageType.Database;
});

// 配置数据库存储
builder.Services.AddQuartzUISqlServer(builder.Configuration.GetConnectionString("QuartzDb"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// 添加QuartzUI中间件
app.UseQuartzUI();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 执行数据库迁移
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QuartzDbContext>();
    dbContext.Database.Migrate();
}

app.Run();
```

## 6. 注意事项

1. **迁移文件**：Chet.QuartzNet.EFCore包已包含必要的迁移文件，通常不需要手动创建新迁移
2. **数据库选择**：确保选择与您的生产环境匹配的数据库提供程序
3. **连接字符串**：建议将连接字符串存储在环境变量或安全的配置源中
4. **权限**：确保数据库用户具有创建表和索引的权限

完成以上步骤后，您的Quartz.Net UI将使用数据库存储方式运行，所有作业信息和执行日志将持久化到数据库中。
