import { requestClient } from '../request';

import type { ApiResponse } from './job';

/**
 * 验证Cron表达式
 * @param cronExpression Cron表达式
 * @returns 验证结果
 */
export async function validateCronExpression(cronExpression: string): Promise<ApiResponse<boolean>> {
  const response = await requestClient.get('/api/quartz/ValidateCronExpression', {
    params: { cronExpression }
  });
  return response;
}

/**
 * 获取Cron表达式未来N次执行时间
 * @param cronExpression Cron表达式
 * @param count 获取次数，默认10次
 * @returns 执行时间列表
 */
export async function getNextRunTimes(cronExpression: string, count: number = 10): Promise<ApiResponse<string[]>> {
  const response = await requestClient.get('/api/quartz/GetNextRunTimes', {
    params: { cronExpression, count }
  });
  return response;
}

/**
 * 获取所有实现了IJob接口的类名列表
 * @returns 作业类列表
 */
export async function getJobClasses(): Promise<ApiResponse<string[]>> {
  const response = await requestClient.get('/api/quartz/GetJobClasses');
  return response;
}