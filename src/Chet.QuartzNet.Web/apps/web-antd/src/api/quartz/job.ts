import { requestClient } from '../request';

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

// 分页响应DTO
export interface PagedResponse<T> {
  /** 数据列表 */
  items: T[];
  /** 总条数 */
  totalCount: number;
  /** 页码 */
  pageIndex: number;
  /** 每页条数 */
  pageSize: number;
  /** 总页数 */
  totalPages: number;
}

// 作业类型枚举
export enum JobTypeEnum {
  DLL = 0,
  API = 1
}

// 作业状态枚举
export enum JobStatusEnum {
  /** 正常 */
  Normal = 0,
  /** 暂停 */
  Paused = 1,
  /** 完成 */
  Completed = 2,
  /** 错误 */
  Error = 3,
  /** 阻塞 */
  Blocked = 4
}

// Quartz作业DTO
export interface QuartzJobDto {
  /** 作业名称 */
  jobName: string;
  /** 作业分组 */
  jobGroup: string;
  /** 触发器名称（可选，若为空则自动根据作业名称生成） */
  triggerName?: string;
  /** 触发器分组（可选，默认与作业分组相同） */
  triggerGroup?: string;
  /** Cron表达式 */
  cronExpression: string;
  /** 作业描述 */
  description?: string;
  /** 作业类型 */
  jobType: JobTypeEnum;
  /** 作业类名或API URL */
  jobClassOrApi: string;
  /** 作业数据（JSON格式） */
  jobData?: string;
  /** API请求方法 */
  apiMethod?: string;
  /** API请求头（JSON格式） */
  apiHeaders?: string;
  /** API请求体（JSON格式） */
  apiBody?: string;
  /** API超时时间（毫秒） */
  apiTimeout?: number;
  /** 失败重试次数（0=不重试） */
  retryCount?: number;
  /** 失败重试间隔（秒） */
  retryIntervalSeconds?: number;
  /** 跳过SSL验证 */
  skipSslValidation?: boolean;
  /** 禁止并发执行（同一作业上一次执行未完成时，新的触发会等待） */
  disallowConcurrentExecution?: boolean;
  /** 开始时间 */
  startTime?: string;
  /** 结束时间 */
  endTime?: string;
  /** 是否启用 */
  isEnabled?: boolean;
}

// Quartz作业响应DTO
export interface QuartzJobResponseDto {
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
  /** 作业类型 */
  jobType: JobTypeEnum;
  /** 作业类名或API URL */
  jobClassOrApi: string;
  /** 作业数据（JSON格式） */
  jobData?: string;
  /** API请求方法 */
  apiMethod?: string;
  /** API请求头 */
  apiHeaders?: string;
  /** API请求体 */
  apiBody?: string;
  /** API请求超时时间（秒） */
  apiTimeout: number;
  /** 失败重试次数（0=不重试） */
  retryCount: number;
  /** 失败重试间隔（秒） */
  retryIntervalSeconds: number;
  /** 是否跳过SSL验证 */
  skipSslValidation: boolean;
  /** 禁止并发执行 */
  disallowConcurrentExecution: boolean;
  /** 开始时间 */
  startTime?: string;
  /** 结束时间 */
  endTime?: string;
  /** 作业状态 */
  status: JobStatusEnum;
  /** 是否启用 */
  isEnabled: boolean;
  /** 创建时间 */
  createTime: string;
  /** 更新时间 */
  updateTime?: string;
  /** 创建人 */
  createBy?: string;
  /** 更新人 */
  updateBy?: string;
  /** 下次执行时间 */
  nextRunTime?: string;
  /** 上次执行时间 */
  previousRunTime?: string;
}

// Quartz作业查询DTO
export interface QuartzJobQueryDto {
  /** 作业名称 */
  jobName?: string;
  /** 作业分组 */
  jobGroup?: string;
  /** 作业状态 */
  status?: JobStatusEnum;
  /** 作业类名或API */
  jobClassOrApi?: string;
  /** 是否启用 */
  isEnabled?: boolean;
  /** 页码 */
  pageIndex?: number;
  /** 每页条数 */
  pageSize?: number;
  /** 排序字段 */
  sortBy?: string;
  /** 排序方向（asc或desc） */
  sortOrder?: string;
}

/**
 * 获取作业列表
 * @param query 查询参数
 * @returns 作业列表分页数据
 */
export async function getJobs(query: QuartzJobQueryDto): Promise<ApiResponse<PagedResponse<QuartzJobResponseDto>>> {
  const response = await requestClient.post('/api/quartz/GetJobs', query);
  return response;
}

/**
 * 获取作业详情
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 作业详情
 */
export async function getJob(jobName: string, jobGroup: string): Promise<ApiResponse<QuartzJobResponseDto>> {
  const response = await requestClient.get('/api/quartz/GetJob', {
    params: { jobName, jobGroup }
  });
  return response;
}

/**
 * 添加作业
 * @param jobDto 作业数据
 * @returns 添加结果
 */
export async function addJob(jobDto: QuartzJobDto): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/AddJob', jobDto);
  return response;
}

/**
 * 更新作业
 * @param jobDto 作业数据
 * @returns 更新结果
 */
export async function updateJob(jobDto: QuartzJobDto): Promise<ApiResponse<boolean>> {
  const response = await requestClient.put('/api/quartz/UpdateJob', jobDto);
  return response;
}

/**
 * 删除作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 删除结果
 */
export async function deleteJob(jobName: string, jobGroup: string): Promise<ApiResponse<boolean>> {
  const response = await requestClient.delete('/api/quartz/DeleteJob', {
    params: { jobName, jobGroup }
  });
  return response;
}

/**
 * 批量删除作业
 * @param jobs 作业列表，包含JobName和JobGroup字段
 * @returns 删除结果
 */
export async function batchDeleteJob(jobs: Array<{JobName: string; JobGroup: string}>): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/BatchDeleteJobs', jobs);
  return response;
}

/**
 * 暂停作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 暂停结果
 */
export async function pauseJob(jobName: string, jobGroup: string): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/PauseJob', null, {
    params: { jobName, jobGroup }
  });
  return response;
}

/**
 * 恢复作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 恢复结果
 */
export async function resumeJob(jobName: string, jobGroup: string): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/ResumeJob', null, {
    params: { jobName, jobGroup }
  });
  return response;
}

/**
 * 立即触发作业
 * @param jobName 作业名称
 * @param jobGroup 作业分组
 * @returns 触发结果
 */
export async function triggerJob(jobName: string, jobGroup: string): Promise<ApiResponse<boolean>> {
  const response = await requestClient.post('/api/quartz/TriggerJob', null, {
    params: { jobName, jobGroup }
  });
  return response;
}