# QuartzNet.UI 邮件通知功能使用指南（已过时）

## 功能概述

QuartzNet.UI 提供了完整的邮件通知功能，可以在作业执行完成或调度器发生异常时自动发送邮件通知。该功能支持自定义配置，确保通知信息的清晰和专业性。

## 功能特性

### 1. 作业执行通知
- **触发时机**：作业执行完成后自动发送
- **通知内容**：作业名称、作业分组、执行状态、执行时间、耗时、结果消息
- **支持状态**：成功、失败、异常

### 2. 调度器异常通知
- **触发时机**：调度器发生异常时
- **通知内容**：异常时间、异常类型、异常消息、堆栈跟踪

### 3. 灵活的通知策略
- 可配置作业成功时是否发送通知
- 可配置作业失败时是否发送通知
- 可配置调度器异常时是否发送通知
- 支持分号或逗号分隔的多个收件人

## 配置方法

### 1. 完整配置示例

在 `appsettings.json` 中配置邮件通知功能：

```json
{
  "QuartzUI": {
    "EmailOptions": {
      "Enabled": true,
      "SmtpServer": "smtp.example.com",
      "SmtpPort": 587,
      "EnableSsl": true,
      "SenderEmail": "sender@example.com",
      "SenderName": "Quartz.NET 调度器",
      "SenderPassword": "your-password-or-app-key",
      "Recipients": "admin@example.com; team@example.com",
      "SubjectPrefix": "[Quartz.NET] ",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": false,
      "NotifyOnSchedulerError": true
    }
  }
}
```

### 2. 配置字段说明

| 字段名 | 类型 | 默认值 | 描述 |
|--------|------|--------|------|
| `Enabled` | `bool` | `false` | 是否启用邮件通知功能 |
| `SmtpServer` | `string` | `""` | SMTP服务器地址（必填） |
| `SmtpPort` | `int` | `587` | SMTP服务器端口（常用端口：25、465、587） |
| `EnableSsl` | `bool` | `true` | 是否启用SSL/TLS加密连接 |
| `SenderEmail` | `string` | `""` | 发件人邮箱地址（必填） |
| `SenderName` | `string` | `"Quartz.NET 调度器"` | 发件人显示名称 |
| `SenderPassword` | `string` | `""` | 发件人邮箱密码或授权码（必填） |
| `Recipients` | `string` | `""` | 收件人邮箱列表，用分号(;)或逗号(,)分隔（必填） |
| `SubjectPrefix` | `string` | `"[Quartz.NET]"` | 邮件主题前缀，便于识别和过滤 |
| `NotifyOnFailure` | `bool` | `true` | 作业执行失败时是否发送通知 |
| `NotifyOnSuccess` | `bool` | `false` | 作业执行成功时是否发送通知 |
| `NotifyOnSchedulerError` | `bool` | `true` | 调度器发生异常时是否发送通知 |

### 3. 配置说明

#### 3.1 基础配置

**启用邮件通知**：
```json
{
  "QuartzUI": {
    "EmailOptions": {
      "Enabled": true
    }
  }
}
```

**SMTP服务器配置**：
```json
{
  "QuartzUI": {
    "EmailOptions": {
      "SmtpServer": "smtp.example.com",
      "SmtpPort": 587,
      "EnableSsl": true
    }
  }
}
```

**发件人配置**：
```json
{
  "QuartzUI": {
    "EmailOptions": {
      "SenderEmail": "sender@example.com",
      "SenderName": "My Scheduler",
      "SenderPassword": "your-password"
    }
  }
}
```

**收件人配置**：
```json
{
  "QuartzUI": {
    "EmailOptions": {
      "Recipients": "admin@example.com; team@example.com"
    }
  }
}
```

**通知策略配置**：
```json
{
  "QuartzUI": {
    "EmailOptions": {
      "NotifyOnFailure": true,
      "NotifyOnSuccess": true,
      "NotifyOnSchedulerError": true
    }
  }
}
```

## 邮件内容结构

### 1. 作业执行通知邮件

作业执行完成后发送的邮件包含以下信息：

- **作业名称**：作业的唯一标识
- **作业分组**：作业所属的分组
- **执行状态**：运行结果（成功/失败）
- **执行时间**：作业开始执行的时间
- **耗时**：作业执行的总耗时
- **结果消息**：执行的详细结果信息（成功消息或错误信息）

### 2. 调度器异常通知邮件

调度器发生异常时发送的邮件包含以下信息：

- **异常时间**：异常发生的具体时间
- **异常类型**：异常的类型
- **异常消息**：异常的简要描述
- **详细信息**：异常的完整堆栈跟踪

## 使用示例

### 1. 启用邮件通知功能

在 `appsettings.json` 中配置完整的邮件通知信息：

```json
{
  "QuartzUI": {
    "EmailOptions": {
      "Enabled": true,
      "SmtpServer": "smtp.163.com",
      "SmtpPort": 465,
      "EnableSsl": true,
      "SenderEmail": "your-email@163.com",
      "SenderName": "Quartz 调度器",
      "SenderPassword": "your-app-password",
      "Recipients": "admin@example.com; dev@example.com",
      "SubjectPrefix": "[调度系统] ",
      "NotifyOnFailure": true,
      "NotifyOnSuccess": false,
      "NotifyOnSchedulerError": true
    }
  }
}
```

### 2. 在程序中动态配置

在 `Program.cs` 中动态配置邮件通知：

```csharp
// 获取配置节
var emailConfig = builder.Configuration.GetSection("QuartzUI:EmailOptions");

// 添加 Quartz UI 服务
builder.Services.AddQuartzUISqlServer(connectionString, options => {
    // 绑定邮件配置
    emailConfig.Bind(options.EmailOptions);
});
```

### 3. 测试邮件配置

通过 API 测试邮件配置是否正确：

```bash
POST /api/quartz/TestEmailConfiguration
```

该 API 会发送一封测试邮件到配置的收件人邮箱，验证邮件服务配置是否正确。

## 注意事项

1. **SMTP 配置**：
   - 确保 SMTP 服务器地址和端口配置正确
   - 验证 SSL/TLS 设置是否与服务器要求一致
   - 注意不同邮箱服务商的 SMTP 服务器和端口可能不同

2. **密码安全**：
   - 强烈建议使用邮箱的授权码而不是登录密码
   - 避免在配置文件中明文存储密码（生产环境建议使用密钥管理服务）

3. **网络连接**：
   - 确保应用服务器能够访问配置的 SMTP 服务器
   - 检查防火墙设置是否允许应用服务器访问 SMTP 端口

4. **通知频率**：
   - 大量作业同时执行可能会产生较多邮件，建议合理配置 `NotifyOnSuccess`
   - 对于高频执行的作业，考虑只配置失败通知

## 故障排查

### 邮件发送失败

1. **检查配置**：
   - 验证 `SmtpServer`、`SmtpPort`、`EnableSsl` 配置是否正确
   - 检查发件人邮箱和密码是否正确
   - 确认收件人列表非空且格式正确

2. **查看日志**：
   - 检查应用日志中的邮件发送相关错误信息
   - 日志会包含 SMTP 服务器的响应和具体错误原因

3. **网络问题**：
   - 验证应用服务器是否可以连接到 SMTP 服务器
   - 尝试使用 telnet 测试 SMTP 服务器连接

### 通知未触发

1. **检查启用状态**：
   - 确认 `Enabled` 配置为 `true`
   - 验证 `NotifyOnFailure`、`NotifyOnSuccess` 或 `NotifyOnSchedulerError` 配置是否正确

2. **作业执行状态**：
   - 检查作业的执行状态是否符合通知触发条件
   - 查看作业执行日志确认执行结果

3. **配置顺序**：
   - 确保邮件配置在 Quartz UI 服务配置之前加载
   - 确认配置文件的结构和字段名是否正确

## 最佳实践

1. **分环境配置**：
   - 开发环境：只配置开发人员邮箱，启用成功通知
   - 生产环境：配置运维和管理人员邮箱，只启用失败和异常通知

2. **通知策略**：
   - 对于重要作业，同时启用成功和失败通知
   - 对于常规作业，只启用失败通知
   - 对于高频作业（每分钟执行），考虑关闭通知或使用批量通知

3. **邮件主题**：
   - 使用清晰的主题前缀，便于邮件过滤和识别
   - 在主题中包含关键信息（如作业名称、执行状态）

4. **定期测试**：
   - 定期测试邮件配置，确保通知功能正常工作
   - 当邮件服务器配置变更时，及时更新配置并测试

## 更新日志

- v1.0.0：初始版本，支持作业执行和调度器异常通知
- v1.1.0：增加邮件配置测试功能，优化通知内容格式
- v1.2.0：支持分号和逗号分隔的多个收件人