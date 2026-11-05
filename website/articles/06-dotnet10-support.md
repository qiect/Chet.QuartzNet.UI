---
title: "Chet.QuartzNet.UI 现已支持 .NET 10 框架"
description: "多框架目标升级过程、依赖调整与 .NET 10 下的使用方式。"
---

# 🔥 Chet.QuartzNet.UI 现已支持 .NET 10 框架！

## 🎯 为什么这是个好消息？

作为一名 .NET 开发者，你是不是已经迫不及待想体验 .NET 10 的强大功能了？现在，好消息来了！**Chet.QuartzNet.UI** 正式支持 .NET 10 框架啦！😍

## ✨ .NET 10 支持带来了哪些惊喜？

### 🚀 利用 .NET 10 最新特性
- **性能提升**：享受 .NET 10 带来的性能优化，作业调度更高效
- **新 API 支持**：使用 .NET 10 提供的最新 API，开发体验更佳
- **AOT 编译**：支持 AOT 编译，启动速度更快，内存占用更低
- **GC 优化**：受益于 .NET 10 的 GC 改进，系统稳定性更强

### 💎 无缝迁移，零成本升级
- 保持与旧版本相同的 API 接口，无需修改现有代码
- 相同的配置方式，平滑过渡到 .NET 10
- 向下兼容 .NET 8/9，保护你的投资


### 📊 更好的开发体验
- 支持 .NET 10 的新工具链
- 与 Visual Studio 2026 完美兼容
- 更好的调试和诊断支持

## 🚀 如何在 .NET 10 项目中使用？

### 1️⃣ 创建 .NET 10 项目

```bash
dotnet new web -n MyQuartzProject -f net10.0
cd MyQuartzProject
```

### 2️⃣ 安装最新版本的 Chet.QuartzNet.UI

```bash
dotnet add package Chet.QuartzNet.UI --version 最新版本号
```

### 3️⃣ 配置服务

在 `Program.cs` 中添加配置：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加 Quartz UI 服务
builder.Services.AddQuartzUI(builder.Configuration);

// 可选：自动扫描并注册 ClassJob
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

// 启用中间件
app.UseQuartz();

app.Run();
```

### 4️⃣ 配置 JWT 认证

在 `appsettings.json` 中添加配置：

```json
{
  "QuartzUI": {
    "JwtSecret": "Y2V0aFF1YXJ6TmV0VUlBdXRoZW50aWNhdGlvblNlY3JldA==",
    "JwtExpiresInMinutes": 60,
    "JwtIssuer": "Chet.QuartzNet.UI",
    "JwtAudience": "Chet.QuartzNet.UI",
    "UserName": "Admin",
    "Password": "123456"
  }
}
```

### 5️⃣ 启动应用

```bash
dotnet run
```

访问 `http://localhost:5173/quartz-ui` 即可体验！

## 💡 .NET 10 特有的优化建议

### 🎯 使用 AOT 编译提升性能

在项目文件中添加：

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

然后发布：

```bash
dotnet publish -c Release -o out
```

### 📈 利用 .NET 10 的新特性优化代码

```csharp
// 使用 Primary Constructors
[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "示例作业")]
public class SampleJob(ILogger<SampleJob> logger) : IJob
{
    private readonly ILogger<SampleJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("SampleJob 执行成功！");
        await Task.CompletedTask;
    }
}
```

### 🔧 配置 .NET 10 的性能选项

在 `appsettings.json` 中添加：

```json
{
  "DotNetPerformance": {
    "GCCount": 10,
    "GCServer": true,
    "TieredCompilation": true
  }
}
```

## 🎉 总结

Chet.QuartzNet.UI 支持 .NET 10 框架，意味着你可以在享受 .NET 10 强大功能的同时，继续使用这款优秀的任务调度 UI 工具。无缝迁移、零成本升级，让你轻松拥抱 .NET 10 的新时代！

如果你对 .NET 10 支持有任何建议或反馈，欢迎在 GitHub 上提出 Issue 或提交 PR，我们期待你的参与！😊

---

**官方地址**：[https://qiect.github.io/Chet.QuartzNet.UI/](https://qiect.github.io/Chet.QuartzNet.UI/)
**项目地址**：[https://github.com/qiect/Chet.QuartzNet.UI](https://github.com/qiect/Chet.QuartzNet.UI)
**NuGet包**：[https://www.nuget.org/packages/Chet.QuartzNet.UI](https://www.nuget.org/packages/Chet.QuartzNet.UI)
