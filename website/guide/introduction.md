---
description: 从整体认识 Chet.QuartzNet.UI：它解决什么问题、具备哪些能力，以及适合什么样的项目。
---

# 项目简介

## 1. 它是什么

**Chet.QuartzNet.UI** 是一个基于 **.NET 8.0 / .NET 10.0** 与 **Quartz.Net** 的可视化作业调度管理系统。它把 Quartz.Net 的作业、触发器、Cron 表达式、执行日志等概念，封装成一套开箱即用的 Web 管理界面。

你只需要在现有的 ASP.NET Core 项目中安装一个 NuGet 包、注入两个服务，就可以通过浏览器完成定时任务的创建、管理、监控与通知配置，而不必为每个作业编写调度代码。

![登录界面](/images/login.png)

![分析页](/images/analytices.png)

## 2. 解决什么问题

直接使用 Quartz.Net 通常需要：

- 为每个作业编写 `IJob` 实现与 `JobDetail` / `Trigger` 注册代码；
- 自己维护 Cron 表达式、启停状态和执行历史；
- 额外开发一套管理界面与权限认证；
- 处理失败重试、执行通知、日志清理等运维问题。

Chet.QuartzNet.UI 把上述工作收敛到一个组件包中：

| 传统做法 | Chet.QuartzNet.UI |
| --- | --- |
| 手写作业注册代码 | 界面创建，或 `[QuartzJob]` 特性自动注册 |
| 自己实现管理后台 | 内置 VbenAdmin 管理界面 |
| 自己记录执行历史 | 内置执行日志与详情 |
| 自己实现通知 | 内置 PushPlus 多渠道通知 |
| 自己处理存储 | 文件 / 四种数据库，可迁移 |
| 自己加认证 | 内置 JWT 认证 |

## 3. 核心特性

- **可视化管理**：作业的新增、编辑、复制、暂停、恢复、立即执行、删除全部在界面完成。
- **实时监控**：分析页提供 KPI 卡片、健康象限、时段热力图、耗时排行等可视化图表。
- **ClassJob 模式**：用 `[QuartzJob]` 特性标记 C# 类，启动时自动扫描并注册。
- **API 作业模式**：无需编写代码，通过 HTTP 请求触发外部接口。
- **多种存储**：文件存储零依赖；数据库存储支持 MySQL、PostgreSQL、SQL Server、SQLite。
- **执行日志**：完整记录开始时间、耗时、结果、错误信息与「已跳过」原因，并支持自动清理。
- **失败重试**：可配置重试次数与重试间隔，减少偶发失败带来的告警噪音。
- **作业依赖**：前置作业全部成功才执行，否则跳过并记录日志。
- **通知集成**：基于 PushPlus 推送至微信、企业微信、钉钉、邮件等渠道，支持 HTML / Markdown / 纯文本模板。
- **数据迁移**：五种存储任意互转，提供预检、进度、备份与迁移后校验。
- **国际化与主题**：内置中英文切换，跟随 VbenAdmin 的浅色 / 深色主题。
- **无侵入集成**：以 Razor Class Library 打包，安装后按需启用，不改变现有项目结构。

## 4. 技术栈

| 层面 | 技术 |
| --- | --- |
| 运行时 | .NET 8.0 / .NET 10.0 |
| 调度内核 | Quartz.Net 3.15.x |
| Web 框架 | ASP.NET Core（Razor Class Library 打包） |
| 数据访问 | EF Core（数据库存储模式） |
| 前端框架 | Vue 3 + VbenAdmin（Ant Design Vue） |
| 表格 / 图表 | vxe-table、ECharts |
| 认证 | JWT |
| 通知服务 | PushPlus |

## 5. 功能地图

| 模块 | 说明 | 文档 |
| --- | --- | --- |
| 分析页 | 作业统计、健康象限、热力图、耗时排行 | [分析页](/modules/dashboard) |
| 作业管理 | 作业增删改查、启停、批量操作、依赖配置 | [作业管理](/modules/job) |
| 日志管理 | 执行历史、状态筛选、详情、自动清理 | [日志管理](/modules/log) |
| 通知管理 | 通知配置、策略、历史、测试通知 | [通知管理](/modules/notification) |
| 系统配置 | 服务标识、日志清理、数据迁移入口 | [系统配置](/modules/system-config) |
| 多语言 / 主题 | 中英文切换、浅色深色主题 | [多语言与主题](/modules/i18n) |

## 6. 适用场景

**适合：**

- 已有 ASP.NET Core 项目，想快速补一套定时任务管理界面；
- 中小型系统，希望用文件存储快速起步，后续再平滑切换到数据库；
- 需要给运维 / 业务人员提供可视化配置入口，而不是改代码发版；
- 需要执行历史、失败重试、失败通知等基础运维能力；
- 希望以较低成本把多个零散定时任务集中管理。

**不适合：**

- 需要审批流、RBAC 细粒度权限、操作审计等重型后台能力；
- 需要复杂 DAG 编排、跨系统分布式调度的大规模场景。

> Chet.QuartzNet.UI 的定位是**轻量的定时任务可视化管理工具**：配置能少就少，打开就能用。v2.6.0 起仍坚持不做 RBAC、审批流等重型功能。

## 7. 项目结构

```text
Chet.QuartzNet.UI/
├── src/
│   ├── Chet.QuartzNet.UI/                   # UI 组件、控制器、中间件（RCL）
│   ├── Chet.QuartzNet.Models/               # 实体与 DTO
│   ├── Chet.QuartzNet.EFCore/               # EF Core 数据访问核心
│   ├── Chet.QuartzNet.EFCore.MySql/         # MySQL 支持
│   ├── Chet.QuartzNet.EFCore.PostgreSql/    # PostgreSQL 支持
│   ├── Chet.QuartzNet.EFCore.SQLite/        # SQLite 支持
│   ├── Chet.QuartzNet.EFCore.SqlServer/     # SQL Server 支持
│   └── Chet.QuartzNet.Web/                  # 前端工程与示例
├── examples/
│   ├── Chet.QuartzNet.Database.Example/     # 数据库存储示例
│   └── Chet.QuartzNet.File.Example/         # 文件存储示例
└── docs/                                    # 文档与截图
```

## 8. 下一步

- [快速开始](/guide/quick-start)：5 分钟完成集成。
- [核心架构](/guide/architecture)：了解组件分层与请求链路。
- [存储模式](/guide/storage)：选择文件存储或数据库存储。
- [更新日志](/changelog)：查看每个版本的变化。
