import type { Recordable } from '@vben/types';
import { requestClient } from '../request';

// 分页响应类型
export interface PageResponse<T> {
  /** 数据列表 */
  data: T[];
  /** 总记录数 */
  total: number;
  /** 页码 */
  pageNum: number;
  /** 每页大小 */
  pageSize: number;
  /** 总页数 */
  totalPages: number;
}

// 日志状态枚举
export enum LogStatusEnum {
  SUCCESS = 0,
  ERROR = 1,
  RUNNING = 2
}

// 日志请求参数
export interface LogQueryParams {
  /** 页码 */
  pageNum?: number;
  /** 每页大小 */
  pageSize?: number;
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
}

// 日志响应DTO
export interface LogResponseDto {
  /** 日志ID */
  logId: string;
  /** 作业ID */
  jobId: string;
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 触发器名称 */
  triggerName: string;
  /** 触发器分组 */
  triggerGroup: string;
  /** 触发时间 */
  fireTime: string;
  /** 开始时间 */
  startTime: string;
  /** 结束时间 */
  endTime: string;
  /** 执行时长(毫秒) */
  executionTime: number;
  /** 日志状态 */
  status: LogStatusEnum;
  /** 异常信息 */
  exceptionMessage?: string;
  /** 创建时间 */
  createTime: string;
}

/**
 * 获取日志列表
 * @param params 查询参数
 * @returns 日志列表分页数据
 */
export async function getLogList(params: LogQueryParams): Promise<PageResponse<LogResponseDto>> {
  return requestClient.get('/quartz/log/list', { params });
}

/**
 * 获取日志详情
 * @param logId 日志ID
 * @returns 日志详情
 */
export async function getLogDetail(logId: string): Promise<LogResponseDto> {
  return requestClient.get(`/quartz/log/${logId}`);
}

/**
 * 导出日志
 * @param params 查询参数
 * @returns 导出结果
 */
export async function exportLogList(params: LogQueryParams): Promise<Blob> {
  return requestClient.get('/quartz/log/export', { 
    params, 
    responseType: 'blob' 
  });
}

/**
 * 清除日志
 * @param days 保留最近几天的日志，0表示全部清除
 * @returns 清除结果
 */
export async function clearLogs(days: number = 0): Promise<boolean> {
  return requestClient.delete('/quartz/log/clear', { params: { days } });
}

/**
 * 获取日志统计信息
 * @param params 查询参数
 * @returns 统计信息
 */
export async function getLogStatistics(params?: {startTime?: string, endTime?: string}): Promise<{
  totalCount: number;
  successCount: number;
  errorCount: number;
  avgExecutionTime: number;
}> {
  return requestClient.get('/quartz/log/statistics', { params });
}
