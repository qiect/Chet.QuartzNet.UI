import { requestClient } from '../request';

import type { ApiResponse } from './job';

// 系统配置DTO
export interface SystemConfigDto {
  /** 服务名称 */
  serviceName: string;
  /** 环境标识 DEV/TEST/UAT/PROD */
  environment: string;
  /** 服务描述 */
  serviceDescription: string;
  /** 日志保留天数，<=0 表示不按天数清理 */
  logRetentionDays?: number;
  /** 日志最大保留条数，<=0 表示不限制 */
  logMaxCount?: number;
  /** 日志自动清理间隔（小时） */
  logCleanupIntervalHours?: number;
}

// 迁移步骤状态枚举
export enum MigrationStepStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Skipped = 4,
}

// 迁移步骤信息
export interface MigrationStepInfo {
  /** 步骤名称 */
  name: string;
  /** 步骤键 */
  key: string;
  /** 步骤状态 */
  status: MigrationStepStatus;
  /** 总记录数 */
  totalCount: number;
  /** 已迁移数 */
  migratedCount: number;
  /** 跳过数 */
  skippedCount: number;
  /** 更新数 */
  updatedCount?: number;
  /** 失败数 */
  failedCount?: number;
  /** 错误消息 */
  errorMessage?: string;
  /** 步骤开始时间 */
  startTime?: string;
  /** 步骤结束时间 */
  endTime?: string;
}

// 单实体迁移状态
export interface MigrationEntityState {
  /** 实体键 */
  entity: string;
  /** 实体名称 */
  name: string;
  /** 状态 */
  status: MigrationStepStatus;
  /** 源端数量 */
  sourceCount: number;
  /** 已处理数 */
  processed: number;
  /** 插入数 */
  inserted: number;
  /** 更新数 */
  updated: number;
  /** 跳过数 */
  skipped: number;
  /** 失败数 */
  failed: number;
  /** 错误消息 */
  errorMessage?: string;
}

// 单行错误样本
export interface MigrationRowError {
  /** 实体键 */
  entity: string;
  /** 记录键 */
  key: string;
  /** 相关字段 */
  field?: string;
  /** 错误消息 */
  message: string;
}

// 数据迁移状态DTO
export interface DataMigrationStatusDto {
  /** 迁移是否正在运行 */
  isRunning: boolean;
  /** 整体进度百分比（0-100） */
  progressPercent: number;
  /** 当前步骤描述 */
  currentStep: string;
  /** 各步骤详情 */
  steps: MigrationStepInfo[];
  /** 迁移开始时间 */
  startTime?: string;
  /** 迁移结束时间 */
  endTime?: string;
  /** 总耗时（毫秒） */
  durationMs?: number;
  /** 是否已完成 */
  isCompleted: boolean;
  /** 是否成功 */
  isSuccess: boolean;
  /** 错误消息 */
  errorMessage?: string;
  /** 文件存储路径 */
  fileStoragePath: string;
  /** 文件存储路径是否存在 */
  fileStoragePathExists: boolean;
  /** 当前存储类型 */
  storageType: string;
  /** 迁移ID */
  migrationId?: string;
  /** 源存储展示名称 */
  sourceDisplay?: string;
  /** 目标存储展示名称 */
  targetDisplay?: string;
  /** 写入模式 */
  mode?: string;
  /** 当前阶段 */
  stage?: string;
  /** 当前实体 */
  currentEntity?: string;
  /** 已处理记录数 */
  processed?: number;
  /** 待处理总数 */
  total?: number;
  /** 是否已取消 */
  isCancelled?: boolean;
  /** 逐实体状态 */
  entities?: MigrationEntityState[];
  /** 错误样本 */
  errorSamples?: MigrationRowError[];
  /** 告警 */
  warnings?: string[];
  /** 迁移后配置建议 */
  suggestedConfig?: string;
}

// 存储类型
export type MigrationStoreType =
  | 'File'
  | 'MySql'
  | 'PostgreSql'
  | 'SQLite'
  | 'SqlServer';

// 迁移写入模式
export type MigrationWriteMode = 'Merge' | 'Overwrite' | 'Replace';

// 迁移实体
export type MigrationEntity = 'Jobs' | 'Logs' | 'Settings' | 'Notifications';

// 迁移存储描述
export interface MigrationStoreDescriptor {
  /** 存储类型 */
  type: MigrationStoreType;
  /** 文件路径 */
  filePath?: string;
  /** 配置档名称 */
  profileName?: string;
  /** 服务端版本 */
  serverVersion?: string;
}

// 迁移预检单实体结果
export interface MigrationPreviewEntityDto {
  /** 实体键 */
  entity: string;
  /** 实体名称 */
  name: string;
  /** 源端数量 */
  sourceCount: number;
  /** 目标已有数量 */
  targetCount: number;
  /** 预计插入数量 */
  willInsert: number;
  /** 预计更新数量 */
  willUpdate: number;
  /** 预计跳过数量 */
  willSkip: number;
}

// 迁移预检结果
export interface MigrationPreviewDto {
  /** 是否可执行 */
  canRun: boolean;
  /** 校验失败原因 */
  message?: string;
  /** 源存储 */
  source: string;
  /** 目标存储 */
  target: string;
  /** 写入模式 */
  mode: string;
  /** 逐实体结果 */
  entities: MigrationPreviewEntityDto[];
  /** 告警 */
  warnings: string[];
}

// 迁移文件源选项
export interface MigrationFileSourceOptionDto {
  /** 名称 */
  name: string;
  /** 路径 */
  path: string;
}

// 迁移配置档选项
export interface MigrationProfileOptionDto {
  /** 名称 */
  name: string;
  /** 数据库类型 */
  provider: string;
  /** 是否已配置连接字符串 */
  hasConnectionString: boolean;
}

// 数据库 Provider 安装状态
export interface MigrationProviderOptionDto {
  /** 存储类型 */
  type: string;
  /** NuGet 包名 */
  packageName: string;
  /** 是否已安装 */
  installed: boolean;
}

// 迁移可选项
export interface MigrationOptionsDto {
  /** 当前存储类型 */
  currentStorageType: string;
  /** 当前数据库 Provider */
  currentDatabaseProvider?: string;
  /** 文件存储路径 */
  fileStoragePath: string;
  /** 文件存储路径是否存在 */
  fileStoragePathExists: boolean;
  /** 文件源 */
  fileSources: MigrationFileSourceOptionDto[];
  /** 配置档 */
  profiles: MigrationProfileOptionDto[];
  /** Provider 安装状态 */
  providers: MigrationProviderOptionDto[];
  /** 是否允许 Replace */
  allowReplace: boolean;
  /** 是否允许前端提交连接字符串 */
  allowAdHocConnectionString: boolean;
  /** 批次大小 */
  batchSize: number;
  /** 错误样本上限 */
  maxErrorSamples: number;
}

// 触发迁移请求DTO
export interface TriggerMigrationRequestDto {
  /** 是否强制重新迁移 */
  force?: boolean;
  /** 迁移源 */
  source?: MigrationStoreDescriptor;
  /** 迁移目标 */
  target?: MigrationStoreDescriptor;
  /** 迁移实体 */
  entities?: MigrationEntity[];
  /** 写入模式 */
  mode?: MigrationWriteMode;
  /** 出错后是否继续 */
  continueOnError?: boolean;
  /** Replace 二次确认 */
  confirmReplace?: boolean;
  /** 迁移期间是否暂停调度器 */
  pauseScheduler?: boolean;
  /** 是否允许升级源库结构 */
  allowUpgradeSourceSchema?: boolean;
}

// 获取系统配置
export const getSystemConfig = async (): Promise<SystemConfigDto> => {
  const response = await requestClient.get('/api/quartz/GetSystemConfig');
  return response;
};

// 保存系统配置
export const saveSystemConfig = async (
  config: SystemConfigDto,
): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post('/api/quartz/SaveSystemConfig', config);
  return response;
};

// 获取数据迁移状态
export const getMigrationStatus =
  async (): Promise<ApiResponse<DataMigrationStatusDto>> => {
    const response = await requestClient.get('/api/quartz/GetMigrationStatus');
    return response;
  };

// 获取数据迁移可选项
export const getMigrationOptions =
  async (): Promise<ApiResponse<MigrationOptionsDto>> => {
    const response = await requestClient.get('/api/quartz/GetMigrationOptions');
    return response;
  };

// 迁移预检
export const previewMigration = async (
  request: TriggerMigrationRequestDto,
): Promise<ApiResponse<MigrationPreviewDto>> => {
  const response = await requestClient.post(
    '/api/quartz/PreviewMigration',
    request,
  );
  return response;
};

// 触发数据迁移
export const triggerMigration = async (
  request?: TriggerMigrationRequestDto,
): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post(
    '/api/quartz/TriggerMigration',
    request ?? { force: false },
  );
  return response;
};

// 取消数据迁移
export const cancelMigration = async (): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post('/api/quartz/CancelMigration');
  return response;
};
