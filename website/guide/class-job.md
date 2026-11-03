---
description: 使用 [QuartzJob] 特性声明 C# 作业类，启动时自动扫描注册，支持依赖注入与 UI 统一管理。
---

# ClassJob 代码作业

## 1. 什么是 ClassJob

ClassJob 是用代码定义的作业：一个继承 `IJob` 的类，通过 `[QuartzJob]` 特性声明名称、分组与 Cron 表达式。应用启动时，框架扫描程序集中所有标记该特性的类并自动注册到 Quartz 调度器。

它与界面创建的 API 作业共享同一套管理能力：

- 在作业列表中统一展示；
- 可在界面暂停、恢复、立即执行、修改 Cron；
- 执行结果写入同一份日志；
- 可配置重试、依赖与通知。

## 2. 创建 ClassJob

### 2.1 基本示例

```csharp
using Chet.QuartzNet.Core.Attributes;
using Quartz;

[QuartzJob("SampleJob", "DEFAULT", "0 0/5 * * * ?", Description = "示例作业，每 5 分钟执行一次")]
public class SampleJob : IJob
{
    private readonly ILogger<SampleJob> _logger;

    public SampleJob(ILogger<SampleJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("SampleJob 开始执行，时间: {ExecuteTime}", DateTime.Now);

        try
        {
            await Task.Delay(1000);
            _logger.LogInformation("SampleJob 执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SampleJob 执行失败");
            throw;
        }
    }
}
```

### 2.2 特性参数

| 参数 | 说明 |
| --- | --- |
| `Name` | 作业名称，与分组共同唯一标识一个作业 |
| `Group` | 作业分组，默认 `DEFAULT` |
| `CronExpression` | Quartz Cron 表达式 |
| `Description` | 作业描述 |
| `Enabled` | 首次注册时是否启用，默认 `true` |

## 3. 注册扫描

```csharp
builder.Services.AddQuartzUI(builder.Configuration);

// 扫描当前应用及引用程序集中的 [QuartzJob] 类
builder.Services.AddQuartzClassJobs();
```

- 只需注册一次，无需逐一手动添加；
- 已存在的同名作业会复用存储中的配置，不会重复创建；
- 若在界面中修改过 Cron 或状态，以存储中的配置为准。

## 4. 依赖注入

ClassJob 由 DI 容器创建，因此可以直接注入任意已注册的服务：

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

建议将作业类注册为 Scoped 或 Transient，避免长时间持有请求级资源。

## 5. 读取作业数据

界面创建作业时可以填写 JSON 格式的作业数据。ClassJob 中可通过扩展方法读取：

```csharp
using Chet.QuartzNet.Core.Extensions;

public async Task Execute(IJobExecutionContext context)
{
    // 原始 JSON 字符串
    var json = context.JobDetail.JobDataMap.GetJobDataJson();

    // 反序列化为强类型对象
    var data = context.JobDetail.JobDataMap.GetJobData<SampleParam>();
}
```

相比直接操作 `JobDataMap.WrappedMap`，扩展方法更简洁，也兼容序列化配置。

## 6. 最佳实践

- **作业保持无状态**：不要在作业实例中保存跨次执行的业务状态；
- **幂等设计**：失败重试与手动补跑都可能造成重复执行；
- **控制执行时长**：长任务应拆分或改为异步队列，避免阻塞调度线程；
- **日志简洁**：框架已记录开始、结束、耗时与异常，作业内只需补充业务上下文；
- **异常要抛出**：抛出异常才会被标记为失败并触发重试与通知；
- **依赖注入按需**：只注入作业真正需要的服务，避免扩大生命周期。

## 7. 与界面创建的作业有什么区别

| 维度 | ClassJob | 界面创建（API 作业） |
| --- | --- | --- |
| 逻辑位置 | C# 代码 | 外部 HTTP 接口 |
| 创建方式 | 特性 + 自动扫描 | 界面填写 |
| 修改逻辑 | 改代码重新发布 | 改接口或参数 |
| 适合 | 业务内聚、需要 DI | 跨服务、轻量触发 |

两种作业都可以在同一个界面中管理和监控。
