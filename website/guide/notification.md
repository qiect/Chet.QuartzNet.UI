---
description: 基于 PushPlus 配置多渠道通知：微信、企业微信、钉钉、邮件，支持三种模板与通知策略。
---

# 通知配置

## 1. 功能概述

通知功能基于 **PushPlus** 服务，在作业执行成功、失败或调度器异常时推送消息。PushPlus 聚合了微信、企业微信、钉钉、邮件、短信等多种渠道。

支持两类通知：

- **作业执行通知**：作业成功或失败时发送；
- **调度器异常通知**：调度器发生异常时发送。

![通知配置](/images/notification-config.png)

## 2. 注册 PushPlus

1. 访问 [PushPlus 官网](https://www.pushplus.plus/)；
2. 使用微信扫码登录；
3. 进入「个人中心」，复制专属 **Token**。

## 3. 配置通知

进入「通知管理」页，点击「通知配置」：

| 配置项 | 说明 | 示例 |
| --- | --- | --- |
| 启用通知 | 通知功能总开关 | 开启 |
| Token | PushPlus 个人 Token | `abcdef123456...` |
| 推送渠道 | 消息推送渠道 | `wechat` |
| 消息模板 | HTML / Markdown / 纯文本 | `markdown` |
| 主题 | 通知主题（可选） | `Quartz 通知` |

### 通知策略

| 策略 | 说明 | 默认值 |
| --- | --- | --- |
| 作业成功时发送 | 成功执行是否通知 | 关闭 |
| 作业失败时发送 | 失败执行是否通知 | 开启 |
| 调度器异常时发送 | 调度器异常是否通知 | 开启 |

建议至少开启「失败」与「调度器异常」两类，成功通知按需开启以免打扰。

## 4. 三种模板

### HTML 模板

适合微信等富文本渠道，包含状态标识、信息表格、颜色区分与执行详情。

```html
<div style="font-family: -apple-system, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; color: #24292f; max-width: 720px; margin: 0 auto; padding: 20px;">
  <div style="background: #fff; border: 1px solid #e1e4e8; border-radius: 6px; overflow: hidden;">
    <div style="background: #f0fff4; border-bottom: 1px solid #e1e4e8; padding: 16px 20px;">
      <h1 style="margin: 0; font-size: 20px; color: #2da44e;">✅ 作业执行成功: 示例任务</h1>
    </div>
    <!-- 作业信息表格 -->
  </div>
</div>
```

### Markdown 模板

适合技术人员阅读，结构清晰：

```markdown
# ❌ 作业执行失败: 示例任务

| 项目 | 内容 |
| --- | --- |
| 作业名称 | 示例任务 |
| 作业分组 | default |
| 执行状态 | ❌ 失败 |
| 执行时间 | 2026-11-03 22:30:00 |
| 执行耗时 | 1500 毫秒 |
| 错误信息 | System.Exception: 执行过程中发生错误 |
```

### 纯文本模板

兼容性最好，适合邮件或短信渠道：

```text
调度器异常

异常时间: 2026-11-03 22:35:00
异常类型: System.InvalidOperationException
异常消息: 调度器初始化失败

此消息由 Chet.QuartzNet.UI 调度系统自动发送，请勿回复。
```

## 5. 测试通知

配置完成后点击「发送测试通知」，确认能收到消息。若发送失败：

- 检查 Token 是否有效；
- 检查网络是否能访问 PushPlus；
- 检查推送渠道是否支持所选模板；
- 查看通知历史中的错误信息。

## 6. 通知历史

「通知管理」页会记录每次发送：

- 通知标题、触发来源、状态（待发送 / 成功 / 失败）；
- 创建时间、发送时间、耗时；
- 支持按状态筛选、按来源搜索；
- 支持单条删除与批量清空。

![通知历史](/images/notification.png)

## 7. 最佳实践

1. 必须开启「作业失败」与「调度器异常」通知；
2. 微信渠道推荐 HTML 或 Markdown 模板；
3. 每次修改配置后发送测试通知；
4. 定期清理通知历史，避免数据无限增长；
5. 对关键作业单独配置通知策略，减少无关噪声。
