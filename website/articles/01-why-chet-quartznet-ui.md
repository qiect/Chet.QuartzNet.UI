---
title: "为什么选择 Chet.QuartzNet.UI：定位、能力与上手"
description: "从一个 NuGet 包、两行注册开始，了解它解决什么问题、能做什么、不做什么。"
---

# 为什么选择 Chet.QuartzNet.UI

## 1. 它解决什么问题

Quartz.Net 本身足够成熟，但把它接进一个业务系统，通常还要自己补齐这些工作：

- 作业和触发器的定义、持久化与启停；
- 执行日志、耗时、异常的记录与查询；
- 失败告警、调度器异常告警；
- 一个非开发人员也能操作的界面。

Chet.QuartzNet.UI 把这部分做成了一个 Razor Class Library：安装一个 NuGet 包，在 `Program.cs` 注册服务与中间件，访问 `/quartz-ui` 即可使用，不需要单独部署前端或调度服务。

![登录界面](/images/login.png)

## 2. 基本信息

| 项目 | 说明 |
| ---- | ---- |
| 技术栈 | ASP.NET Core + Quartz.Net 3.15.1 + VbenAdmin（Vue 3 / Ant Design Vue） |
| 目标框架 | `net8.0`、`net10.0` |
| 集成方式 | NuGet 包（RCL），静态资源嵌入包内 |
| 管理入口 | `/quartz-ui` |
| 认证 | 内置单账号 JWT（`QuartzUI:UserName` / `Password`） |
| 存储 | 文件（JSON）/ MySQL / PostgreSQL / SQL Server / SQLite |
| 调度存储 | Quartz 使用内存 JobStore，作业定义与日志另存于文件或数据库 |

## 3. 核心能力

### 3.1 作业管理

支持两种作业类型：

| 类型 | 实现方式 | 适用场景 |
| ---- | -------- | -------- |
| DLL（ClassJob） | C# 类实现 `IJob`，用 `[QuartzJob]` 声明并自动注册 | 业务逻辑在应用内、需要依赖注入 |
| API | 配置 HTTP 地址、方法、请求头、请求体与超时 | 触发其他服务的接口，无需改代码 |

作业层面可配置 Cron、失败重试、禁止并发执行、中断后自动重跑、前置依赖，并支持批量暂停 / 恢复 / 启用 / 禁用。

![作业管理](/images/job.png)

### 3.2 执行日志与运行态

- 执行开始即写入 `Running` 记录，页面可看到运行中任务与实时耗时；
- 结束后更新同一条记录为成功 / 失败，并记录耗时、异常与作业数据；
- 状态包含运行中、成功、失败、已跳过、中断；
- 支持按名称、分组、状态、时间范围筛选，以及「仅看异常」。

![日志管理](/images/log.png)

### 3.3 通知

基于 PushPlus，在作业成功 / 失败或调度器异常时推送。支持微信、企业微信、Webhook、邮件、短信等渠道，消息模板有 HTML、Markdown、TXT、JSON 四种。

### 3.4 存储与迁移

文件存储零依赖，适合单机与快速验证；数据库存储适合生产环境。内置迁移向导，可在文件、MySQL、PostgreSQL、SQLite、SQL Server 之间搬运作业、日志、设置与通知数据。

### 3.5 可靠性（v2.6 / v2.7）

- 作业依赖：前置作业最近一次未成功则本次跳过，并写入「已跳过」日志；
- 日志自动清理：按保留天数或最大条数在后台清理；
- 中断识别：进程崩溃后心跳过期，记录标记为「中断」；
- 中断补偿：下次启动按策略补跑，也可以手动「重新执行」；
- 排水停机：停止接收新触发，等待执行中任务收尾后再关闭调度器。

## 4. 能力边界

它定位是**轻量的定时任务可视化管理工具**，以下能力并不提供，选型前需要确认：

| 不提供 | 说明 |
| ------ | ---- |
| 集群调度 | Quartz 使用 `UseInMemoryStore`，多实例各自调度；数据库只共享作业定义与日志 |
| RBAC / 多用户 / 审计 | 内置只有一个账号，权限体系需在宿主应用或网关实现 |
| DAG 工作流引擎 | 只有「前置依赖校验」，没有可视化编排与自动触发链路 |
| 断点续传 | 中断补偿是整段重跑，语义为 at-least-once |
| 按作业配置通知渠道 | 通知是系统级配置，策略为成功 / 失败 / 调度器异常 |

## 5. 五分钟跑起来

### 5.1 安装

```bash
dotnet add package Chet.QuartzNet.UI
```

### 5.2 注册服务

```csharp
using Chet.QuartzNet.UI.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddQuartzUI(builder.Configuration);

// 可选：扫描并注册 [QuartzJob] 作业类
builder.Services.AddQuartzClassJobs();

var app = builder.Build();

app.MapControllers();
app.UseQuartz();   // 挂载 /quartz-ui 路由与静态资源
app.Run();
```

### 5.3 添加配置

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

文件存储无需额外配置，数据默认写入 `App_Data/QuartzJobs/`。

### 5.4 启动并访问

```bash
dotnet run
```

访问 `http://localhost:{端口}/quartz-ui`，用配置中的账号密码登录。仓库中的两个示例项目可直接体验：

| 示例 | 端口 | 存储 |
| ---- | ---- | ---- |
| `examples/Chet.QuartzNet.File.Example` | 5001 | 文件 |
| `examples/Chet.QuartzNet.Database.Example` | 5002 | SQLite |

### 5.5 第一个 ClassJob

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "每 5 分钟执行一次")]
public class SampleJob : IJob
{
    private readonly ILogger<SampleJob> _logger;

    public SampleJob(ILogger<SampleJob> logger) => _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("SampleJob 开始执行：{Time}", DateTime.Now);
        await Task.Delay(1000);
        _logger.LogInformation("SampleJob 执行完成");
    }
}
```

启动后会自动出现在作业列表中，无需在界面创建。

## 6. 适合与不适合

**适合**

- 单体或少量实例的 .NET 应用，需要快速补上任务调度与监控；
- 已有 Quartz 代码，希望有一个统一的管理与日志界面；
- 需要失败告警、依赖控制、停机收尾，但不想引入重型调度平台。

**不适合**

- 需要多节点集群调度、分片、故障转移；
- 需要审批流、多租户、细粒度权限；
- 需要复杂 DAG 编排与跨系统工作流。

## 相关阅读

- [快速开始](/guide/quick-start)
- [配置说明](/guide/configuration)
- [存储模式](/guide/storage)
- [核心架构](/guide/architecture)

---

- GitHub：https://github.com/qiect/Chet.QuartzNet.UI
- NuGet：https://www.nuget.org/packages/Chet.QuartzNet.UI
