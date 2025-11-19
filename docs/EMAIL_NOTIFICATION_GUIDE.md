# QuartzNet.UI 邮件通知功能使用指南

## 功能概述

QuartzNet.UI 提供了完整的邮件通知功能，可以在作业执行完成或调度器发生异常时自动发送邮件通知。该功能支持自定义邮件模板，确保通知信息的清晰和专业性。

## 功能特性

### 1. 作业执行通知
- **触发时机**：作业执行完成后自动发送
- **通知内容**：作业名称、作业分组、执行状态、执行时间、耗时、结果消息
- **支持状态**：成功、失败、异常

### 2. 调度器异常通知
- **触发时机**：调度器启动、暂停、恢复或发生异常时
- **通知内容**：异常时间、异常类型、异常消息、堆栈跟踪

### 3. 邮件模板定制
- **统一样式**：所有邮件采用统一的表格布局
- **固定宽度**：字段名列宽度统一为120px，确保视觉一致性
- **响应式设计**：适配各种邮件客户端

## 配置方法

### 1. 邮件服务配置
在 `appsettings.json` 中配置邮件服务：

```json
{
    "EmailOptions": {
      "Enabled": true,
      "SmtpServer": "smtp.163.com",
      "SmtpPort": 25,
      "SenderEmail": "qct154878690@163.com",
      "SenderName": "Chet.QuartzNET.UI 监控",
      "SenderPassword": "NZu2MTKbnTrMMfx4",
      "EnableSsl": false,
      "SubjectPrefix": "[Chet.QuartzNET.UI]",
      "Recipients": "154878690@qq.com,qiechangtang@zy-cast.com",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": false,
      "NotifyOnSchedulerError": true
    }
}
{
  "EmailSettings": {
    "SmtpServer": "smtp.example.com",
    "SmtpPort": 587,
    "Username": "your-email@example.com",
    "Password": "your-password",
    "EnableSsl": true,
    "FromEmail": "noreply@example.com",
    "FromName": "QuartzNet Scheduler"
  }
}
```

### 2. 通知收件人配置
```json
{
  "QuartzUI": {
    "NotificationEmails": [
      "admin@example.com",
      "dev-team@example.com"
    ]
  }
}
```

## 邮件模板结构

### 作业执行通知邮件模板

邮件包含以下字段信息：
- **作业名称**：作业的唯一标识
- **作业分组**：作业所属的分组
- **执行状态**：运行结果（成功/失败/异常）
- **执行时间**：作业开始执行的时间
- **耗时**：作业执行的总耗时
- **结果消息**：执行的详细结果信息

### 调度器异常通知邮件模板

邮件包含以下字段信息：
- **异常时间**：异常发生的具体时间
- **异常类型**：异常的类型分类
- **异常消息**：异常的简要描述
- **详细信息**：异常的完整堆栈跟踪

## 使用示例

### 启用邮件通知

1. **配置邮件服务**：在配置文件中设置SMTP服务器信息
2. **设置收件人**：添加需要接收通知的邮件地址
3. **重启应用**：使配置生效

### 查看邮件通知

当作业执行完成或调度器发生异常时，系统会自动发送邮件到配置的收件人地址。邮件内容包含完整的执行信息和相关详情。

## 注意事项

1. **SMTP配置**：确保SMTP服务器配置正确，特别是端口和SSL设置
2. **邮件地址**：验证所有通知邮件地址的有效性
3. **网络连接**：确保应用服务器能够访问SMTP服务器
4. **频率控制**：大量作业同时执行可能会产生较多邮件，建议合理配置通知策略

## 故障排查

### 邮件发送失败
- 检查SMTP服务器配置是否正确
- 验证网络连接和防火墙设置
- 查看应用日志中的邮件发送错误信息

### 邮件内容异常
- 检查邮件模板是否正确加载
- 验证作业信息是否完整
- 查看调度器日志中的异常信息
