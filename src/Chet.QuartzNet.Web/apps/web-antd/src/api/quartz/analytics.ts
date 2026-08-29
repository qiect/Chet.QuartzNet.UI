import { requestClient } from '../request';

import type { ApiResponse } from './job';

// 作业统计数据DTO
export interface JobStats {
  totalJobs: number;          // 总作业数
  enabledJobs: number;        // 启用的作业数
  disabledJobs: number;       // 禁用的作业数
  totalExecutions: number;    // 总执行数
  successCount: number;       // 成功的执行数
  failedCount: number;        // 失败的执行数
}

// 作业状态分布数据DTO
export interface JobStatusDistribution {
  status: string;             // 作业状态
  count: number;              // 数量
  percentage: number;         // 百分比
}

// 作业执行趋势数据DTO
export interface JobExecutionTrend {
  time: string;               // 时间点
  successCount: number;       // 成功执行次数
  failedCount: number;        // 失败执行次数
  totalCount: number;         // 总执行次数
}

// 统计查询DTO
export interface StatsQueryDto {
  /** 时间范围类型：today, yesterday, thisWeek, thisMonth, custom */
  timeRangeType?: string;
  /** 自定义开始时间 */
  startTime?: string;
  /** 自定义结束时间 */
  endTime?: string;
  /** 作业名称 */
  jobName?: string;
  /** 作业分组 */
  jobGroup?: string;
}

// 作业健康数据DTO
export interface JobHealth {
  jobName: string;
  jobGroup: string;
  status: string;
  isEnabled: boolean;
  successRate: number;
  avgDuration: number;
  maxDuration: number;
  executionCount: number;
  lastExecutionTime?: string;
  cronExpression?: string;
}

// 作业执行热力图数据DTO
export interface JobExecutionHeatmap {
  dayOfWeek: number;
  hour: number;
  count: number;
  successCount: number;
  failedCount: number;
}

// 耗时基线分析数据DTO
export interface TopSlowJob {
  jobName: string;
  jobGroup: string;
  avgDuration: number;
  maxDuration: number;
  minDuration: number;
  executionCount: number;
  successRate: number;
  lastExecutionTime?: string;
}

/**
 * 获取作业统计数据
 * @param query 查询参数
 * @returns 作业统计数据
 */
export async function getJobStats(query?: StatsQueryDto): Promise<ApiResponse<JobStats>> {
  const response = await requestClient.post('/api/quartz/GetJobStats', query);
  return response;
}

/**
 * 获取作业状态分布数据
 * @param query 查询参数
 * @returns 作业状态分布数据
 */
export async function getJobStatusDistribution(query?: StatsQueryDto): Promise<ApiResponse<JobStatusDistribution[]>> {
  const response = await requestClient.post('/api/quartz/GetJobStatusDistribution', query);
  return response;
}

/**
 * 获取作业执行趋势数据
 * @param query 查询参数
 * @returns 作业执行趋势数据
 */
export async function getJobExecutionTrend(query?: StatsQueryDto): Promise<ApiResponse<JobExecutionTrend[]>> {
  const response = await requestClient.post('/api/quartz/GetJobExecutionTrend', query);
  return response;
}

/**
 * 获取作业健康概览数据
 * @param query 查询参数
 * @returns 作业健康概览数据
 */
export async function getJobHealthOverview(query?: StatsQueryDto): Promise<ApiResponse<JobHealth[]>> {
  const response = await requestClient.post('/api/quartz/GetJobHealthOverview', query);
  return response;
}

/**
 * 获取作业执行热力图数据
 * @param query 查询参数
 * @returns 作业执行热力图数据
 */
export async function getJobExecutionHeatmap(query?: StatsQueryDto): Promise<ApiResponse<JobExecutionHeatmap[]>> {
  const response = await requestClient.post('/api/quartz/GetJobExecutionHeatmap', query);
  return response;
}

/**
 * 获取耗时基线分析数据
 * @param query 查询参数
 * @param topCount 获取数量，默认10条
 * @returns 耗时基线分析数据
 */
export async function getTopSlowJobs(query?: StatsQueryDto, topCount: number = 10): Promise<ApiResponse<TopSlowJob[]>> {
  const response = await requestClient.post(`/api/quartz/GetTopSlowJobs?topCount=${topCount}`, query);
  return response;
}