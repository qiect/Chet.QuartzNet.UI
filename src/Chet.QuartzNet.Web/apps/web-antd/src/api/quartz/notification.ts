import { requestClient } from '../request';

// 通知状态枚举
export enum NotificationStatusEnum {
  Pending = 0,
  Sent = 1,
  Failed = 2,
}

// 通知策略DTO
export interface NotificationStrategyDto {
  notifyOnJobSuccess: boolean;
  notifyOnJobFailure: boolean;
  notifyOnSchedulerError: boolean;
}

// PushPlus配置DTO
export interface PushPlusConfigDto {
  token: string;
  channel: string;
  template: string;
  topic: string;
  /** 渠道配置参数（webhook/cp必填，mail可选） */
  option: string;
  /** 接收人（好友令牌或企业微信用户id，多人逗号隔开） */
  to: string;
  /** 发送结果回调地址 */
  callbackUrl: string;
  /** 毫秒时间戳，服务器时间大于此值则不发送 */
  timestamp?: number;
  enable: boolean;
  strategy: NotificationStrategyDto;
}

// 通知消息DTO
export interface QuartzNotificationDto {
  notificationId: string;
  title: string;
  content: string;
  status: NotificationStatusEnum;
  errorMessage?: string;
  triggeredBy?: string;
  createTime: string;
  sendTime?: string;
  duration?: number;
}

// 通知查询DTO
export interface NotificationQueryDto {
  pageIndex: number;
  pageSize: number;
  sortBy?: string;
  sortOrder?: string;
  status?: NotificationStatusEnum;
  triggeredBy?: string;
  startTime?: string;
  endTime?: string;
}

// 分页响应DTO
export interface PagedResponseDto<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
}

// API响应DTO
export interface ApiResponse<T> {
  /** 是否成功 */
  success: boolean;
  /** 消息 */
  message: string;
  /** 数据 */
  data?: T;
  /** 错误码 */
  errorCode?: string;
}

// 获取PushPlus配置
export const getPushPlusConfig = async (): Promise<PushPlusConfigDto> => {
  const response = await requestClient.get('/api/quartz/GetPushPlusConfig');
  return response;
};

// 保存PushPlus配置
export const savePushPlusConfig = async (config: PushPlusConfigDto): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post('/api/quartz/SavePushPlusConfig', config);
  return response;
};

// 发送测试通知
export const sendTestNotification = async (): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post('/api/quartz/SendTestNotification');
  return response;
};

// 获取通知消息列表
export const getNotifications = async (queryDto: NotificationQueryDto): Promise<ApiResponse<PagedResponseDto<QuartzNotificationDto>>> => {
  const response = await requestClient.post('/api/quartz/GetNotifications', queryDto);
  return response;
};

// 获取通知消息详情
export const getNotification = async (id: string): Promise<ApiResponse<QuartzNotificationDto>> => {
  const response = await requestClient.get('/api/quartz/GetNotification', {
    params: { notificationId: id }
  });
  return response;
};

// 删除通知消息
export const deleteNotification = async (id: string): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.delete('/api/quartz/DeleteNotification', {
    params: { notificationId: id }
  });
  return response;
};

// 清空通知消息
export const clearNotifications = async (queryDto: NotificationQueryDto): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post('/api/quartz/ClearNotifications', queryDto);
  return response;
};
