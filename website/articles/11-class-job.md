---
title: "ClassJob 介绍"
description: "用 [QuartzJob] 特性把 C# 类变成可管理的定时任务：扫描规则、特性参数、依赖注入与边界。"
---

# ClassJob 介绍

ClassJob 是用代码定义的作业：一个实现 `IJob` 的类，加上 `[QuartzJob]` 特性，应用启动时自动注册到调度器，并出现在管理界面中。

## 1. 它是怎么被注册的

调用 `AddQuartzClassJobs()` 后，应用启动时会执行 `ClassJobInitializer`：

1. 遍历当前应用域中已加载的程序集；
2. 找出**公开、非抽象、实现 `IJob`** 且标记了 `[QuartzJob]` 的类；
3. 检查存储中是否已存在同名 + 同分组的作业；
4. 不存在则写入存储并注册，存在则跳过。

因此有两个重要行为：

- **存储优先**：在界面上改过 Cron 或状态后，下次启动不会用特性里的值覆盖；
- **不支持热重载**：改了代码需要重新编译并重启应用。

## 2. 最小示例

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

        try
        {
            await Task.Delay(1000);
            _logger.LogInformation("SampleJob 执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SampleJob 执行失败");
            throw;   // 抛出后才会被标记为失败，并触发重试/通知
        }
    }
}
```

## 3. 特性参数

`QuartzJobAttribute` 的定义很克制，只有五个参数：

| 参数 | 类型 | 说明 |
| ---- | ---- | ---- |
| `Name` | string | 作业名称，与分组共同唯一 |
| `Group` | string | 作业分组 |
| `CronExpression` | string | Quartz Cron 表达式 |
| `Description` | string | 作业描述 |
| `Enabled` | bool | 首次注册时是否启用，默认 `true` |

构造函数签名为：

```csharp
QuartzJobAttribute(
    string name,
    string group,
    string cronExpression,
    string description = "",
    bool enabled = true
)
```

注意：特性里**没有**优先级、通知开关、重试次数这些参数。重试、并发、依赖、中断补跑都在创建后通过界面配置。

## 4. 注册

```csharp
using Chet.QuartzNet.UI.Extensions;

builder.Services.AddQuartzUI(builder.Configuration);
builder.Services.AddQuartzClassJobs();
```

`AddQuartzClassJobs()` 注册的是一个 HostedService，只负责“启动时同步作业定义”，不负责运行作业。作业的创建与执行仍由 `AddQuartzUI()` 中的调度器负责。

## 5. 依赖注入

作业由 DI 容器创建，可以直接注入服务：

```csharp
[QuartzJob("OrderSyncJob", "ORDER", "0 0/10 * * * ?", Description = "订单同步")]
public class OrderSyncJob : IJob
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderSyncJob> _logger;

    public OrderSyncJob(IOrderService orderService, ILogger<OrderSyncJob> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var count = await _orderService.SyncAsync();
        _logger.LogInformation("同步完成，共 {Count} 条", count);
    }
}
```

约定：

- 使用构造函数注入，不要在作业里 `new` 服务；
- 不要在字段中保存跨执行的业务状态；
- 使用 `ILogger` 记录业务上下文，框架已经记录开始、结束、耗时与异常。

## 6. 读取作业数据

在界面编辑 DLL 作业时填写的 JSON，会放进 `JobDataMap`。ClassJob 中通过扩展方法读取：

```csharp
using Chet.QuartzNet.Core.Extensions;

public async Task Execute(IJobExecutionContext context)
{
    // 原始 JSON
    var json = context.JobDetail.JobDataMap.GetJobDataJson();

    // 反序列化为强类型
    var data = context.JobDetail.JobDataMap.GetJobData<OrderSyncParam>();
}
```

示例参数类型：

```csharp
public class OrderSyncParam
{
    public string? Source { get; set; }
    public int BatchSize { get; set; } = 100;
}
```

## 7. 在界面中管理

ClassJob 注册后会出现在作业列表中，与 API 作业一样可以：

- 编辑 Cron、描述、重试、并发、依赖、中断补跑；
- 暂停 / 恢复、立即执行；
- 查看执行日志；
- 启用 / 禁用。

需要注意：

- 作业名称与分组不可在界面修改；
- 界面修改的是存储中的定义，不会改代码里的特性；
- 删除 ClassJob 后，下次启动如果特性还在，会被重新注册。

## 8. 与 API 作业的区别

| 维度 | ClassJob（DLL） | API 作业 |
| ---- | --------------- | -------- |
| 逻辑位置 | 应用内的 C# 类 | 外部 HTTP 接口 |
| 创建方式 | 特性 + 自动扫描 | 界面填写 |
| 修改逻辑 | 改代码并重新发布 | 改接口或参数 |
| 依赖注入 | 支持 | 不涉及 |
| 适合场景 | 业务内聚、需要事务/本地服务 | 跨服务触发、轻量调用 |

## 9. 最佳实践

1. **作业无状态**：每次执行都从数据库或服务读取最新数据；
2. **幂等**：失败重试、手动执行、中断补跑都可能造成重复执行；
3. **控制时长**：长时间任务考虑拆分，或开启「禁止并发执行」避免堆积；
4. **异常要抛出**：吞掉异常会导致日志显示成功，重试与通知也不会触发；
5. **日志有边界**：框架已记录执行元信息，作业内只补充业务关键信息；
6. **低频作业注意沉默**：启用后长期不执行会被分析页标为「沉默作业」，确认 Cron 是否正确。

## 10. 常见问题

| 问题 | 处理 |
| ---- | ---- |
| 启动后列表里没有作业 | 确认类为 `public`、实现 `IJob`、标记了 `[QuartzJob]`，并调用了 `AddQuartzClassJobs()` |
| 改了特性里的 Cron 但没生效 | 存储中已存在该作业，界面配置优先；在界面修改或删除后重启 |
| 注入的服务为 null | 确认服务已在 DI 中注册，且作业通过构造函数注入 |
| 作业执行显示成功但业务失败 | 作业内部吞掉了异常，改为记录后 `throw` |
| 删除后重启又出现 | 代码中的特性仍存在，属于预期行为 |

## 相关阅读

- [ClassJob 代码作业](/guide/class-job)
- [新增作业指南](/articles/07-create-job)
- [作业管理介绍](/articles/08-job-management)
