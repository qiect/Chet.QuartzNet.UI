# Chet.QuartzNet.UI 数据迁移使用指南

> 本文介绍 **存储数据迁移** 功能：在文件、MySQL、PostgreSQL、SQLite、SQL Server 五种存储之间迁移作业、日志、设置和通知数据。
>
> 如需了解数据库表结构的 EF Core 迁移文件生成方式，请阅读《Chet.QuartzNet.UI 数据迁移生成指南》。

## 1. 功能概述

### 1.1 什么是数据迁移

数据迁移用于在 **不丢失数据** 的前提下，把 QuartzNet.UI 的业务数据从一种存储搬移到另一种存储。典型用途：

- 单机文件存储升级为数据库存储
- 数据库更换（MySQL → PostgreSQL、SQL Server → SQLite 等）
- 生产数据导出为文件备份，或文件数据导入新环境
- 多环境之间同步作业配置

迁移只负责「搬运数据」，**不会自动修改应用的存储配置**。迁移完成后，需要手动修改 `QuartzUI:StorageType`、`DatabaseProvider` 和连接字符串并重启应用（迁移结果会给出建议配置片段）。

### 1.2 支持的存储与迁移方向

| 存储类型 | 枚举值 | 标识 |
| --- | --- | --- |
| 文件 | `File` | 目录下的 JSON 文件 |
| MySQL | `MySql` | 需要通过 NuGet 包提供 |
| PostgreSQL | `PostgreSql` | 需要通过 NuGet 包提供 |
| SQLite | `SQLite` | 需要通过 NuGet 包提供 |
| SQL Server | `SqlServer` | 需要通过 NuGet 包提供 |

五种存储之间 **任意两两互转**（共 20 个方向），也支持文件 ↔ 数据库互转。

约束：

- 源与目标不能是 **同一存储类型**（例如两个 SQLite 配置档之间迁移会被拒绝）。需要同类型搬运时，可先导出到文件，再从文件导入另一个同类型库。
- 文件源路径必须来自服务端允许列表（见 4.4 节）。
- 数据库连接字符串只能来自服务端配置，前端不会收到连接字符串。

### 1.3 迁移内容

| 实体 | 键（JSON 字段） | 业务键 | 说明 |
| --- | --- | --- | --- |
| 作业数据 | `Jobs` | `JobGroup` + `JobName` | 作业定义、触发器、Cron、执行状态等 |
| 作业日志 | `Logs` | `LogId` | 执行历史记录 |
| 系统设置 | `Settings` | `Key` | 键值型系统配置 |
| 通知消息 | `Notifications` | `NotificationId` | 通知历史记录 |

说明：

- 未指定迁移内容时默认四类全选。
- 服务端按固定顺序执行：**系统设置 → 作业数据 → 通知消息 → 作业日志**，避免作业先于设置写入导致行为不一致。
- 日志、通知属于流水数据，合并时按主键去重；重复主键会被跳过而不是覆盖。

### 1.4 三种写入模式

| 模式 | 枚举值 | 语义 | 是否幂等 |
| --- | --- | --- | --- |
| 合并 | `Merge` | 不存在则插入，存在则跳过 | 是 |
| 覆盖 | `Overwrite` | 不存在则插入，存在则更新（作业、设置） | 基本幂等 |
| 替换 | `Replace` | 先清空目标中所选实体，再全量写入 | 是，但会删除目标数据 |

三种模式的逐实体行为见第 6 章。

## 2. 工作原理

### 2.1 整体流程

迁移由后台任务执行，前端通过轮询状态获取进度。阶段顺序如下：

| 阶段 | `stage` 值 | 说明 |
| --- | --- | --- |
| 参数校验 | `Validate` | 校验源/目标、模式、二次确认、配置档、Provider 安装状态 |
| 连通性探测 | `Probe` | 源端只读探测，目标端探测并准备目录/数据库 |
| 统计 | — | 读取源端四类实体数量，计算总进度 |
| 暂停调度器 | — | 仅当勾选「迁移期间暂停调度器」时执行 `Standby` |
| 备份 | `Backup` | 文件源自动备份；数据库源不自动备份 |
| 目标结构 | `EnsureSchema` | 目标库执行 EF Core `Migrate`，自动建表或升级结构 |
| 迁移数据 | `Migrate` | 按实体、按批次分页读写 |
| 校验 | `Verify` | 数量校验 + 抽样校验 |
| 完成 | `Done` | 成功结束，生成建议配置；失败为 `Failed`，取消为 `Cancelled` |

### 2.2 分页与流式处理

- 每批默认读取/写入 **500 条**（`BatchSize`，有效范围 1–2000）。
- 文件端的日志、通知使用顺序游标流式读取，避免一次性把大文件加载进内存；写入使用临时文件，成功后原子替换。
- 数据库端按确定性排序分页（避免重复或遗漏），每批写入后清理 EF Core 变更跟踪。

### 2.3 幂等与主键

- **保留源端主键**：日志 `LogId`、设置 `SettingId`、通知 `NotificationId` 原样保留，便于多次迁移与对账。
- **业务键去重**：作业按 `JobGroup + JobName`、设置按 `Key`、日志/通知按各自主键判断「是否已存在」。
- 同一批次内出现重复业务键时，仅写入第一条，其余计为跳过。
- 因此对同一源反复执行 `Merge` 是安全的：第二次预检通常显示「将插入 0，全部跳过」。

### 2.4 迁移后校验

迁移完成后自动执行：

1. **数量校验**
   - `Replace` 模式要求源/目标数量一致，否则产生告警。
   - 非 `Replace` 模式目标数量少于源数量时告警，并提示「源端可能存在重复键」。
2. **抽样校验**
   - 对每个实体读取首部、中部、尾部各最多 20 条记录的业务键，检查目标端是否存在。
   - 抽样缺失会产生告警，告警内容包含实体名与 offset。

校验只产生 **告警**，不会自动回滚；判定迁移成功与否以执行过程中是否抛出异常为准。

## 3. 前置条件

### 3.1 安装数据库驱动包

迁移功能通过反射加载数据库 Provider。**未安装对应 NuGet 包时，该存储类型不可用**，页面会提示缺失的包名。

| 数据库 | NuGet 包 | 反射类型所在程序集 |
| --- | --- | --- |
| MySQL | `Chet.QuartzNet.EFCore.MySQL` | `Chet.QuartzNet.EFCore.MySQL` |
| PostgreSQL | `Chet.QuartzNet.EFCore.PostgreSQL` | `Chet.QuartzNet.EFCore.PostgreSQL` |
| SQLite | `Chet.QuartzNet.EFCore.SQLite` | `Chet.QuartzNet.EFCore.SQLite` |
| SQL Server | `Chet.QuartzNet.EFCore.SqlServer` | `Chet.QuartzNet.EFCore.SqlServer` |

安装示例（版本请与主包保持一致）：

```bash
dotnet add package Chet.QuartzNet.EFCore.SQLite
dotnet add package Chet.QuartzNet.EFCore.MySQL
dotnet add package Chet.QuartzNet.EFCore.PostgreSQL
dotnet add package Chet.QuartzNet.EFCore.SqlServer
```

安装后 **无需额外注册代码**，重启应用即可在迁移面板中选择该存储。

### 3.2 服务注册

迁移功能由 `AddQuartzUI` 自动注册，无需手动调用：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddQuartzUI(builder.Configuration);

var app = builder.Build();
app.MapGet("/", () => Results.Redirect("/quartz-ui"));
app.UseQuartz();
app.MapControllers();
app.Run();
```

### 3.3 权限要求

| 角色 | 要求 |
| --- | --- |
| 文件源 | 读取目录权限；作为目标时需要写权限 |
| 数据库源 | 读取权限；源库存在未应用迁移时，需 `AllowUpgradeSourceSchema` 才可升级结构 |
| 数据库目标 | 建表/改表、写入权限；`Replace` 模式还需要删除权限 |
| 应用账号 | 可访问 QuartzNet.UI 的已登录用户（`QuartzUIPolicy`） |

## 4. 配置说明

### 4.1 完整配置示例

```json
{
  "ConnectionStrings": {
    "QuartzUI": "Data Source=quartzui.db"
  },
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "sqlite",
    "FileStoragePath": "App_Data/QuartzJobs",

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
        },
        "mysql-prod": {
          "Provider": "MySQL",
          "ConnectionString": "Server=127.0.0.1;Port=3306;Database=quartzui;User=root;Password=***;"
        },
        "pg-archive": {
          "Provider": "PostgreSQL",
          "ConnectionString": "Host=127.0.0.1;Port=5432;Database=quartzui;Username=postgres;Password=***;"
        },
        "sqlserver-archive": {
          "Provider": "SqlServer",
          "ConnectionString": "Data Source=127.0.0.1;Initial Catalog=quartzui;User Id=sa;Password=***;TrustServerCertificate=true;"
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

> `StorageType` 和 `DatabaseProvider` 决定应用「当前」使用哪种存储，与迁移的源/目标互不影响。`FileStoragePath` 在数据库存储模式下仍作为文件迁移的默认路径。

### 4.2 `QuartzUI:Migration` 配置项

| 配置项 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `Profiles` | 对象字典 | 空 | 数据库配置档，键为配置档名称 |
| `FileSources` | 数组 | 空 | 额外允许迁移的文件源目录 |
| `BatchSize` | int | 500 | 每批读写条数，实际取值会夹取到 1–2000 |
| `ContinueOnError` | bool | false | 批次写入失败时是否继续后续批次 |
| `MaxErrorSamples` | int | 20 | 错误样本上限，实际取值会夹取到 1–200 |
| `PauseSchedulerDuringMigration` | bool | false | 迁移期间是否暂停调度器触发 |
| `AllowReplace` | bool | false | 是否允许 `Replace` 模式 |
| `AllowAdHocConnectionString` | bool | false | 是否允许 API 直接提交连接字符串 |

### 4.3 数据库配置档 `Profiles`

每个配置档包含：

| 字段 | 说明 |
| --- | --- |
| `Provider` | `MySQL` / `PostgreSQL` / `SQLite` / `SqlServer`，也接受小写与 `postgres`、`npgsql`、`mssql` 等别名 |
| `ConnectionString` | 该数据库的连接字符串，仅保存在服务端 |
| `ServerVersion` | 可选。MySQL 等需要显式指定服务器版本时使用 |

使用要点：

- 配置档名称即页面下拉中显示的名称，建议按环境命名（如 `mysql-prod`、`pg-archive`）。
- 配置档缺少 `ConnectionString` 时，下拉项会置灰并标注「未配置连接字符串」。
- 请求中的 `Provider` 与配置档中的 `Provider` 不一致时会直接被拒绝，避免误连。
- 迁移结果不返回连接字符串，只返回存储展示名，例如 `SQLite(sqlite-dev)`。

### 4.4 文件源 `FileSources`

文件源路径白名单由两部分组成：

1. 当前 `FileStoragePath`
2. `Migration.FileSources` 中配置的每个 `Path`

规则：

- 请求中提交的 `filePath` 必须命中白名单，否则报「文件路径不在服务端允许列表中」。
- 路径为相对路径时，按 **应用工作目录** 解析；建议直接写绝对路径。
- 同名路径会去重，优先使用 `FileStoragePath`。
- `FileSources` 的 `Name` 仅用于界面展示。

### 4.5 `current` 配置档

`current` 是保留名称，代表「当前应用正在使用的数据库」：

- 仅当 `StorageType = Database` 时可用，否则报「当前应用使用文件存储，不存在可用的当前数据库」。
- 使用 `ConnectionStrings:QuartzUI` 和 `QuartzUI:DatabaseProvider`，无需在 `Profiles` 中重复配置。
- 页面下拉显示为「当前数据库 · {Provider}」。
- API 中可通过 `"profileName": "current"` 使用。

典型用途：把文件数据导入当前正在使用的数据库（`当前数据库 → 文件` 方向则用于导出）。

### 4.6 安全建议

- 不要开启 `AllowAdHocConnectionString`，统一使用 `Profiles`，避免连接字符串通过请求体传播。
- `AllowReplace` 默认关闭，仅在确实需要全量替换时临时开启，用完再关闭。
- 迁移接口受 JWT 保护，但仍建议放在内网或受控环境，并限制管理账号。
- `Profiles` 中的密码建议使用环境变量或密钥管理服务注入，不要提交到代码仓库。

## 5. 页面操作指南

### 5.1 打开数据迁移面板

1. 启动应用并登录 QuartzNet.UI。
2. 进入左侧菜单「系统设置」页面。
3. 页面下方即为「数据迁移」卡片。

卡片顶部会显示：

- 当前存储类型
- 文件存储路径
- 路径是否存在

### 5.2 选择迁移源与迁移目标

下拉选项包含三类：

| 选项形式 | 含义 |
| --- | --- |
| `文件存储 · {名称}` | `FileStoragePath` 或 `FileSources` 中的目录 |
| `当前数据库 · {Provider}` | 保留配置档 `current` |
| `{配置档名} · {Provider}` | `Profiles` 中的数据库配置档 |

- 源与目标下拉会互相排除已选中的项。
- 配置档未配置连接字符串时不可选。
- 顶部若提示「以下数据库驱动未安装」，对应数据库将无法选择，需先安装 NuGet 包。

### 5.3 选择写入模式与迁移内容

- **写入模式**：合并 / 覆盖 / 替换。未开启 `AllowReplace` 时，「替换」不可选。
- **迁移内容**：作业数据、作业日志、系统设置、通知消息，至少选择一项。
- **出错后继续**：批次写入失败时继续执行后续批次（默认取服务端配置）。
- **迁移期间暂停调度器**：迁移时暂停触发，结束后自动恢复（任一侧为 true 即生效）。
- **允许升级源库结构**：源库存在未应用的 EF Core 迁移时，允许对源库执行结构升级。

### 5.4 执行预检

点击「预检」，服务端会连接源与目标并返回：

| 列 | 说明 |
| --- | --- |
| 源端 | 源存储中的记录数 |
| 目标已有 | 目标存储中的记录数 |
| 将插入 | 预计新增的记录数 |
| 将更新 | 预计更新的记录数（仅覆盖模式下的作业/设置） |
| 将跳过 | 预计跳过的记录数 |

预检返回 `canRun = false` 时会给出原因（例如配置档不存在、Provider 未安装、源目标类型相同、目标库尚未初始化等）。**建议每次迁移前先预检**，确认预计写入量符合预期。

### 5.5 开始迁移

预检通过后点击「开始迁移」：

1. 若选择了 `Replace`，会弹出二次确认对话框。
2. 确认后迁移在后台执行，按钮变为「迁移中...」并出现「取消迁移」。
3. 页面自动轮询状态并刷新进度。

> 迁移成功后再次点击「开始迁移」会被拒绝，并提示使用「强制重新迁移」。这是为了避免误触重复执行；需要重新执行时点击该按钮即可（对应请求 `force = true`）。

### 5.6 查看进度

进度区域包含：

| 字段 | 说明 |
| --- | --- |
| 迁移进度 | 整体百分比 `processed / total` |
| 当前阶段 | `Validate` / `Probe` / `Backup` / `EnsureSchema` / `Migrate` / `Verify` / `Done` |
| 当前实体 | 正在迁移的实体（`jobs` / `logs` / `settings` / `notifications`） |
| 开始时间 / 结束时间 / 耗时 | 时间信息 |
| 各实体步骤 | 已迁移、跳过、更新、失败数量与状态 |

状态标识：

- 蓝色：迁移进行中
- 绿色：迁移完成
- 红色：迁移失败
- 琥珀色：迁移已取消
- 灰色：等待迁移

### 5.7 取消迁移

点击「取消迁移」：

- 服务端在当前批次结束后停止，已写入的数据会保留。
- 未完成的实体被标记为失败，状态标记为「迁移已取消」。
- 取消后可以重新预检并再次执行（`Merge` 不会重复写入已有数据）。

### 5.8 查看结果与配置建议

迁移完成后，卡片会展示：

- **迁移摘要**：源/目标展示名、模式、总耗时
- **迁移告警**：数量不一致、抽样缺失、暂停调度器失败等
- **错误样本**：实体、记录键、错误信息（最多保留 `MaxErrorSamples` 条）
- **迁移后配置建议**：切换存储所需的 `appsettings.json` 片段

配置建议示例：

```json
{
  "QuartzUI": {
    "StorageType": "Database",
    "DatabaseProvider": "SQLite"
  },
  "ConnectionStrings": {
    "QuartzUI": "<目标连接字符串>"
  }
}
```

## 6. 三种写入模式详解

### 6.1 行为矩阵

| 实体 | Merge（合并） | Overwrite（覆盖） | Replace（替换） |
| --- | --- | --- | --- |
| 作业 | 不存在则插入；存在则跳过 | 不存在则插入；存在则更新（保留目标 `CreateTime`） | 清空目标作业后全部插入 |
| 日志 | 按 `LogId` 去重；存在则跳过 | 同 Merge（日志视为不可变，已存在则跳过） | 清空目标日志后全部插入 |
| 设置 | 按 `Key` 去重；存在则跳过 | 存在则更新 `Value`、`Description`、`Enabled`、`UpdateTime` | 清空目标设置后全部插入 |
| 通知 | 按 `NotificationId` 去重；存在则跳过 | 同 Merge | 清空目标通知后全部插入 |

补充说明：

- `Merge` 与 `Overwrite` 的插入项都会 **保留源端主键与创建时间**。
- 来源数据自身存在重复业务键时，重复记录计为「跳过」，因此目标数量可能少于源数量。
- `Replace` 模式即使源端某实体为 0 条，也会清空目标对应实体，请谨慎使用。

### 6.2 Merge（合并）

适用场景：首次迁移、增量同步、可重复执行。

特点：

- 不会修改目标已有记录。
- 重复执行安全，第二次预检通常显示「将插入 0」。
- 适合从生产环境向测试环境补充数据，但不会把生产修改过的字段同步到已有记录。

### 6.3 Overwrite（覆盖）

适用场景：目标已有同键记录，但需要以源端的最新配置为准。

特点：

- 作业、设置会被更新；日志、通知仍只做去重插入。
- 更新作业时不会覆盖目标的 `CreateTime`，保留首次创建时间。
- 若源端与目标端对同一作业的 `TriggerName` 等字段不同，更新后以源端为准。

### 6.4 Replace（替换）

适用场景：目标数据已废弃，需要完全以源端为准的重建式迁移。

启用条件：

1. 服务端 `QuartzUI:Migration:AllowReplace = true`
2. 页面二次确认，或 API 传入 `confirmReplace = true`

执行效果：

- 先对所选实体执行删除（分块删除，避免大事务），再全量写入。
- 文件端目标在清空前会自动备份到 `Backup/migration_yyyyMMdd_HHmmss`。
- 数据库端不会自动备份，需提前做数据库快照或逻辑备份。

### 6.5 模式选择建议

| 场景 | 建议模式 |
| --- | --- |
| 首次迁入空库/空目录 | Merge |
| 反复执行、补数据 | Merge |
| 以源端配置为准更新目标 | Overwrite |
| 目标数据需要整体重建 | Replace（先备份） |

## 7. API 使用指南

### 7.1 鉴权

所有迁移接口均受 `QuartzUIPolicy` 保护。先调用登录接口获取 JWT：

```http
POST /api/quartz/Login
Content-Type: application/json

{
  "userName": "Admin",
  "password": "123456"
}
```

响应中的 `data.accessToken` 用于后续请求：

```http
Authorization: Bearer {accessToken}
```

### 7.2 接口一览

以下接口均提供新旧两套路由，行为完全一致：

| 功能 | 推荐路由（新） | 兼容路由（旧） | 方法 |
| --- | --- | --- | --- |
| 获取可选项 | `/api/quartz/StorageMigration/GetOptions` | `/api/quartz/GetMigrationOptions` | GET |
| 获取状态 | `/api/quartz/StorageMigration/GetStatus` | `/api/quartz/GetMigrationStatus` | GET |
| 预检 | `/api/quartz/StorageMigration/Preview` | `/api/quartz/PreviewMigration` | POST |
| 触发迁移 | `/api/quartz/StorageMigration/Trigger` | `/api/quartz/TriggerMigration` | POST |
| 取消迁移 | `/api/quartz/StorageMigration/Cancel` | `/api/quartz/CancelMigration` | POST |

统一响应结构：

```json
{
  "success": true,
  "message": "操作成功",
  "data": {}
}
```

### 7.3 请求字段

`Preview` 与 `Trigger` 共用同一请求体：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `force` | bool | 否 | 强制重新迁移。成功后再次触发必须为 `true` |
| `source` | 对象 | 否 | 迁移源，省略时默认当前 `FileStoragePath` |
| `target` | 对象 | 否 | 迁移目标，省略时默认当前数据库 |
| `entities` | 字符串数组 | 否 | `Jobs` / `Logs` / `Settings` / `Notifications`，省略时全选 |
| `mode` | string | 否 | `Merge`（默认）/ `Overwrite` / `Replace` |
| `continueOnError` | bool? | 否 | 省略时取 `QuartzUI:Migration:ContinueOnError` |
| `confirmReplace` | bool | 否 | `Replace` 模式必须为 `true` |
| `pauseScheduler` | bool | 否 | 为 `true` 或配置开启时暂停调度器 |
| `allowUpgradeSourceSchema` | bool | 否 | 允许升级源库结构，默认 `false` |

`source` / `target` 对象字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `type` | string | `File` / `MySql` / `PostgreSql` / `SQLite` / `SqlServer` |
| `filePath` | string | `File` 类型使用，必须在服务端允许列表内 |
| `profileName` | string | 数据库配置档名称，或保留值 `current` |
| `connectionString` | string | 仅当 `AllowAdHocConnectionString = true` 时允许 |
| `serverVersion` | string | 可选，MySQL 等场景使用 |

### 7.4 预检示例

请求：

```http
POST /api/quartz/StorageMigration/Preview
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "source": {
    "type": "File",
    "filePath": "App_Data/QuartzJobs"
  },
  "target": {
    "type": "SQLite",
    "profileName": "sqlite-dev"
  },
  "entities": ["Jobs", "Logs", "Settings", "Notifications"],
  "mode": "Merge"
}
```

响应（示例数据）：

```json
{
  "success": true,
  "message": "操作成功",
  "data": {
    "canRun": true,
    "message": null,
    "source": "File(D:\\App\\App_Data\\QuartzJobs)",
    "target": "SQLite(sqlite-dev)",
    "mode": "Merge",
    "entities": [
      {
        "entity": "jobs",
        "name": "作业数据",
        "sourceCount": 12,
        "targetCount": 8,
        "willInsert": 4,
        "willUpdate": 0,
        "willSkip": 8
      },
      {
        "entity": "logs",
        "name": "作业日志",
        "sourceCount": 1108,
        "targetCount": 0,
        "willInsert": 1108,
        "willUpdate": 0,
        "willSkip": 0
      }
    ],
    "warnings": []
  }
}
```

### 7.5 触发迁移示例

```http
POST /api/quartz/StorageMigration/Trigger
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "force": false,
  "source": {
    "type": "File",
    "filePath": "App_Data/QuartzJobs"
  },
  "target": {
    "type": "SQLite",
    "profileName": "sqlite-dev"
  },
  "entities": ["Jobs", "Logs", "Settings", "Notifications"],
  "mode": "Merge",
  "continueOnError": false,
  "pauseScheduler": true,
  "allowUpgradeSourceSchema": false
}
```

响应：

```json
{
  "success": true,
  "message": "迁移任务已触发",
  "data": true
}
```

触发接口只返回「是否已启动」，实际执行在后台进行，需要轮询状态接口。

### 7.6 查询状态

```http
GET /api/quartz/StorageMigration/GetStatus
Authorization: Bearer {accessToken}
```

响应（示例）：

```json
{
  "success": true,
  "message": "操作成功",
  "data": {
    "isRunning": true,
    "progressPercent": 42,
    "currentStep": "迁移数据",
    "migrationId": "3f2b1c9d8e7a4b5c9d0e1f2a3b4c5d6e",
    "sourceDisplay": "File(D:\\App\\App_Data\\QuartzJobs)",
    "targetDisplay": "SQLite(sqlite-dev)",
    "mode": "Merge",
    "stage": "Migrate",
    "currentEntity": "logs",
    "processed": 512,
    "total": 1211,
    "isCompleted": false,
    "isSuccess": false,
    "isCancelled": false,
    "warnings": [],
    "entities": [
      {
        "entity": "jobs",
        "name": "作业数据",
        "status": 2,
        "sourceCount": 12,
        "processed": 12,
        "inserted": 4,
        "updated": 0,
        "skipped": 8,
        "failed": 0
      }
    ],
    "errorSamples": [],
    "suggestedConfig": null
  }
}
```

> 实际字段名以接口返回为准；`stage`、`currentEntity` 等字段为字符串，实体与步骤的 `status` 为数字枚举（见下表）。

状态字段说明：

| 字段 | 说明 |
| --- | --- |
| `isRunning` | 是否正在执行 |
| `isCompleted` | 是否已结束（成功、失败、取消均为 `true`） |
| `isSuccess` | 是否成功 |
| `isCancelled` | 是否被取消 |
| `stage` | 当前阶段 |
| `currentEntity` | 当前实体键 |
| `processed` / `total` | 已处理条数 / 总条数（所选实体源端合计） |
| `entities` | 逐实体统计：插入、更新、跳过、失败 |
| `errorSamples` | 错误样本（实体、记录键、错误信息） |
| `warnings` | 告警与校验提示 |
| `suggestedConfig` | 成功后生成的配置片段 |

`entities[].status` 与 `steps[].status` 数字枚举：

| 值 | 状态 | 含义 |
| --- | --- | --- |
| 0 | `Pending` | 等待中 |
| 1 | `Running` | 迁移中 |
| 2 | `Completed` | 已完成 |
| 3 | `Failed` | 失败 |
| 4 | `Skipped` | 跳过（源端无数据或未选择） |

`stage` 取值：`Idle`、`Validate`、`Probe`、`Backup`、`EnsureSchema`、`Migrate`、`Verify`、`Done`、`Failed`、`Cancelled`。

### 7.7 取消迁移

```http
POST /api/quartz/StorageMigration/Cancel
Authorization: Bearer {accessToken}
```

- 有运行中的任务时返回成功，并在当前批次结束后停止。
- 没有运行中的任务时返回失败，消息为「当前没有正在运行的迁移任务」。

### 7.8 PowerShell 完整示例

```powershell
$base = "http://localhost:5099"

# 1. 登录
$login = Invoke-RestMethod -Method Post -Uri "$base/api/quartz/Login" `
  -ContentType "application/json" `
  -Body '{"userName":"Admin","password":"123456"}'
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }

# 2. 查看可选项
Invoke-RestMethod -Method Get -Uri "$base/api/quartz/StorageMigration/GetOptions" -Headers $headers

# 3. 预检
$previewBody = @{
  source = @{ type = "File"; filePath = "App_Data/QuartzJobs" }
  target = @{ type = "SQLite"; profileName = "sqlite-dev" }
  mode   = "Merge"
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "$base/api/quartz/StorageMigration/Preview" `
  -Headers $headers -ContentType "application/json" -Body $previewBody

# 4. 触发
$triggerBody = @{
  source = @{ type = "File"; filePath = "App_Data/QuartzJobs" }
  target = @{ type = "SQLite"; profileName = "sqlite-dev" }
  entities = @("Jobs", "Logs", "Settings", "Notifications")
  mode = "Merge"
  pauseScheduler = $true
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "$base/api/quartz/StorageMigration/Trigger" `
  -Headers $headers -ContentType "application/json" -Body $triggerBody

# 5. 轮询状态
do {
  Start-Sleep -Seconds 2
  $status = (Invoke-RestMethod -Method Get -Uri "$base/api/quartz/StorageMigration/GetStatus" -Headers $headers).data
  Write-Host "$($status.stage) $($status.progressPercent)% $($status.currentEntity)"
} while ($status.isRunning)
```

## 8. 典型迁移场景

### 8.1 文件 → SQLite

适用：单机文件存储升级为 SQLite。

1. 安装 `Chet.QuartzNet.EFCore.SQLite` 包并重启。
2. 配置 `Profiles.sqlite-dev`，`Provider = SQLite`，连接串指向目标文件。
3. 页面选择源「文件存储 · 当前文件存储」、目标「sqlite-dev · SQLite」。
4. 模式选择「合并」，点击「预检」确认预计写入量。
5. 点击「开始迁移」，等待完成。
6. 将 `StorageType` 改为 `Database`、`DatabaseProvider` 改为 `sqlite`，连接串指向同一 SQLite 文件，重启应用。
7. 验证作业列表、日志、设置与迁移前一致。

### 8.2 SQLite → 文件

适用：从数据库导出为文件备份。

1. 页面选择源「sqlite-dev · SQLite」、目标「文件存储 · 当前文件存储」。
2. 目标目录不存在时会自动创建。
3. 模式选择「合并」，执行迁移。
4. 迁移完成后检查目标目录下的 `jobs.json`、`logs.json`、`setting.json`、`notification.json`。
5. 如需让应用改回文件存储：`StorageType = File`、`FileStoragePath` 指向该目录，重启应用。

### 8.3 MySQL → PostgreSQL

适用：数据库整体更换。

1. 同时安装 `Chet.QuartzNet.EFCore.MySQL` 与 `Chet.QuartzNet.EFCore.PostgreSQL`。
2. 配置两个配置档：`mysql-prod`、`pg-prod`。
3. 源选 `mysql-prod · MySQL`，目标选 `pg-prod · PostgreSQL`。
4. 首次用「合并」模式迁移；目标库为空时预检会显示全部为「将插入」。
5. 迁移完成后修改 `DatabaseProvider = postgresql` 与连接串，重启应用。

注意：

- 源库若存在未应用的 EF Core 迁移，会报「源数据库存在未应用的迁移」；确认结构版本后勾选「允许升级源库结构」或先启动应用完成升级。
- 迁移不改变源库数据，可在验证通过后再下线旧库。

### 8.4 增量合并

适用：定期把生产环境的作业配置同步到测试环境。

1. 源选生产配置档，目标选测试配置档。
2. 模式「合并」，只勾选「作业数据」和「系统设置」。
3. 每次执行前先预检；已存在的作业会显示为「将跳过」，只写入新增部分。
4. 需要让已有作业以生产为准时，改用「覆盖」模式。

### 8.5 以文件为中转

适用：同类型存储之间无法直接迁移（如两个 SQLite 库）。

1. SQLite A → 文件（导出）。
2. 文件 → SQLite B（导入）。
3. 两段迁移都使用「合并」即可保证幂等。

文件目录同时充当可审计的中间快照。

### 8.6 迁移后切换存储

迁移与配置切换是两个独立步骤：

| 步骤 | 操作 | 是否需要重启 |
| --- | --- | --- |
| 迁移数据 | 页面或 API 执行 | 否 |
| 切换存储 | 修改 `StorageType` / `DatabaseProvider` / 连接串 | 是 |
| 验证 | 检查作业、日志、设置、通知 | 否 |

未切换配置时，应用仍读写原存储，新目标的数据不会被使用。

## 9. 备份、校验与回滚

### 9.1 文件端自动备份

- 作为 **源** 的文件目录：迁移开始前（`Backup` 阶段）自动备份。
- 作为 **目标** 的文件目录：`Replace` 模式清空前自动备份。
- 备份位置：`{文件存储目录}/Backup/migration_yyyyMMdd_HHmmss/`
- 备份内容：目录下的数据文件（`jobs.json`、`logs.json`、`setting.json`、`notification.json`）。

示例：

```text
App_Data/QuartzJobs/
├─ jobs.json
├─ logs.json
├─ setting.json
├─ notification.json
└─ Backup/
   └─ migration_20261103_150210/
      ├─ jobs.json
      ├─ logs.json
      ├─ setting.json
      └─ notification.json
```

> 自动备份不参与 `MaxBackupFiles` 的常规备份轮转，建议手动清理过期目录。

### 9.2 数据库端备份

数据库 **不会** 自动备份。迁移前请自行执行：

- SQLite：复制 `.db` 文件（需停止写入或使用备份 API）。
- MySQL：`mysqldump` 或逻辑备份。
- PostgreSQL：`pg_dump`。
- SQL Server：数据库备份或快照。

`Replace` 模式会删除目标数据，数据库备份是唯一可靠的恢复手段。

### 9.3 迁移后校验清单

1. 状态接口 `isSuccess = true`，`isCompleted = true`。
2. 各实体 `failed = 0`。
3. `warnings` 中没有「记录未写入目标」的抽样告警。
4. 数量差异告警能解释（通常是源端重复键）。
5. 在「作业管理」「日志管理」页面抽查作业数量、最近日志，并在「系统设置」页面确认设置值。
6. 切换配置并重启后，确认调度器能正常加载作业。

### 9.4 回滚步骤

| 场景 | 回滚方式 |
| --- | --- |
| 尚未切换配置 | 无需回滚，应用仍使用原存储 |
| 已切换配置 | 改回原 `StorageType` / `DatabaseProvider` / 连接串并重启 |
| 文件目标被 Replace | 从 `Backup/migration_*` 目录恢复文件 |
| 数据库目标被 Replace | 使用迁移前的数据库备份恢复 |
| 迁移中断 | 修复原因后重新预检并以 `Merge` 继续；已写入的数据不会重复插入 |

## 10. 常见问题

### 10.1 未找到迁移配置档：xxx

- 检查 `Profiles` 的键名拼写与请求中的 `profileName` 是否一致。
- 确认配置位于 `QuartzUI:Migration:Profiles` 下，而不是 `QuartzUI` 同级。
- 修改配置后需重启应用才能生效。

### 10.2 未安装 xxx 包，无法使用 xxx 存储

- 安装对应 NuGet 包（见 3.1 节），确保版本与主包匹配。
- 重启应用，`GetOptions` 的 `providers[].installed` 应变为 `true`。

### 10.3 源与目标的存储类型相同

同一存储类型之间不提供迁移。请改用文件作为中转（见 8.5 节）。

### 10.4 Replace 模式未启用

在 `appsettings.json` 中设置：

```json
{
  "QuartzUI": {
    "Migration": {
      "AllowReplace": true
    }
  }
}
```

重启后页面中的「替换」选项才会可选。API 还需要传 `confirmReplace = true`。

### 10.5 迁移已完成，如需重新迁移请点击「强制重新迁移」

这是幂等保护。重新执行时请求体加入 `"force": true`。

### 10.6 源数据库存在未应用的迁移

含义：源数据库结构版本落后于当前代码。可选做法：

1. 先启动一次应用连上源库，让 EF Core 完成结构升级（推荐）。
2. 或勾选「允许升级源库结构」（`allowUpgradeSourceSchema = true`），由迁移流程对源库执行 `Migrate`。

升级源库结构属于写操作，生产环境请先备份。

### 10.7 文件路径不在服务端允许列表中

请求的 `filePath` 必须是 `FileStoragePath` 或 `Migration.FileSources` 中的路径。相对路径按应用工作目录解析，跨目录访问需显式配置 `FileSources`。

### 10.8 目标数量少于源数量

告警形如：

```text
作业日志数量可能不足：源 1108，目标 765（源端可能存在重复键）
```

原因通常是源数据本身存在重复业务键（例如 `logs.json` 中重复的 `LogId`）。合并模式会按主键去重，属于预期行为；如需保留全部重复记录，应先清理源数据或改用其他主键策略。

### 10.9 暂停调度器失败

可能原因：

- 调度器未启动（`AutoStartScheduler = false`）。
- 使用远程管理，当前实例无法直接操作调度器。

该问题只产生告警，不阻塞迁移。迁移期间请避免在页面手工修改作业。

### 10.10 迁移成功但作业没有变化

- 确认是否已修改 `StorageType` / `DatabaseProvider` / 连接串并重启。
- 确认迁移目标与应用当前使用的存储是同一实例（特别是 `current` 配置档与 `Profiles` 的差异）。
- 检查 `warnings` 中是否有抽样缺失告警。

### 10.11 不允许直接提交连接字符串

服务端未开启 `AllowAdHocConnectionString`。请使用 `Profiles` 配置档，或临时开启该选项（不推荐长期开启）。

### 10.12 取消后无法立即重新迁移

等待状态接口返回 `isRunning = false`、`isCompleted = true`。取消是「批次边界生效」，大日志批次可能需要一点时间收尾。

## 11. 最佳实践

1. **先预检，再执行**：预检能提前暴露配置档、Provider、路径、目标结构等问题。
2. **先备份，再 Replace**：数据库端没有自动备份，Replace 前务必做快照或逻辑备份。
3. **生产迁移期间暂停调度器**：勾选「迁移期间暂停调度器」，避免迁移过程中作业状态被并发改写。
4. **日志分批处理**：日志量大时优先调整 `BatchSize`（如 1000–2000），并观察状态接口的 `processed/total`。
5. **迁移期间避免其他写操作**：不要在页面新增/编辑作业，也不要运行另一个迁移任务（服务端只允许一个任务）。
6. **保留源数据直到验证完成**：迁移不会删除源数据，验证通过后再考虑下线旧存储。
7. **用合并模式做增量**：日常同步使用 `Merge`；只有确需以源端覆盖目标时才用 `Overwrite`。
8. **连接串只放服务端**：统一使用 `Profiles`，保持 `AllowAdHocConnectionString = false`。
9. **账号最小权限**：源库只读；目标库按模式授予建表、写入、删除权限。
10. **迁移后核对告警**：数量告警与抽样告警是判断数据完整性的第一手依据。

## 12. 兼容性与旧接口

为兼容旧版本前端与脚本，以下旧路由继续可用：

| 旧路由 | 行为 |
| --- | --- |
| `GET /api/quartz/GetMigrationStatus` | 等价于 `StorageMigration/GetStatus` |
| `GET /api/quartz/GetMigrationOptions` | 等价于 `StorageMigration/GetOptions` |
| `POST /api/quartz/PreviewMigration` | 等价于 `StorageMigration/Preview` |
| `POST /api/quartz/TriggerMigration` | 等价于 `StorageMigration/Trigger` |
| `POST /api/quartz/CancelMigration` | 等价于 `StorageMigration/Cancel` |

旧接口在省略源/目标时保持原有默认行为：

- 源：当前文件存储路径
- 目标：当前数据库（要求 `StorageType = Database`）

`FileDataMigrationService` 仍作为兼容壳保留，内部委托给 `StorageMigrationService`。

## 13. 附录

### 13.1 数据文件与数据表

| 实体 | 文件存储文件 | 数据库表 |
| --- | --- | --- |
| 作业 | `jobs.json` | `quartz_jobs` |
| 日志 | `logs.json` | `quartz_job_logs` |
| 设置 | `setting.json` | `quartz_settings` |
| 通知 | `notification.json` | `quartz_notifications` |

### 13.2 关键类型

| 类型 | 命名空间 | 作用 |
| --- | --- | --- |
| `StorageMigrationService` | `Chet.QuartzNet.UI.Services` | 迁移编排、预检、状态、取消 |
| `IMigrationStore` | `Chet.QuartzNet.Core.Interfaces` | 存储适配器接口 |
| `FileMigrationStore` | `Chet.QuartzNet.Core.Migration` | 文件存储适配器 |
| `EfCoreMigrationStore` | `Chet.QuartzNet.EFCore.MigrationSupport` | 数据库存储适配器 |
| `MigrationStoreFactory` | `Chet.QuartzNet.EFCore.MigrationSupport` | 适配器工厂与 Provider 反射加载 |
| `MigrationStoreType` / `MigrationEntity` / `MigrationWriteMode` | `Chet.QuartzNet.Models.DTOs` | 存储、实体、模式枚举 |

### 13.3 限制说明

- 同一时间只允许一个迁移任务。
- 源与目标不能是同一存储类型。
- 迁移不会自动切换应用存储配置。
- 数据库端不自动备份。
- 校验为「数量 + 抽样」，不做逐字段全量比对。
- 文件端写入使用临时文件原子替换，但迁移期间仍应避免其他进程同时写同一目录。

---

**更新时间**：2026-11-03
