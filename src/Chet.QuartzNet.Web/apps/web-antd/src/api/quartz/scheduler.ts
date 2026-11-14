import { requestClient } from '../request';

import type { ApiResponse } from './job';

// 调度器状态
export interface SchedulerStatusDto {
  schedulerName: string;
  schedulerInstanceId: string;
  /** 当前进程运行实例标识（机器名:进程号:启动短标识） */
  runtimeInstanceId?: string;
  isStarted: boolean;
  isShutdown: boolean;
  inStandbyMode: boolean;
  status: string;
  jobCount: number;
  executingJobCount: number;
  threadPoolSize: number;
  version: string;
  startTime?: string;
  runningTime: number;
  /** 是否正在排水（含收尾关闭调度器阶段） */
  isDraining?: boolean;
  /** 是否处于排水收尾（正在关闭调度器，不可取消） */
  isStopping?: boolean;
  /** 当前生效的停机策略（启动期配置，重启后生效） */
  shutdownMode?: 'Drain' | 'Immediate' | 'Wait';
  /** 排水开始时间 */
  drainStartedAt?: string;
  /** 排水截止时间 */
  drainDeadline?: string;
  /** 排水仍需等待的执行中任务数 */
  remainingExecutingCount?: number;
  /** 本实例恢复队列中未完成的补偿数 */
  pendingCompensationCount?: number;
}

// 单条恢复明细
export interface RecoveryDetailDto {
  logId: string;
  jobName: string;
  jobGroup: string;
  attempt: number;
  outcome: 'Pending' | 'Rerun' | 'Skipped' | 'Failed';
  reason?: string | null;
}

// 本次启动恢复汇总
export interface RecoverySummaryDto {
  bootId: string;
  instanceId: string;
  bootTime: string;
  startedAt?: string | null;
  completedAt?: string | null;
  isRunning: boolean;
  interruptedCount: number;
  rerunCount: number;
  skippedCount: number;
  failedCount: number;
  pendingCount: number;
  truncated: boolean;
  details: RecoveryDetailDto[];
}

/**
 * 获取调度器状态
 */
export async function getSchedulerStatus(): Promise<
  ApiResponse<SchedulerStatusDto>
> {
  const response = await requestClient.get('/api/quartz/GetSchedulerStatus');
  return response;
}

/**
 * 启动调度器
 */
export async function startScheduler(): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/StartScheduler');
  return response;
}

/**
 * 停止调度器
 */
export async function stopScheduler(): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/StopScheduler');
  return response;
}

/**
 * 排水：停止接收新触发，等待执行中任务收尾后自动停止
 */
export async function drainScheduler(
  timeoutSeconds?: number,
): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/DrainScheduler', {
    timeoutSeconds,
  });
  return response;
}

/**
 * 取消排水并恢复调度
 */
export async function resumeScheduler(): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/ResumeScheduler');
  return response;
}

/**
 * 获取本次启动的中断恢复汇总
 */
export async function getRecoverySummary(): Promise<
  ApiResponse<RecoverySummaryDto>
> {
  const response = await requestClient.get('/api/quartz/GetRecoverySummary');
  return response;
}
