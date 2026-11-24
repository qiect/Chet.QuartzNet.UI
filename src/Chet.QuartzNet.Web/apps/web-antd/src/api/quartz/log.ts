import { requestClient } from '../request';

import type { ApiResponse } from './job';

// 分页响应类型
export interface PageResponse<T> {
  /** 数据列表 */
  items: T[];
  /** 总记录数 */
  totalCount: number;
  /** 页码 */
  pageIndex: number;
  /** 每页大小 */
  pageSize: number;
  /** 总页数 */
  totalPages: number;
}

// 日志状态枚举
export enum LogStatusEnum {
  RUNNING = 0,
  SUCCESS = 1,
  ERROR = 2,
  SKIPPED = 3,
  INTERRUPTED = 4,
}

// 触发来源
export type TriggerType = 'Cron' | 'Manual' | 'Recovery' | 'Unknown';

// 补偿状态
export type CompensationStatus =
  | 'None'
  | 'Pending'
  | 'Triggered'
  | 'Skipped'
  | 'Failed';

// 日志查询参数
export interface LogQueryParams {
  /** 页码 */
  pageIndex?: number;
  /** 每页大小 */
  pageSize?: number;
  /** 作业ID */
  jobId?: string;
  /** 作业名称 */
  jobName?: string;
  /** 作业分组 */
  jobGroup?: string;
  /** 日志状态 */
  status?: LogStatusEnum;
  /** 日志状态集合（多状态筛选） */
  statuses?: LogStatusEnum[];
  /** 开始时间 */
  startTime?: string;
  /** 结束时间 */
  endTime?: string;
  /** 开始时间范围查询-下限（针对 StartTime 字段） */
  startStartTime?: string;
  /** 开始时间范围查询-上限（针对 StartTime 字段） */
  endStartTime?: string;
  /** 结束时间范围查询-下限（针对 EndTime 字段） */
  startEndTime?: string;
  /** 结束时间范围查询-上限（针对 EndTime 字段） */
  endEndTime?: string;
  /** 排序字段 */
  sortBy?: string;
  /** 排序方向 */
  sortOrder?: string;
}

// 日志响应DTO
export interface LogResponseDto {
  /** 日志ID */
  logId: string;
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 触发器名称 */
  triggerName?: string;
  /** 触发器分组 */
  triggerGroup?: string;
  /** 开始时间 */
  startTime: string;
  /** 结束时间 */
  endTime?: string | null;
  /** 执行时长(毫秒) */
  duration?: number | null;
  /** 执行消息 */
  message?: string | null;
  /** 异常信息 */
  exception?: string | null;
  /** 错误信息 */
  errorMessage?: string | null;
  /** 异常栈跟踪 */
  errorStackTrace?: string | null;
  /** 执行结果 */
  result?: any | null;
  /** 作业数据 */
  jobData?: any | null;
  /** 日志状态 */
  status: LogStatusEnum;
  /** 创建时间 */
  createTime?: string;
  /** 触发时间 - 根据实际API返回添加 */
  fireTime?: string;
  /** 触发来源 */
  triggerType?: TriggerType;
  /** 操作人 */
  triggeredBy?: string | null;
  /** 补偿来源日志ID */
  recoverySourceLogId?: string | null;
  /** 补偿来源执行开始时间（前端按需补齐） */
  recoverySourceStartTime?: string | null;
  /** 第几次尝试 */
  recoveryAttempt?: number;
  /** 最后心跳时间 */
  heartbeatTime?: string | null;
  /** 判定中断时间 */
  interruptedAt?: string | null;
  /** 补偿状态 */
  compensationStatus?: CompensationStatus;
  /** 跳过补偿原因 */
  skipReason?: string | null;
}

// 后端枚举在 STJ 宿主下是字符串（"Cron"/"Triggered"），在 Newtonsoft 宿主下会退化成数字（2），
// 统一在接口边界归一化，页面里的字符串比较与文案映射才不会静默失效
const TRIGGER_TYPE_VALUES: TriggerType[] = [
  'Cron',
  'Manual',
  'Recovery',
  'Unknown',
];
const COMPENSATION_STATUS_VALUES: CompensationStatus[] = [
  'None',
  'Pending',
  'Triggered',
  'Skipped',
  'Failed',
];

/** 归一化触发来源：数字按枚举顺序还原，字符串忽略大小写，未知值回落 Unknown */
export function normalizeTriggerType(value: unknown): TriggerType {
  if (typeof value === 'number') {
    return TRIGGER_TYPE_VALUES[value] ?? 'Unknown';
  }
  const key = String(value ?? '').trim().toLowerCase();
  return TRIGGER_TYPE_VALUES.find((item) => item.toLowerCase() === key) ?? 'Unknown';
}

/** 归一化补偿状态：数字按枚举顺序还原，字符串忽略大小写，未知值回落 None */
export function normalizeCompensationStatus(
  value: unknown,
): CompensationStatus {
  if (typeof value === 'number') {
    return COMPENSATION_STATUS_VALUES[value] ?? 'None';
  }
  const key = String(value ?? '').trim().toLowerCase();
  return (
    COMPENSATION_STATUS_VALUES.find((item) => item.toLowerCase() === key) ??
    'None'
  );
}

// 缺失（null/undefined）保持原样，避免把「没有触发来源」错标成 Cron
function normalizeLogDto(log: LogResponseDto): LogResponseDto {
  const raw = log as unknown as {
    compensationStatus?: unknown;
    triggerType?: unknown;
  };
  const normalized: LogResponseDto = { ...log };

  if (raw.triggerType !== null && raw.triggerType !== undefined) {
    normalized.triggerType = normalizeTriggerType(raw.triggerType);
  }
  if (raw.compensationStatus !== null && raw.compensationStatus !== undefined) {
    normalized.compensationStatus = normalizeCompensationStatus(
      raw.compensationStatus,
    );
  }

  return normalized;
}

/**
 * 获取日志列表
 * @param params 查询参数
 * @returns 日志列表分页数据
 */
export async function getLogList(
  params: LogQueryParams
): Promise<ApiResponse<PageResponse<LogResponseDto>>> {
  const response = await requestClient.post<
    ApiResponse<PageResponse<LogResponseDto>>
  >('/api/quartz/GetJobLogs', params);

  if (response?.data && Array.isArray(response.data.items)) {
    response.data = {
      ...response.data,
      items: response.data.items.map((item) => normalizeLogDto(item)),
    };
  }

  return response;
}

/**
 * 获取单条日志详情
 * @param logId 日志ID
 */
export async function getJobLog(
  logId: string,
): Promise<ApiResponse<LogResponseDto>> {
  const response = await requestClient.get<ApiResponse<LogResponseDto>>(
    '/api/quartz/GetJobLog',
    { params: { logId } },
  );

  if (response?.data) {
    response.data = normalizeLogDto(response.data);
  }

  return response;
}

/**
 * 手动重新执行一条中断日志
 * @param logId 中断日志ID
 */
export async function rerunInterruptedLog(
  logId: string,
): Promise<ApiResponse<boolean>> {
  return requestClient.post('/api/quartz/RerunInterruptedLog', { logId });
}

/**
 * 手动中断一条运行中的作业
 * @param logId 运行中日志ID
 */
export async function interruptRunningLog(
  logId: string,
): Promise<ApiResponse<boolean>> {
  return requestClient.post('/api/quartz/InterruptJob', { logId });
}

/**
 * 手动中断在 SkipReason 中的固定前缀。
 * 手动中断与不可预期中断共用「中断」状态，仅靠此前缀在展示层区分，不能用于查询过滤。
 */
export const MANUAL_INTERRUPT_PREFIX = '【手动中断】';

/** 是否为用户手动中断：状态为中断，且跳过原因带手动中断前缀 */
export const isManualInterrupt = (log: LogResponseDto) =>
  log.status === LogStatusEnum.INTERRUPTED &&
  !!log.skipReason?.startsWith(MANUAL_INTERRUPT_PREFIX);

/** 展示用：裁掉手动中断前缀 */
export const manualInterruptText = (reason?: string | null) =>
  reason?.startsWith(MANUAL_INTERRUPT_PREFIX)
    ? reason.slice(MANUAL_INTERRUPT_PREFIX.length)
    : (reason ?? '');

/**
 * 获取日志详情
 * 注：此接口在后端未实现，暂时使用模拟数据
 * @param logId 日志ID
 * @returns 日志详情
 */
export async function getLogDetail(logId: string): Promise<LogResponseDto> {
  // 暂时返回模拟数据
  console.warn('后端未提供日志详情接口');
  return {
    logId: logId,
    jobName: '模拟作业',
    jobGroup: 'DEFAULT',
    triggerName: 'trigger-' + logId,
    triggerGroup: 'DEFAULT',
    fireTime: new Date().toISOString(),
    startTime: new Date().toISOString(),
    endTime: new Date().toISOString(),
    duration: 1000,
    status: LogStatusEnum.SUCCESS,
    createTime: new Date().toISOString(),
  };
}



/**
 * 清除日志
 * @param params 查询参数
 * @returns 清除结果
 */
export async function clearLogs(params: any = {}): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/ClearLogs', params);
  return response;
}

/**
 * 获取日志统计信息
 * 注：此接口在后端未实现，暂时返回模拟数据
 * @param params 查询参数
 * @returns 统计信息
 */
export async function getLogStatistics(_params?: { startTime?: string; endTime?: string }): Promise<{
  totalLogs: number;
  successCount: number;
  failureCount: number;
  runningCount: number;
  cancelledCount: number;
}> {
  // 暂时返回模拟数据
  console.warn('后端未提供日志统计接口，返回模拟数据');
  return {
    totalLogs: 0,
    successCount: 0,
    failureCount: 0,
    runningCount: 0,
    cancelledCount: 0,
  };
}