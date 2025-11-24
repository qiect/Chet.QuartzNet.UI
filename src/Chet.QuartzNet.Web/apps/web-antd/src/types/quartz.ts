// Quartz任务相关类型定义

// 调度器状态
export interface SchedulerStatus {
  running: boolean;
  status: 'STARTED' | 'STOPPED';
  message: string;
  runningSince?: string;
}

// 作业信息
export interface JobInfo {
  jobName: string;
  jobGroup: string;
  cronExpression: string;
  jobClassName: string;
  status: number; // 0: 正常, 1: 暂停, 2: 完成, 3: 错误, 4: 阻塞
  description?: string;
  jobData?: string;
}

// 作业列表响应
export interface JobListResponse {
  list: JobInfo[];
  total: number;
  page: number;
  pageSize: number;
}

// 作业日志
export interface JobLog {
  id: string;
  jobName: string;
  jobGroup: string;
  status: number; // 0: 运行中, 1: 成功, 2: 失败
  startTime: string;
  endTime: string;
  duration: number; // 执行时长，毫秒
  result?: string;
  exception?: string;
}

// 日志列表响应
export interface JobLogResponse {
  list: JobLog[];
  total: number;
  page: number;
  pageSize: number;
}

// 作业操作参数
export interface JobOperationParams {
  jobName: string;
  jobGroup: string;
}

// 作业保存参数
export interface JobSaveParams {
  jobName: string;
  jobGroup: string;
  cronExpression: string;
  jobClassName: string;
  description?: string;
  jobData?: string;
}

// 任务状态枚举
export enum JobStatus {
  NORMAL = 0,
  PAUSED = 1,
  COMPLETED = 2,
  ERROR = 3,
  BLOCKED = 4
}

// 日志状态枚举
export enum LogStatus {
  RUNNING = 0,
  SUCCESS = 1,
  FAILURE = 2
}

// 排序方向
export type SortOrder = 'asc' | 'desc';

// 列表查询参数
export interface ListParams {
  page?: number;
  pageSize?: number;
  sortField?: string;
  sortOrder?: SortOrder;
}