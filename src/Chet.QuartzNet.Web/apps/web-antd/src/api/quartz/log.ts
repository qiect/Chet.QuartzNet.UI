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
}

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
}

/**
 * 获取日志列表
 * @param params 查询参数
 * @returns 日志列表分页数据
 */
export async function getLogList(
  params: LogQueryParams
): Promise<ApiResponse<PageResponse<LogResponseDto>>> {
  return requestClient.post('/api/quartz/GetJobLogs', params);
}

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
export async function getLogStatistics(params?: { startTime?: string; endTime?: string }): Promise<{
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