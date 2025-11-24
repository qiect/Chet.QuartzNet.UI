import type { Recordable } from '@vben/types';
import { requestClient } from '../request';

// 作业类型枚举
export enum JobTypeEnum {
  CLASS = 0,
  HTTP = 1,
  SCRIPT = 2
}

// 作业状态枚举
export enum JobStatusEnum {
  STOPPED = 0,
  RUNNING = 1
}

// 作业请求DTO
export interface JobRequestDto {
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 触发器名称 */
  triggerName: string;
  /** 触发器分组 */
  triggerGroup: string;
  /** Cron表达式 */
  cronExpression: string;
  /** 作业描述 */
  description?: string;
  /** 作业类型枚举 */
  jobTypeEnum: JobTypeEnum;
  /** 作业类型（类名或API URL） */
  jobType: string;
  /** 作业数据（JSON格式） */
  jobData?: string;
  /** API请求方法 */
  apiMethod?: string;
  /** API请求头 */
  apiHeaders?: string;
  /** API请求体 */
  apiRequestBody?: string;
}

// 作业响应DTO
export interface JobResponseDto {
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 触发器名称 */
  triggerName: string;
  /** 触发器分组 */
  triggerGroup: string;
  /** Cron表达式 */
  cronExpression: string;
  /** 作业描述 */
  description?: string;
  /** 作业类型枚举 */
  jobTypeEnum: JobTypeEnum;
  /** 作业类型（类名或API URL） */
  jobType: string;
  /** 作业数据（JSON格式） */
  jobData?: string;
  /** API请求方法 */
  apiMethod?: string;
  /** API请求头 */
  apiHeaders?: string;
  /** API请求体 */
  apiRequestBody?: string;
  /** 作业状态 */
  jobStatus: JobStatusEnum;
  /** 创建时间 */
  createTime?: string;
  /** 下次执行时间 */
  nextFireTime?: string;
  /** 上次执行时间 */
  previousFireTime?: string;
}

// 分页查询参数
export interface JobPageQueryParams {
  /** 页码 */
  pageNum?: number;
  /** 每页大小 */
  pageSize?: number;
  /** 作业名称 */
  jobName?: string;
  /** 作业分组 */
  jobGroup?: string;
  /** 作业状态 */
  jobStatus?: JobStatusEnum;
}

// 分页响应
export interface PageResponse<T> {
  /** 总记录数 */
  total: number;
  /** 数据列表 */
  list: T[];
  /** 每页大小 */
  pageSize: number;
  /** 页码 */
  pageNum: number;
}

/**
 * 获取作业列表
 * @param params 查询参数
 * @returns 作业列表分页数据
 */
export async function getJobList(params: JobPageQueryParams): Promise<PageResponse<JobResponseDto>> {
  return requestClient.get('/quartz/job/list', { params });
}

/**
 * 获取作业详情
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 作业详情
 */
export async function getJobDetail(jobName: string, jobGroup: string): Promise<JobResponseDto> {
  return requestClient.get(`/quartz/job/${jobGroup}/${jobName}`);
}

/**
 * 创建作业
 * @param data 作业数据
 * @returns 创建结果
 */
export async function createJob(data: JobRequestDto): Promise<boolean> {
  return requestClient.post('/quartz/job', data);
}

/**
 * 更新作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @param data 作业数据
 * @returns 更新结果
 */
export async function updateJob(jobName: string, jobGroup: string, data: JobRequestDto): Promise<boolean> {
  return requestClient.put(`/quartz/job/${jobGroup}/${jobName}`, data);
}

/**
 * 删除作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 删除结果
 */
export async function deleteJob(jobName: string, jobGroup: string): Promise<boolean> {
  return requestClient.delete(`/quartz/job/${jobGroup}/${jobName}`);
}

/**
 * 暂停作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 暂停结果
 */
export async function pauseJob(jobName: string, jobGroup: string): Promise<boolean> {
  return requestClient.post(`/quartz/job/pause/${jobGroup}/${jobName}`);
}

/**
 * 恢复作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 恢复结果
 */
export async function resumeJob(jobName: string, jobGroup: string): Promise<boolean> {
  return requestClient.post(`/quartz/job/resume/${jobGroup}/${jobName}`);
}

/**
 * 立即执行作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 执行结果
 */
export async function triggerJob(jobName: string, jobGroup: string): Promise<boolean> {
  return requestClient.post(`/quartz/job/trigger/${jobGroup}/${jobName}`);
}

/**
 * 获取作业分组列表
 * @returns 作业分组列表
 */
export async function getJobGroups(): Promise<string[]> {
  return requestClient.get('/quartz/job/groups');
}

/**
 * 获取系统作业类型列表（仅CLASS类型作业可用）
 * @returns 作业类型列表
 */
export async function getSystemJobTypes(): Promise<Array<{label: string, value: string}>> {
  return requestClient.get('/quartz/job/system-types');
}

/**
 * 验证Cron表达式
 * @param cronExpression Cron表达式
 * @returns 验证结果
 */
export async function validateCronExpression(cronExpression: string): Promise<{valid: boolean, description?: string}> {
  return requestClient.get('/quartz/job/validate-cron', { params: { cronExpression } });
}
