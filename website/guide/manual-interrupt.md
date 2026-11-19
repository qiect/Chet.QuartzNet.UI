---
description: 手动中断运行中的作业：日志页一键发出取消信号，以及 DLL 作业如何通过 context.CancellationToken 配合退出。
---

# 手动中断运行中的作业

v2.8.0 新增。日志页的运行中记录多了一个「中断」按钮，用于提前结束长时间运行的作业，不必等它自然跑完，也不用去停调度器。

> 先明确一条边界：Quartz 的 `Interrupt` 是**协作式取消**，它只是把 `context.CancellationToken` 置为已取消，**不会强杀线程**。作业能不能停下来，取决于作业代码有没有观察这个令牌。API 作业平台已经处理好了，DLL 作业需要按本文第 3 节改造。

## 1. 使用方式

1. 打开「日志管理」，找到状态为**运行中**的记录；
2. 点操作列的**中断**图标（圆形禁止符，红色；仅运行中行显示）；
3. 二次确认后提交，列表立即刷新；
4. 状态列仍显示「中断」，鼠标悬浮可看到「手动中断」；详情页「恢复信息」中会多出「中断方式」一行。

中断中的图标会置灰，防止重复提交。手动中断与进程崩溃产生的记录**共用「中断」状态**，靠跳过原因前缀 `【手动中断】` 在展示层区分，不新增状态值。

## 2. 中断后系统做了什么

```text
点击中断
  └─ 校验：日志是运行中 → 执行实例属于当前节点 → 调度器可用
       └─ 定位本次执行：按 JobKey 匹配，多个并发实例取触发时间最接近的一条
            └─ 先把日志标记为「中断」（补偿状态置为「已跳过」）
                 └─ 再发 Interrupt 信号（优先精确到 fireInstanceId，失败回退 JobKey）
                      └─ 作业观察到取消 → 抛 OperationCanceledException → 收尾
```

几个刻意的设计：

| 行为 | 原因 |
| --- | --- |
| 先标记、后发信号 | 中断会让作业立刻抛异常，若监听器抢先收尾会把日志写成「失败」；先标记可让监听器识别并**保留中断状态** |
| 补偿状态置为「已跳过」 | 手动中断不应进入启动期自动补偿，否则下次启动会把它当异常中断重跑 |
| 定位到具体执行实例 | 同一作业允许并发时，只中断目标实例，不影响其它在跑的实例 |
| 校验实例归属 | 当前为内存调度（单进程），中断只对本节点生效；进程重启后的残留记录会直接拒绝 |

中断成功后，该记录可以像普通中断一样点「重新执行」手动重跑（此时会变成「已补偿重跑」）。

### 接口

```http
POST /api/quartz/InterruptJob
Content-Type: application/json

{ "logId": "3f2b1c6e-..." }
```

返回沿用统一响应结构 `{ success, message, data, errorCode }`，常见错误码：

| 错误码 | 含义 |
| --- | --- |
| `LOG_NOT_FOUND` | 日志不存在 |
| `LOG_NOT_RUNNING` | 不是运行中的记录 |
| `LOG_NOT_ON_THIS_NODE` | 执行不在当前节点，或已随进程重启结束 |
| `SCHEDULER_NOT_RUNNING` | 调度器已停止 |
| `EXECUTION_NOT_FOUND` | 没找到正在执行的实例（可能刚结束） |
| `EXECUTION_FINISHED` | 标记时执行已结束 |

## 3. DLL 作业如何配合（重点）

### 3.1 为什么必须由作业配合

.NET 运行时不支持安全地终止线程（`Thread.Abort` 在 .NET Core 起直接抛异常），所以 Quartz 只能"请求"作业停下。作业与令牌的关系如下：

| 作业类型 | 点击中断后的实际效果 |
| --- | --- |
| API 作业（界面配置，`JobType=API`） | 平台内置 `ApiJob` 已把令牌链进 HTTP 请求，请求立即取消，**无需写代码** |
| 重试包装器正在等待重试 | 等待中的 `Task.Delay` 已接令牌，立即结束，**无需写代码** |
| 自定义 DLL 作业，代码里接了令牌 | 在下一个等待点/检查点退出，**推荐做法** |
| 自定义 DLL 作业，代码里没接令牌 | 令牌只是被置位，作业**继续跑完**，最终日志会被真实结果覆盖 |

### 3.2 最小改造：三个动作

```csharp
public async Task Execute(IJobExecutionContext context)
{
    // ① 拿到 Quartz 注入的令牌（不要自己 new）
    var token = context.CancellationToken;

    // ② 所有等待/IO 调用都把令牌传进去
    await Task.Delay(TimeSpan.FromSeconds(30), token);
    using var response = await _httpClient.GetAsync("https://example.com/api", token);
    await _dbContext.SaveChangesAsync(token);

    // ③ 长循环里自己设检查点
    foreach (var item in items)
    {
        token.ThrowIfCancellationRequested();
        await ProcessAsync(item, token);
    }
}
```

### 3.3 常见等待点的正确写法

| 原写法（不可中断） | 改法 |
| --- | --- |
| `Thread.Sleep(60_000)` | `await Task.Delay(TimeSpan.FromMinutes(1), token)` |
| `await Task.Delay(60_000)` | `await Task.Delay(60_000, token)` |
| `httpClient.SendAsync(request)` | `httpClient.SendAsync(request, token)` |
| `dbContext.SaveChangesAsync()` | `dbContext.SaveChangesAsync(token)` |
| `query.ToListAsync()` | `query.ToListAsync(token)` |
| `stream.ReadAsync(buffer, 0, len)` | `stream.ReadAsync(buffer, token)` |
| `semaphore.WaitAsync()` | `semaphore.WaitAsync(token)` |
| 同步计算 / 大批量循环 | 每轮 `token.ThrowIfCancellationRequested()` |

拿不到异步 API 的老代码（比如只能同步调用的第三方 SDK），退而求其次：

```csharp
token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));  // 可被中断唤醒
token.ThrowIfCancellationRequested();
```

### 3.4 三个必须避免的坑

**① 不要吞掉取消异常**

```csharp
// 错误：作业"正常返回"，监听器会把它记成成功，中断状态被覆盖
try { await Task.Delay(60_000, token); }
catch (OperationCanceledException) { }

// 正确：让它抛出去（或在 catch 里重新 throw）
```

**② 不要把取消当成业务失败**

```csharp
catch (OperationCanceledException) when (token.IsCancellationRequested)
{
    // 用户手动中断 / 调度器停机，正常退出，不要写"失败"结果，也不要触发重试
    throw;
}
catch (Exception ex)
{
    // 真正的业务异常
}
```

**③ 不要在 catch / finally 里继续长时间工作**

取消信号到达时，作业可能在任意 `await` 处抛出。收尾逻辑（写库、补发消息）如果耗时很长，会让"中断"看起来没生效，还可能继续产生副作用。

### 3.5 完整示例

```csharp
public class LongRunningJob : IJob
{
    private readonly ILogger<LongRunningJob> _logger;
    private readonly HttpClient _httpClient;
    private readonly MyDbContext _dbContext;

    public LongRunningJob(
        ILogger<LongRunningJob> logger,
        HttpClient httpClient,
        MyDbContext dbContext)
    {
        _logger = logger;
        _httpClient = httpClient;
        _dbContext = dbContext;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var token = context.CancellationToken;

        var items = await _dbContext.Items
            .Where(x => x.Pending)
            .ToListAsync(token);

        _logger.LogInformation("待处理 {Count} 条，作业 {JobKey}",
            items.Count, context.JobDetail.Key);

        foreach (var item in items)
        {
            // 每处理一条检查一次，中断最多延迟一条的处理时间
            token.ThrowIfCancellationRequested();

            await _httpClient.PostAsJsonAsync(
                "https://example.com/api/process", item, token);

            item.Pending = false;
            await _dbContext.SaveChangesAsync(token);
        }

        _logger.LogInformation("作业完成 {JobKey}", context.JobDetail.Key);
    }
}
```

这个示例里，点击中断后最坏情况是"当前这一条处理完"就退出，通常在几百毫秒内。

### 3.6 作业不听令牌会怎样

- **线程继续占用**：Quartz 的线程池线程会被一直占着，长任务多时可能拖垮并发能力；
- **副作用继续产生**：写入、调用都会照常发生；
- **日志最终会变成真实结果**：如果作业最后正常返回，监听器会把「中断」覆盖为「成功」。这不是 bug，是"日志如实反映作业有没有停下来"。

真正需要"无论如何都必须停"的场景（例如第三方库内部死循环），只能走**进程隔离**：把这类作业放到独立进程/容器里执行，中断时直接结束进程。Quartz 层面没有这种能力。

## 4. 与自动补偿的关系

| 场景 | 补偿状态 | 下次启动是否自动重跑 |
| --- | --- | --- |
| 进程崩溃 / 强杀 / 停机超时 | 待补偿 | 按策略自动补偿 |
| 手动中断 | 已跳过 | **不会**，需要手动点「重新执行」 |

这样设计是为了避免"我手动停掉的作业，第二天自己又跑了一遍"。

## 5. 相关文档

- 中断判定与自动补偿规则：[中断恢复与排水调度](/guide/recovery)
- DLL 作业的编写与注册：[ClassJob 代码作业](/guide/class-job)
- 日志页的筛选、详情与清理：[日志管理](/modules/log)
