---
title: "通知功能详解与配置指南"
description: "基于 PushPlus 配置作业失败、成功与调度器异常通知：渠道、模板、策略与排错。"
---

# 通知功能详解与配置指南

定时任务出问题时，最常见的两个场景是：任务失败了没人知道，以及调度器异常了没人发现。通知功能把这些事件推送到手机或群组，避免只能靠翻日志发现故障。

## 1. 工作方式

通知基于 **PushPlus**：系统把消息提交给 PushPlus，再由它按你选择的渠道投递。因此不需要自己维护 SMTP、企业微信应用或钉钉机器人，只需要一个 PushPlus Token。

触发时机只有三类：

| 事件 | 说明 |
| ---- | ---- |
| 作业执行失败 | 作业抛出异常或执行结果为失败 |
| 作业执行成功 | 默认关闭，按需开启 |
| 调度器异常 | Quartz 核心服务异常时发送 |

::: tip
通知是**系统级配置**，不支持为单个作业单独设置渠道或接收人。作业层面的开关只有失败重试、禁止并发与中断补跑。
:::

## 2. 获取 Token

1. 打开 [PushPlus 官网](https://www.pushplus.plus/)；
2. 使用微信扫码登录；
3. 进入「个人中心」，复制 Token。

## 3. 通知配置

登录管理界面，进入「通知管理」，点击「通知配置」。

![通知配置](/images/notification-config.png)

### 3.1 基础配置

| 配置项 | 说明 |
| ------ | ---- |
| 启用通知 | 通知功能总开关，关闭后所有策略都不生效 |
| Token | PushPlus 个人 Token，必填 |
| 推送渠道 | 见 3.2 |
| 消息模板 | `html` / `markdown` / `txt` / `json` |
| 业务主题（Topic） | 群组编码，填写后推送到群组；不填则发给个人 |

### 3.2 推送渠道

| 渠道 | `channel` | 说明 |
| ---- | --------- | ---- |
| 微信公众号 | `wechat` | 默认渠道，扫码即可接收 |
| 企业微信 | `cp` | 需要填写应用编码 `Option` |
| 第三方 Webhook | `webhook` | 需要填写 Webhook 编码 `Option` |
| 电子邮件 | `mail` | 可使用官网默认邮件，或填写自定义邮件渠道编码 |
| 手机短信 | `sms` | 通过短信渠道推送 |
| 语音通知 | `voice` | 语音播报 |
| 浏览器扩展 | `extension` | 浏览器端接收 |
| App 推送 | `app` | PushPlus App 接收 |

### 3.3 渠道参数

不同渠道的必填项不同：

| 配置项 | 适用渠道 | 说明 |
| ------ | -------- | ---- |
| `Option` | `webhook`、`cp`、`mail` | webhook 填 Webhook 编码；cp 填企业微信应用编码；mail 可选填自定义邮件渠道编码 |
| `To` | `wechat`、`cp` | 好友令牌或企业微信用户 id，多人用逗号分隔 |
| `CallbackUrl` | 全部 | 发送结果回调地址，选填 |
| `Timestamp` | 全部 | 毫秒时间戳；服务器时间超过该值则不发送，用于控制消息时效 |

::: warning
`Topic` 与 `To` 不建议同时使用，`Topic` 优先级更高。需要精确推送给某个人时，请清空 `Topic`。
:::

## 4. 消息模板

模板在配置页以卡片形式选择，右侧提供实时预览。v2.7.0 起预览改为悬停浮层。

### 4.1 HTML

适合微信等富文本渠道，包含状态色块与信息表格。

```html
<div style="font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;
            font-size:14px;color:#24292f;max-width:720px;margin:0 auto;padding:20px;">
  <div style="background:#fff;border:1px solid #e1e4e8;border-radius:6px;overflow:hidden;">
    <div style="background:#fff5f5;border-bottom:1px solid #e1e4e8;padding:16px 20px;">
      <h1 style="margin:0;font-size:20px;color:#cf222e;">作业执行失败: order-sync</h1>
    </div>
    <table style="width:100%;border-collapse:collapse;">
      <tr><td style="padding:8px 20px;color:#57606a;">作业分组</td><td>DEFAULT</td></tr>
      <tr><td style="padding:8px 20px;color:#57606a;">执行耗时</td><td>1520 毫秒</td></tr>
      <tr><td style="padding:8px 20px;color:#57606a;">错误信息</td><td>接口返回 500</td></tr>
    </table>
  </div>
</div>
```

### 4.2 Markdown

适合技术人员阅读，结构与表格都清晰。

```markdown
# 作业执行失败: order-sync

| 项目 | 内容 |
| ---- | ---- |
| 作业分组 | DEFAULT |
| 执行状态 | 失败 |
| 执行时间 | 2026-11-17 10:00:00 |
| 执行耗时 | 1520 毫秒 |
| 错误信息 | 接口返回 500 |
```

### 4.3 TXT

兼容性最好，适合短信、语音等渠道的正文。

```text
作业执行失败
作业名称: order-sync
作业分组: DEFAULT
执行时间: 2026-11-17 10:00:00
执行耗时: 1520 毫秒
错误信息: 接口返回 500

此消息由 Chet.QuartzNet.UI 调度系统自动发送，请勿回复。
```

### 4.4 JSON

v2.7.0 新增，方便下游系统解析或接入自动化流程。

```json
{
  "eventType": "jobExecution",
  "jobName": "order-sync",
  "jobGroup": "DEFAULT",
  "status": "failed",
  "statusText": "失败",
  "executionTime": "2026-11-17 10:00:00",
  "duration": 1520,
  "message": "接口返回 500"
}
```

## 5. 通知策略

| 策略 | 默认值 | 建议 |
| ---- | ------ | ---- |
| 作业执行成功 | 关闭 | 关键链路可按需开启，避免通知噪声 |
| 作业执行失败 | 开启 | 保持开启 |
| 调度器异常 | 开启 | 保持开启 |

## 6. 测试与历史

### 6.1 发送测试通知

配置保存后点击「发送测试通知」。失败时优先检查：

1. Token 是否有效；
2. 服务器能否访问 PushPlus；
3. 当前渠道是否必填 `Option`（webhook / cp）；
4. 应用日志中的异常信息（测试通知不会写入通知历史）。

### 6.2 通知历史

「通知管理」页记录作业与调度器触发的每次发送（测试通知不记录）：

- 通知标题与内容；
- 触发来源（作业名或调度器）；
- 状态：待发送 / 发送成功 / 发送失败；
- 创建时间、发送时间与耗时；
- 支持按状态筛选、按来源搜索、单条删除与批量清空。

![通知历史](/images/notification.png)

## 7. 排错对照

| 现象 | 排查方向 |
| ---- | -------- |
| 测试通知失败 | Token、网络、渠道必填参数 |
| 作业失败但没有通知 | 通知总开关、失败策略、作业是否真正执行（看日志） |
| 收到通知但内容为空 | 模板选择与渠道是否支持该格式 |
| 群组收不到 | `Topic` 是否填写正确，或改用 `To` 指定接收人 |
| 重复收到通知 | 检查是否有多个实例都在调度同一作业 |

## 相关阅读

- [通知配置](/guide/notification)
- [通知管理](/modules/notification)
- [通知管理介绍](/articles/10-notification-management)
