import { requestClient } from '../request';

import type { ApiResponse } from './job';

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

// 作业性能查询DTO（扩展StatsQueryDto，增加TopCount参数）
export interface JobPerformanceQueryDto extends StatsQueryDto {
  /** 耗时排行取前N条，默认10 */
  topCount?: number;
}

// 作业统计数据DTO
export interface JobStats {
  /** 总作业数 */
  totalJobs: number;
  /** 启用的作业数 */
  enabledJobs: number;
  /** 禁用的作业数 */
  disabledJobs: number;
  /** 总执行数 */
  totalExecutions: number;
  /** 成功的执行数 */
  successCount: number;
  /** 失败的执行数 */
  failedCount: number;
}

// 作业状态分布数据DTO
export interface JobStatusDistribution {
  /** 作业状态 */
  status: string;
  /** 数量 */
  count: number;
  /** 百分比 */
  percentage: number;
}

// 作业执行趋势数据DTO
export interface JobExecutionTrend {
  /** 时间点 */
  time: string;
  /** 成功执行次数 */
  successCount: number;
  /** 失败执行次数 */
  failedCount: number;
  /** 总执行次数 */
  totalCount: number;
}

// 作业执行热力图数据DTO
export interface JobExecutionHeatmap {
  /** 星期几（1=周一...7=周日） */
  dayOfWeek: number;
  /** 小时（0-23） */
  hour: number;
  /** 执行次数 */
  count: number;
  /** 成功次数 */
  successCount: number;
  /** 失败次数 */
  failedCount: number;
}

// 统计分析概览聚合DTO（合并作业统计+状态分布+执行趋势+热力图）
export interface AnalyticsOverview {
  /** 作业统计概览 */
  stats: JobStats;
  /** 作业状态分布 */
  statusDistribution: JobStatusDistribution[];
  /** 执行趋势数据 */
  executionTrend: JobExecutionTrend[];
  /** 执行热力图数据 */
  executionHeatmap: JobExecutionHeatmap[];
}

// 作业健康数据DTO
export interface JobHealth {
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 作业状态 */
  status: string;
  /** 是否启用 */
  isEnabled: boolean;
  /** 成功率（0-100） */
  successRate: number;
  /** 平均执行耗时（毫秒） */
  avgDuration: number;
  /** 最大执行耗时（毫秒） */
  maxDuration: number;
  /** 执行次数 */
  executionCount: number;
  /** 最近执行时间 */
  lastExecutionTime?: string;
  /** Cron表达式 */
  cronExpression?: string;
}

// 耗时基线分析数据DTO
export interface TopSlowJob {
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 平均执行耗时（毫秒） */
  avgDuration: number;
  /** 最大执行耗时（毫秒） */
  maxDuration: number;
  /** 最小执行耗时（毫秒） */
  minDuration: number;
  /** 执行次数 */
  executionCount: number;
  /** 成功率（0-100） */
  successRate: number;
  /** 最近执行时间 */
  lastExecutionTime?: string;
}

// 作业性能分析聚合DTO（合并健康概览+耗时排行）
export interface AnalyticsJobPerformance {
  /** 作业健康概览数据 */
  jobHealthOverview: JobHealth[];
  /** 耗时基线分析数据 */
  topSlowJobs: TopSlowJob[];
}

/**
 * 获取统计分析概览聚合数据（合并作业统计+状态分布+执行趋势+热力图）
 * @param query 查询参数
 * @returns 统计分析概览聚合数据
 */
export async function getAnalyticsOverview(query?: StatsQueryDto): Promise<ApiResponse<AnalyticsOverview>> {
  const response = await requestClient.post('/api/quartz/GetAnalyticsOverview', query);
  return response;
}

/**
 * 获取作业性能分析聚合数据（合并健康概览+耗时排行）
 * @param query 查询参数（含topCount）
 * @returns 作业性能分析聚合数据
 */
export async function getAnalyticsJobPerformance(query?: JobPerformanceQueryDto): Promise<ApiResponse<AnalyticsJobPerformance>> {
  const response = await requestClient.post('/api/quartz/GetAnalyticsJobPerformance', query);
  return response;
}