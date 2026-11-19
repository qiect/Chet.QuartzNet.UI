using Chet.QuartzNet.Models.Entities;

namespace Chet.QuartzNet.Models.DTOs;

/// <summary>
/// PushPlus配置DTO
/// </summary>
public class PushPlusConfigDto
{
    /// <summary>
    /// PushPlus Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 推送渠道
    /// 可选值：wechat, webhook, cp, mail, sms, voice, extension, app, clawbot
    /// </summary>
    public string Channel { get; set; } = "wechat";

    /// <summary>
    /// 消息模板
    /// 可选值：html, txt, json, markdown, cloudMonitor
    /// </summary>
    public string Template { get; set; } = "html";

    /// <summary>
    /// 主题
    /// 用于订阅推送（群组编码），不填仅发送给自己；channel为webhook时无效
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// 渠道配置参数
    /// webhook渠道：必填，填入webhook编码
    /// cp渠道：必填，填入企业微信应用编码
    /// mail渠道：可选，填入自定义邮件渠道编码，不填则使用官网默认邮件发送
    /// 其他渠道：不需要
    /// </summary>
    public string Option { get; set; } = string.Empty;

    /// <summary>
    /// 接收人
    /// 微信公众号渠道：填好友令牌实现好友消息推送，多人用逗号隔开
    /// 企业微信渠道：填企业微信用户id，多人用逗号隔开
    /// 注意：to和topic不能同时使用，topic优先级更高
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 发送结果回调地址
    /// 接口改为异步后，可通过此地址接收发送结果回调
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// 毫秒时间戳
    /// 服务器时间戳大于此值则不发送，用于控制消息时效性
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 通知策略
    /// </summary>
    public NotificationStrategyDto Strategy { get; set; } = new();
}
