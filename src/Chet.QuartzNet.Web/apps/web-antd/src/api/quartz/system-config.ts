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
  /** 错误消息 */
  errorMessage?: string;
  /** 步骤开始时间 */
  startTime?: string;
  /** 步骤结束时间 */
  endTime?: string;
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
}

// 触发迁移请求DTO
export interface TriggerMigrationRequestDto {
  /** 是否强制重新迁移 */
  force: boolean;
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

// 触发文件数据迁移
export const triggerMigration = async (
  request?: TriggerMigrationRequestDto,
): Promise<ApiResponse<boolean>> => {
  const response = await requestClient.post(
    '/api/quartz/TriggerMigration',
    request ?? { force: false },
  );
  return response;
};