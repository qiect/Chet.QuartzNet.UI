import { requestClient } from '../request';

import type { ApiResponse } from './job';

/**
 * 获取调度器状态
 * @returns 调度器状态
 */
export async function getSchedulerStatus(): Promise<ApiResponse<any>> {
  const response = await requestClient.get('/api/quartz/GetSchedulerStatus');
  return response;
}

/**
 * 启动调度器
 * @returns 启动结果
 */
export async function startScheduler(): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/StartScheduler');
  return response;
}

/**
 * 停止调度器
 * @returns 停止结果
 */
export async function stopScheduler(): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/StopScheduler');
  return response;
}