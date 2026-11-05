<script lang="ts" setup>
import { ref, computed, onUnmounted, watch } from 'vue';

import { Page } from '@vben/common-ui';
import { formatDateTime } from '@vben/utils';

import {
  Button,
  Card,
  Checkbox,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Select,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { $t } from '#/locales';

import {
  getSystemConfig,
  saveSystemConfig,
  getMigrationStatus,
  getMigrationOptions,
  previewMigration,
  triggerMigration,
  cancelMigration,
  MigrationStepStatus,
} from '../../api/quartz/system-config';
import type {
  DataMigrationStatusDto,
  MigrationEntity,
  MigrationOptionsDto,
  MigrationPreviewDto,
  MigrationStepInfo,
  MigrationStoreDescriptor,
  MigrationStoreType,
  MigrationWriteMode,
} from '../../api/quartz/system-config';
import { useSystemConfig } from '../../composables/use-system-config';

const configLoading = ref(false);
const saveLoading = ref(false);

const serviceName = ref('');
const environment = ref('DEV');
const serviceDescription = ref('');

// 日志自动清理配置
const logRetentionDays = ref(30);
const logMaxCount = ref(0);
const logCleanupIntervalHours = ref(6);

const environmentOptions = computed(() => [
  { label: $t('page.quartz.systemConfigPage.envDEV'), value: 'DEV' },
  { label: $t('page.quartz.systemConfigPage.envTEST'), value: 'TEST' },
  { label: $t('page.quartz.systemConfigPage.envUAT'), value: 'UAT' },
  { label: $t('page.quartz.systemConfigPage.envPROD'), value: 'PROD' },
]);

const envTagColor = computed(() => {
  const map: Record<string, string> = { DEV: 'default', TEST: 'processing', UAT: 'warning', PROD: 'error' };
  return map[environment.value] || 'default';
});

async function loadConfig() {
  configLoading.value = true;
  try {
    const response = (await getSystemConfig()) as any;
    const data = response?.data ?? response;
    serviceName.value = data?.serviceName || '';
    environment.value = data?.environment || 'DEV';
    serviceDescription.value = data?.serviceDescription || '';
    logRetentionDays.value = data?.logRetentionDays ?? 30;
    logMaxCount.value = data?.logMaxCount ?? 0;
    logCleanupIntervalHours.value = data?.logCleanupIntervalHours ?? 6;
  } catch (error) {
    message.error($t('page.quartz.systemConfigPage.getConfigFailed'));
    console.error($t('page.quartz.systemConfigPage.getConfigFailed'), error);
  } finally {
    configLoading.value = false;
  }
}

async function handleSave() {
  if (!serviceName.value.trim()) {
    message.warning($t('page.quartz.systemConfigPage.serviceNameRequired'));
    return;
  }
  saveLoading.value = true;
  try {
    const response = await saveSystemConfig({
      serviceName: serviceName.value,
      environment: environment.value,
      serviceDescription: serviceDescription.value,
      logRetentionDays: logRetentionDays.value ?? 30,
      logMaxCount: logMaxCount.value ?? 0,
      logCleanupIntervalHours: logCleanupIntervalHours.value ?? 6,
    });
    if (response.success) {
      message.success($t('page.quartz.systemConfigPage.saveSuccess'));
      const { systemConfig } = useSystemConfig();
      systemConfig.value = {
        serviceName: serviceName.value,
        environment: environment.value,
        serviceDescription: serviceDescription.value,
      };
    } else {
      message.error(response.message || $t('page.quartz.systemConfigPage.saveFailed'));
    }
  } catch (error: any) {
    message.error(error.message || $t('page.quartz.systemConfigPage.saveFailed'));
    console.error($t('page.quartz.systemConfigPage.saveFailed'), error);
  } finally {
    saveLoading.value = false;
  }
}

async function handleReset() {
  await loadConfig();
  message.info($t('page.quartz.systemConfigPage.resetSuccess'));
}

loadConfig();

// ============ 数据迁移 ============

type MigrationState = 'idle' | 'running' | 'success' | 'failed' | 'cancelled';

const migrationStatus = ref<DataMigrationStatusDto | null>(null);
const migrationOptions = ref<MigrationOptionsDto | null>(null);
const migrationPreview = ref<MigrationPreviewDto | null>(null);
const migrationLoading = ref(false);
const previewLoading = ref(false);
const migrationCollapsed = ref(true);
const previewModalOpen = ref(false);
const summaryModalOpen = ref(false);
let migrationPollTimer: ReturnType<typeof setInterval> | null = null;

const migrationSource = ref('');
const migrationTarget = ref('');
const migrationMode = ref<MigrationWriteMode>('Merge');
const migrationEntities = ref<MigrationEntity[]>([
  'Jobs',
  'Logs',
  'Settings',
  'Notifications',
]);
const migrationContinueOnError = ref(false);
const migrationPauseScheduler = ref(false);
const migrationAllowUpgradeSource = ref(false);

// 整体状态 → 样式与文案映射
const stateConfig: Record<
  MigrationState,
  { dot: string; pill: string; textKey: string }
> = {
  idle: {
    textKey: 'migrationIdle',
    dot: 'bg-muted-foreground/50',
    pill: 'bg-muted text-muted-foreground',
  },
  running: {
    textKey: 'migrationRunning',
    dot: 'bg-blue-500',
    pill: 'bg-blue-500/10 text-blue-600 dark:text-blue-400',
  },
  success: {
    textKey: 'migrationCompleted',
    dot: 'bg-emerald-500',
    pill: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400',
  },
  failed: {
    textKey: 'migrationFailed',
    dot: 'bg-red-500',
    pill: 'bg-red-500/10 text-red-600 dark:text-red-400',
  },
  cancelled: {
    textKey: 'migrationCancelled',
    dot: 'bg-amber-500',
    pill: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  },
};

// 步骤状态 → 文案与颜色映射
const stepConfig: Record<number, { label: string; labelKey: string }> = {
  [MigrationStepStatus.Pending]: {
    labelKey: 'migrationStepPending',
    label: 'text-muted-foreground',
  },
  [MigrationStepStatus.Running]: {
    labelKey: 'migrationStepRunning',
    label: 'text-blue-600 dark:text-blue-400',
  },
  [MigrationStepStatus.Completed]: {
    labelKey: 'migrationStepCompleted',
    label: 'text-emerald-600 dark:text-emerald-400',
  },
  [MigrationStepStatus.Failed]: {
    labelKey: 'migrationStepFailed',
    label: 'text-red-600 dark:text-red-400',
  },
  [MigrationStepStatus.Skipped]: {
    labelKey: 'migrationStepSkipped',
    label: 'text-amber-600 dark:text-amber-400',
  },
};

const overallState = computed<MigrationState>(() => {
  const s = migrationStatus.value;
  if (!s) return 'idle';
  if (s.isRunning) return 'running';
  if (s.isCancelled) return 'cancelled';
  if (s.isCompleted && s.isSuccess) return 'success';
  if (s.isCompleted) return 'failed';
  return 'idle';
});

const overallStatusText = computed(() =>
  $t(`page.quartz.systemConfigPage.${stateConfig[overallState.value].textKey}`),
);

const migrationSteps = computed(() => migrationStatus.value?.steps ?? []);

// 存在迁移记录（进行中或已结束）时展示进度概览与摘要入口
const hasMigrationSummary = computed(() => {
  const status = migrationStatus.value;
  return !!status && (status.isRunning || status.isCompleted || status.isCancelled || false);
});

// 后端步骤 key → i18n 文案，未匹配时回退后端原始名称
const stepNameKeys: Record<string, string> = {
  jobs: 'migrationStepJobs',
  logs: 'migrationStepLogs',
  settings: 'migrationStepSettings',
  notifications: 'migrationStepNotifications',
};

function stepName(step: MigrationStepInfo): string {
  const key = stepNameKeys[step.key];
  return key
    ? $t(`page.quartz.systemConfigPage.${key}`)
    : step.name;
}

interface StorageOption {
  label: string;
  value: string;
  title?: string;
  disabled?: boolean;
}

const storageOptions = computed<StorageOption[]>(() => {
  const options = migrationOptions.value;
  const result: StorageOption[] = [];
  const isFileStorageCurrent = options?.currentStorageType === 'File';

  options?.fileSources.forEach((source) => {
    // 只有当前存储确实为文件存储时，才把该文件源标记为“当前”
    const isCurrentFile =
      isFileStorageCurrent && source.path === options?.fileStoragePath;
    // 展示路径优先使用后端计算的相对路径，完整路径放入 title 提示
    const displayPath = source.displayPath || source.path;
    const displayName =
      source.path === options?.fileStoragePath
        ? displayPath
        : source.name && source.name !== source.path
          ? source.name
          : displayPath;
    result.push({
      label: isCurrentFile
        ? `${$t('page.quartz.systemConfigPage.migrationCurrentFileStorage')} · ${displayName}`
        : `${$t('page.quartz.systemConfigPage.migrationFileSourceLabel')} · ${displayName}`,
      value: `file:${source.path}`,
      title: source.path,
    });
  });

  if (
    options?.currentStorageType === 'Database' &&
    options.currentDatabaseProvider
  ) {
    result.push({
      label: `${$t('page.quartz.systemConfigPage.migrationCurrentDatabase')} · ${options.currentDatabaseProvider}`,
      value: 'current',
    });
  }

  options?.profiles.forEach((profile) => {
    const displayName = profile.isAdditional
      ? $t('page.quartz.systemConfigPage.migrationAdditionalStore')
      : profile.name;
    result.push({
      label: `${displayName} · ${profile.provider}${profile.hasConnectionString ? '' : ` (${$t('page.quartz.systemConfigPage.migrationProfileNoConnection')})`}`,
      value: `profile:${profile.name}`,
      disabled: !profile.hasConnectionString,
    });
  });

  return result;
});

// 至少需要两种可用存储才能组合出迁移的源和目标
const hasAvailableStores = computed(
  () => storageOptions.value.filter((item) => !item.disabled).length >= 2,
);

const entityOptions = computed(() => [
  { label: $t('page.quartz.systemConfigPage.migrationStepJobs'), value: 'Jobs' },
  { label: $t('page.quartz.systemConfigPage.migrationStepLogs'), value: 'Logs' },
  {
    label: $t('page.quartz.systemConfigPage.migrationStepSettings'),
    value: 'Settings',
  },
  {
    label: $t('page.quartz.systemConfigPage.migrationStepNotifications'),
    value: 'Notifications',
  },
]);

const modeOptions = computed(() => [
  {
    label: $t('page.quartz.systemConfigPage.migrationModeMerge'),
    value: 'Merge',
  },
  {
    label: $t('page.quartz.systemConfigPage.migrationModeOverwrite'),
    value: 'Overwrite',
  },
  {
    label: $t('page.quartz.systemConfigPage.migrationModeReplace'),
    value: 'Replace',
    disabled: !migrationOptions.value?.allowReplace,
  },
]);

const unavailableProviders = computed(() =>
  (migrationOptions.value?.providers ?? [])
    .filter((provider) => !provider.installed)
    .map((provider) => provider.packageName),
);

const canTrigger = computed(() => {
  return (
    !!migrationSource.value &&
    !!migrationTarget.value &&
    migrationSource.value !== migrationTarget.value &&
    migrationEntities.value.length > 0 &&
    !migrationStatus.value?.isRunning
  );
});

const isMigrationRunning = computed(() => !!migrationStatus.value?.isRunning);

const migrationWarnings = computed(() => migrationStatus.value?.warnings ?? []);

const migrationErrorSamples = computed(
  () => migrationStatus.value?.errorSamples ?? [],
);

// 当前实际生效的存储模式，兼容选项未加载完成的情况
const currentStorageDisplay = computed(() => {
  const options = migrationOptions.value;
  if (!options) {
    return migrationStatus.value?.storageType || '-';
  }

  if (options.currentStorageType === 'Database') {
    return options.currentDatabaseProvider
      ? `${$t('page.quartz.systemConfigPage.migrationStorageDatabase')} · ${options.currentDatabaseProvider}`
      : $t('page.quartz.systemConfigPage.migrationStorageDatabase');
  }

  return $t('page.quartz.systemConfigPage.migrationStorageFile');
});

function normalizeProvider(provider: string): MigrationStoreType {
  switch (provider.toLowerCase()) {
    case 'mysql': {
      return 'MySql';
    }
    case 'npgsql':
    case 'postgres':
    case 'postgresql': {
      return 'PostgreSql';
    }
    case 'sqlite': {
      return 'SQLite';
    }
    case 'mssql':
    case 'sqlserver': {
      return 'SqlServer';
    }
    default: {
      return provider as MigrationStoreType;
    }
  }
}

function buildStore(value: string): MigrationStoreDescriptor | undefined {
  if (!value) {
    return undefined;
  }

  if (value.startsWith('file:')) {
    return { type: 'File', filePath: value.slice(5) };
  }

  if (value === 'current') {
    const provider = migrationOptions.value?.currentDatabaseProvider;
    return provider
      ? { type: normalizeProvider(provider), profileName: 'current' }
      : undefined;
  }

  if (value.startsWith('profile:')) {
    const name = value.slice(8);
    const profile = migrationOptions.value?.profiles.find(
      (item) => item.name === name,
    );
    return profile
      ? { type: normalizeProvider(profile.provider), profileName: name }
      : undefined;
  }

  return undefined;
}

function buildRequest(force = false) {
  const source = buildStore(migrationSource.value);
  const target = buildStore(migrationTarget.value);

  return {
    force,
    mode: migrationMode.value,
    entities: [...migrationEntities.value],
    continueOnError: migrationContinueOnError.value,
    pauseScheduler: migrationPauseScheduler.value,
    allowUpgradeSourceSchema: migrationAllowUpgradeSource.value,
    confirmReplace: migrationMode.value === 'Replace',
    ...(source ? { source } : {}),
    ...(target ? { target } : {}),
  };
}

function handleEntitiesChange(values: unknown): void {
  migrationEntities.value = (values as MigrationEntity[]) ?? [];
}

function validateSelection(): boolean {
  if (!migrationSource.value || !migrationTarget.value) {
    message.warning(
      $t('page.quartz.systemConfigPage.migrationSelectRequired'),
    );
    return false;
  }

  if (migrationSource.value === migrationTarget.value) {
    message.warning(
      $t('page.quartz.systemConfigPage.migrationSourceTargetSame'),
    );
    return false;
  }

  if (!migrationEntities.value.length) {
    message.warning($t('page.quartz.systemConfigPage.migrationNoEntities'));
    return false;
  }

  return true;
}

function formatDuration(ms?: null | number): string {
  if (ms === undefined || ms === null) return '-';
  if (ms < 1000) return `${ms}ms`;
  const seconds = ms / 1000;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
}

function formatTime(value?: string): string {
  return value ? formatDateTime(value) : '-';
}

function stepLabelClass(status: number): string {
  return stepConfig[status]?.label ?? 'text-muted-foreground';
}

function stepLabelKey(status: number): string {
  return stepConfig[status]?.labelKey ?? 'migrationStepPending';
}

async function loadMigrationStatus(initial = false) {
  try {
    const response = await getMigrationStatus();
    if (response.success && response.data) {
      migrationStatus.value = response.data;
      if (response.data.isRunning) {
        if (!migrationPollTimer) startPolling();
        // 页面加载时迁移进行中，自动展开面板
        if (initial) migrationCollapsed.value = false;
      }
    }
  } catch {
    // silent
  }
}

async function loadMigrationOptions() {
  try {
    const response = await getMigrationOptions();
    if (!response.success || !response.data) {
      return;
    }

    const options = response.data;
    migrationOptions.value = options;

    const defaultFileSource =
      options.fileSources.find(
        (source) =>
          source.path === options.fileStoragePath &&
          options.fileStoragePathExists,
      ) ??
      options.fileSources.find(
        (source) => source.path !== options.fileStoragePath,
      ) ??
      options.fileSources[0];
    const fileDefault = defaultFileSource?.path
      ? `file:${defaultFileSource.path}`
      : '';
    // 当前存储为文件时，优先把其他文件源作为迁移目标，避免源和目标都指向当前存储
    const otherFileSource = options.fileSources.find(
      (source) => source.path !== options.fileStoragePath,
    );
    const otherFileDefault = otherFileSource?.path
      ? `file:${otherFileSource.path}`
      : '';
    const profileDefault = options.profiles.find(
      (profile) => profile.hasConnectionString,
    )
      ? `profile:${
          options.profiles.find((profile) => profile.hasConnectionString)!.name
        }`
      : '';
    const currentDefault =
      options.currentStorageType === 'Database' &&
      options.currentDatabaseProvider
        ? 'current'
        : '';

    if (!migrationSource.value) {
      // 迁移源默认为当前存储，符合“从当前存储迁出”的直觉
      migrationSource.value = currentDefault || fileDefault || profileDefault;
    }

    if (!migrationTarget.value) {
      migrationTarget.value = currentDefault
        ? fileDefault || profileDefault || otherFileDefault
        : profileDefault || otherFileDefault || fileDefault;
    }

    if (migrationSource.value === migrationTarget.value) {
      if (profileDefault && migrationSource.value !== profileDefault) {
        migrationSource.value = profileDefault;
      } else if (
        otherFileDefault &&
        migrationSource.value !== otherFileDefault
      ) {
        migrationSource.value = otherFileDefault;
      } else if (fileDefault && migrationSource.value !== fileDefault) {
        migrationSource.value = fileDefault;
      }
    }
  } catch {
    message.error($t('page.quartz.systemConfigPage.migrationOptionsFailed'));
  }
}

async function handlePreviewMigration() {
  if (!validateSelection()) {
    return;
  }

  previewLoading.value = true;
  try {
    const response = await previewMigration(buildRequest(false));
    if (response.success && response.data) {
      migrationPreview.value = response.data;
      previewModalOpen.value = true;
      if (!response.data.canRun) {
        message.error(
          response.data.message ||
            $t('page.quartz.systemConfigPage.migrationPreviewCannotRun'),
        );
      }
    } else {
      message.error(
        response.message ||
          $t('page.quartz.systemConfigPage.migrationPreviewFailed'),
      );
    }
  } catch (error: any) {
    message.error(
      error.message ||
        $t('page.quartz.systemConfigPage.migrationPreviewFailed'),
    );
  } finally {
    previewLoading.value = false;
  }
}

async function doTriggerMigration(force: boolean) {
  migrationLoading.value = true;
  try {
    const response = await triggerMigration(buildRequest(force));
    if (response.success) {
      message.success(
        $t('page.quartz.systemConfigPage.migrationTriggerSuccess'),
      );
      migrationCollapsed.value = false;
      previewModalOpen.value = false;
      summaryModalOpen.value = true;
      startPolling();
    } else {
      message.error(
        response.message ||
          $t('page.quartz.systemConfigPage.migrationTriggerFailed'),
      );
    }
  } catch (error: any) {
    message.error(
      error.message ||
        $t('page.quartz.systemConfigPage.migrationTriggerFailed'),
    );
  } finally {
    migrationLoading.value = false;
  }
}

async function handleTriggerMigration(force: boolean = false) {
  if (!validateSelection()) {
    return;
  }

  if (migrationMode.value === 'Replace') {
    Modal.confirm({
      title: $t('page.quartz.systemConfigPage.migrationConfirmReplaceTitle'),
      content: $t('page.quartz.systemConfigPage.migrationConfirmReplace'),
      okText: $t('page.quartz.systemConfigPage.migrationConfirmOk'),
      cancelText: $t('page.quartz.systemConfigPage.migrationConfirmCancel'),
      onOk: () => doTriggerMigration(force),
    });
    return;
  }

  await doTriggerMigration(force);
}

function handleStartFromPreview() {
  handleTriggerMigration(false);
}

async function handleCancelMigration() {
  migrationLoading.value = true;
  try {
    const response = await cancelMigration();
    if (response.success) {
      message.success(
        $t('page.quartz.systemConfigPage.migrationCancelSuccess'),
      );
      startPolling();
    } else {
      message.error(
        response.message ||
          $t('page.quartz.systemConfigPage.migrationCancelFailed'),
      );
    }
  } catch (error: any) {
    message.error(
      error.message ||
        $t('page.quartz.systemConfigPage.migrationCancelFailed'),
    );
  } finally {
    migrationLoading.value = false;
  }
}

function startPolling() {
  stopPolling();
  migrationPollTimer = setInterval(async () => {
    await loadMigrationStatus();
    if (migrationStatus.value?.isCompleted) {
      stopPolling();
    }
  }, 1000);
  loadMigrationStatus();
}

function stopPolling() {
  if (migrationPollTimer) {
    clearInterval(migrationPollTimer);
    migrationPollTimer = null;
  }
}

onUnmounted(() => {
  stopPolling();
});

watch(
  [migrationSource, migrationTarget, migrationMode, migrationEntities],
  () => {
    migrationPreview.value = null;
  },
  { deep: true },
);

loadMigrationOptions();
loadMigrationStatus(true);
</script>

<template>
  <Page content-class="flex flex-col gap-4">
    <!-- 基础信息 -->
    <Card>
      <template #title>
        <div class="flex flex-col">
          <span>{{ $t('page.quartz.systemConfigPage.basicSection') }}</span>
          <span class="text-xs font-normal leading-4 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.description') }}
          </span>
        </div>
      </template>

      <Form layout="vertical" class="compact-form mb-2">
        <div class="grid grid-cols-1 gap-x-5 gap-y-0 sm:grid-cols-3">
          <FormItem
            class="sm:col-span-2"
            :label="$t('page.quartz.systemConfigPage.serviceName')"
          >
            <Input
              v-model:value="serviceName"
              :placeholder="$t('page.quartz.systemConfigPage.serviceNamePlaceholder')"
              allow-clear
              size="middle"
            />
          </FormItem>

          <FormItem :label="$t('page.quartz.systemConfigPage.environment')">
            <div class="flex items-center gap-2">
              <Select
                v-model:value="environment"
                :options="environmentOptions"
                :placeholder="$t('page.quartz.systemConfigPage.environmentPlaceholder')"
                size="middle"
                class="flex-1"
              />
              <Tag :color="envTagColor" size="small" class="text-xs shrink-0">{{ environment }}</Tag>
            </div>
          </FormItem>
        </div>

        <FormItem :label="$t('page.quartz.systemConfigPage.serviceDescription')">
          <Input.TextArea
            v-model:value="serviceDescription"
            :placeholder="$t('page.quartz.systemConfigPage.serviceDescriptionPlaceholder')"
            :auto-size="{ minRows: 1, maxRows: 3 }"
            :maxlength="200"
            show-count
            size="middle"
          />
        </FormItem>

        <!-- 日志自动清理 -->
        <div class="mt-2 border-t border-dashed border-border pt-4">
          <div class="mb-3 flex flex-col">
            <span class="text-sm font-medium">
              {{ $t('page.quartz.systemConfigPage.logCleanupSection') }}
            </span>
            <span class="text-xs leading-4 text-muted-foreground">
              {{ $t('page.quartz.systemConfigPage.logCleanupDescription') }}
            </span>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-x-6">
            <FormItem :label="$t('page.quartz.systemConfigPage.logRetentionDays')">
              <InputNumber
                v-model:value="logRetentionDays"
                :min="0"
                :max="3650"
                class="w-full"
                size="middle"
              />
            </FormItem>
            <FormItem :label="$t('page.quartz.systemConfigPage.logMaxCount')">
              <InputNumber
                v-model:value="logMaxCount"
                :min="0"
                :max="10000000"
                class="w-full"
                size="middle"
              />
            </FormItem>
            <FormItem :label="$t('page.quartz.systemConfigPage.logCleanupInterval')">
              <InputNumber
                v-model:value="logCleanupIntervalHours"
                :min="1"
                :max="168"
                class="w-full"
                size="middle"
              />
            </FormItem>
          </div>
          <div class="text-xs leading-5 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.logCleanupHint') }}
          </div>
        </div>
      </Form>

      <div class="flex gap-2 pt-2">
        <Button type="primary" :loading="saveLoading" @click="handleSave">
          {{ $t('page.quartz.systemConfigPage.save') }}
        </Button>
        <Button @click="handleReset">
          {{ $t('page.quartz.systemConfigPage.reset') }}
        </Button>
      </div>
    </Card>

    <!-- 数据迁移 -->
    <Card>
      <template #title>
        <div
          class="flex cursor-pointer select-none flex-col"
          @click="migrationCollapsed = !migrationCollapsed"
        >
          <span class="flex items-center gap-2">
            <svg
              class="h-3.5 w-3.5 text-muted-foreground transition-transform duration-200"
              :class="migrationCollapsed ? '-rotate-90' : ''"
              fill="none"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              viewBox="0 0 16 16"
            >
              <path d="M4 6.5 8 10.5 12 6.5" />
            </svg>
            {{ $t('page.quartz.systemConfigPage.migrationSection') }}
            <Tag :bordered="false" size="small" class="shrink-0 text-xs">
              {{ $t('page.quartz.systemConfigPage.migrationCurrentStorage') }}：{{ currentStorageDisplay }}
            </Tag>
          </span>
          <span class="pl-[22px] text-xs font-normal leading-4 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationDescription') }}
          </span>
        </div>
      </template>

      <template #extra>
        <span
          v-if="overallState !== 'idle'"
          class="inline-flex cursor-pointer select-none items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium"
          :class="stateConfig[overallState].pill"
          @click="migrationCollapsed = !migrationCollapsed"
        >
          <span
            class="h-1.5 w-1.5 rounded-full"
            :class="[stateConfig[overallState].dot, overallState === 'running' ? 'pulse-dot' : '']"
          />
          {{ overallStatusText }}
        </span>
      </template>

      <Transition name="expand">
        <div v-if="!migrationCollapsed" class="collapse-grid">
          <div class="collapse-inner">
            <!-- 只有一种存储时无法组合源和目标 -->
            <div
              v-if="!hasAvailableStores"
              class="rounded-md bg-muted/40 px-3 py-4 text-xs leading-5 text-muted-foreground"
            >
              {{ $t('page.quartz.systemConfigPage.migrationNoAvailableStore') }}
            </div>

            <!-- 迁移配置 -->
            <div v-else class="space-y-3">
              <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <div class="mb-1 text-xs font-medium text-muted-foreground">
                    {{ $t('page.quartz.systemConfigPage.migrationSource') }}
                  </div>
                  <Select
                    v-model:value="migrationSource"
                    :options="storageOptions"
                    :disabled="isMigrationRunning"
                    class="w-full"
                    size="middle"
                  />
                </div>
                <div>
                  <div class="mb-1 text-xs font-medium text-muted-foreground">
                    {{ $t('page.quartz.systemConfigPage.migrationTarget') }}
                  </div>
                  <Select
                    v-model:value="migrationTarget"
                    :options="storageOptions"
                    :disabled="isMigrationRunning"
                    class="w-full"
                    size="middle"
                  />
                </div>
              </div>

              <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <div class="mb-1 text-xs font-medium text-muted-foreground">
                    {{ $t('page.quartz.systemConfigPage.migrationMode') }}
                  </div>
                  <Select
                    v-model:value="migrationMode"
                    :options="modeOptions"
                    :disabled="isMigrationRunning"
                    class="w-full"
                    size="middle"
                  />
                </div>
                <div>
                  <div class="mb-1 text-xs font-medium text-muted-foreground">
                    {{ $t('page.quartz.systemConfigPage.migrationEntities') }}
                  </div>
                  <Checkbox.Group
                    :value="migrationEntities"
                    :options="entityOptions"
                    :disabled="isMigrationRunning"
                    @change="handleEntitiesChange"
                  />
                </div>
              </div>

              <div class="flex flex-wrap items-center gap-x-4 gap-y-2 text-[13px]">
                <Checkbox
                  v-model:checked="migrationContinueOnError"
                  :disabled="isMigrationRunning"
                >
                  {{ $t('page.quartz.systemConfigPage.migrationContinueOnError') }}
                </Checkbox>
                <Checkbox
                  v-model:checked="migrationPauseScheduler"
                  :disabled="isMigrationRunning"
                >
                  {{ $t('page.quartz.systemConfigPage.migrationPauseScheduler') }}
                </Checkbox>
                <Checkbox
                  v-model:checked="migrationAllowUpgradeSource"
                  :disabled="isMigrationRunning"
                >
                  {{ $t('page.quartz.systemConfigPage.migrationAllowUpgradeSource') }}
                </Checkbox>
              </div>

              <div
                v-if="unavailableProviders.length"
                class="rounded-md bg-amber-500/10 px-3 py-2 text-xs leading-5 text-amber-600 dark:text-amber-400"
              >
                {{ $t('page.quartz.systemConfigPage.migrationMissingProvider') }}:
                {{ unavailableProviders.join('、') }}
              </div>

              <!-- 操作 -->
              <div class="flex flex-wrap items-center gap-2">
                <Button
                  :loading="previewLoading"
                  :disabled="!canTrigger || isMigrationRunning"
                  @click="handlePreviewMigration"
                >
                  {{
                    previewLoading
                      ? $t('page.quartz.systemConfigPage.migrationPreviewing')
                      : $t('page.quartz.systemConfigPage.migrationPreview')
                  }}
                </Button>
                <Button
                  type="primary"
                  :loading="migrationLoading || isMigrationRunning"
                  :disabled="!canTrigger"
                  @click="handleTriggerMigration(false)"
                >
                  {{
                    isMigrationRunning
                      ? $t('page.quartz.systemConfigPage.migrationTriggering')
                      : $t('page.quartz.systemConfigPage.migrationTrigger')
                  }}
                </Button>
                <Button
                  v-if="isMigrationRunning"
                  danger
                  :loading="migrationLoading"
                  @click="handleCancelMigration"
                >
                  {{ $t('page.quartz.systemConfigPage.migrationCancel') }}
                </Button>
                <Tooltip
                  v-if="migrationStatus?.isCompleted && migrationStatus?.isSuccess"
                  :title="$t('page.quartz.systemConfigPage.migrationAlreadyCompleted')"
                >
                  <span>
                    <Button
                      :loading="migrationLoading"
                      :disabled="isMigrationRunning"
                      @click="handleTriggerMigration(true)"
                    >
                      {{ $t('page.quartz.systemConfigPage.migrationTriggerForce') }}
                    </Button>
                  </span>
                </Tooltip>
              </div>
            </div>

            <!-- 迁移进度概览：详细摘要放入弹窗 -->
            <div
              v-if="hasMigrationSummary"
              class="mt-4 flex items-center gap-3 rounded-lg border border-border/60 px-3 py-2.5"
            >
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
                  <span class="font-medium">
                    {{ $t('page.quartz.systemConfigPage.migrationSummary') }}
                  </span>
                  <span class="tabular-nums text-muted-foreground">
                    {{ migrationStatus?.progressPercent ?? 0 }}% ·
                    {{ migrationStatus?.processed ?? 0 }}/{{ migrationStatus?.total ?? 0 }}
                  </span>
                </div>
                <div class="mt-1.5 h-1.5 w-full overflow-hidden rounded-full bg-muted">
                  <div
                    class="h-full rounded-full bg-blue-500 transition-all duration-300"
                    :style="{ width: `${migrationStatus?.progressPercent ?? 0}%` }"
                  />
                </div>
              </div>
              <Button
                type="link"
                size="small"
                class="!h-auto shrink-0 !px-0 text-xs"
                @click="summaryModalOpen = true"
              >
                {{ $t('page.quartz.systemConfigPage.migrationViewSummary') }}
              </Button>
            </div>
          </div>
        </div>
      </Transition>
    </Card>

    <!-- 预检结果弹窗 -->
    <Modal
      v-model:open="previewModalOpen"
      :title="$t('page.quartz.systemConfigPage.migrationPreviewSummary')"
      :width="640"
      centered
      :footer="null"
    >
      <div v-if="migrationPreview" class="space-y-3">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-[13px]">
          <span class="text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationSource') }}
          </span>
          <span class="font-medium">{{ migrationPreview.source }}</span>
          <span class="text-muted-foreground">→</span>
          <span class="text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationTarget') }}
          </span>
          <span class="font-medium">{{ migrationPreview.target }}</span>
        </div>

        <div class="overflow-hidden rounded-lg border border-border/60">
          <div
            class="grid grid-cols-[minmax(0,1.4fr)_repeat(5,minmax(0,1fr))] gap-x-3 border-b border-border/60 bg-muted/40 px-3 py-2 text-xs font-medium text-muted-foreground"
          >
            <span>{{ $t('page.quartz.systemConfigPage.migrationEntities') }}</span>
            <span class="text-right">
              {{ $t('page.quartz.systemConfigPage.migrationPreviewSource') }}
            </span>
            <span class="text-right">
              {{ $t('page.quartz.systemConfigPage.migrationPreviewTarget') }}
            </span>
            <span class="text-right">
              {{ $t('page.quartz.systemConfigPage.migrationPreviewInsert') }}
            </span>
            <span class="text-right">
              {{ $t('page.quartz.systemConfigPage.migrationPreviewUpdate') }}
            </span>
            <span class="text-right">
              {{ $t('page.quartz.systemConfigPage.migrationPreviewSkip') }}
            </span>
          </div>
          <div
            v-for="item in migrationPreview.entities"
            :key="item.entity"
            class="grid grid-cols-[minmax(0,1.4fr)_repeat(5,minmax(0,1fr))] gap-x-3 border-b border-border/40 px-3 py-2 text-[13px] last:border-b-0"
          >
            <span class="truncate font-medium">{{ item.name }}</span>
            <span class="text-right tabular-nums">{{ item.sourceCount }}</span>
            <span class="text-right tabular-nums">{{ item.targetCount }}</span>
            <span class="text-right tabular-nums text-emerald-600 dark:text-emerald-400">
              {{ item.willInsert }}
            </span>
            <span class="text-right tabular-nums text-blue-600 dark:text-blue-400">
              {{ item.willUpdate }}
            </span>
            <span
              class="text-right tabular-nums"
              :class="item.willSkip > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-muted-foreground'"
            >
              {{ item.willSkip }}
            </span>
          </div>
        </div>

        <div v-if="migrationPreview.warnings.length" class="space-y-1">
          <div
            v-for="(warning, index) in migrationPreview.warnings"
            :key="index"
            class="break-all text-xs leading-5 text-amber-600 dark:text-amber-400"
          >
            • {{ warning }}
          </div>
        </div>

        <div
          v-if="!migrationPreview.canRun && migrationPreview.message"
          class="break-all rounded-md bg-red-500/10 px-3 py-2 text-xs leading-5 text-red-600 dark:text-red-400"
        >
          {{ migrationPreview.message }}
        </div>
      </div>

      <div class="mt-5 flex justify-end gap-2">
        <Button @click="previewModalOpen = false">
          {{ $t('page.quartz.systemConfigPage.migrationClose') }}
        </Button>
        <Button
          type="primary"
          :loading="migrationLoading"
          :disabled="!migrationPreview?.canRun"
          @click="handleStartFromPreview"
        >
          {{ $t('page.quartz.systemConfigPage.migrationTrigger') }}
        </Button>
      </div>
    </Modal>

    <!-- 迁移摘要弹窗 -->
    <Modal
      v-model:open="summaryModalOpen"
      :title="$t('page.quartz.systemConfigPage.migrationSummary')"
      :width="720"
      centered
      :footer="null"
    >
      <div class="max-h-[65vh] space-y-3 overflow-y-auto pr-1">
        <div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
          <span
            class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium"
            :class="stateConfig[overallState].pill"
          >
            <span
              class="h-1.5 w-1.5 rounded-full"
              :class="[stateConfig[overallState].dot, overallState === 'running' ? 'pulse-dot' : '']"
            />
            {{ overallStatusText }}
          </span>
          <span
            v-if="migrationStatus?.total"
            class="text-xs tabular-nums text-muted-foreground"
          >
            {{ migrationStatus?.progressPercent ?? 0 }}% ·
            {{ migrationStatus?.processed ?? 0 }}/{{ migrationStatus?.total ?? 0 }}
          </span>
        </div>

        <div class="h-1.5 w-full overflow-hidden rounded-full bg-muted">
          <div
            class="h-full rounded-full bg-blue-500 transition-all duration-300"
            :style="{ width: `${migrationStatus?.progressPercent ?? 0}%` }"
          />
        </div>

        <div
          v-if="migrationStatus?.isRunning && (migrationStatus.stage || migrationStatus.currentEntity)"
          class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
        >
          <span v-if="migrationStatus.stage">
            {{ $t('page.quartz.systemConfigPage.migrationStage') }}:
            <span class="font-mono text-foreground">{{ migrationStatus.stage }}</span>
          </span>
          <span v-if="migrationStatus.currentEntity">
            {{ $t('page.quartz.systemConfigPage.migrationCurrentEntity') }}:
            <span class="font-mono text-foreground">{{ migrationStatus.currentEntity }}</span>
          </span>
        </div>

        <div
          v-if="!migrationSteps.length"
          class="text-[13px] text-muted-foreground"
        >
          {{ $t('page.quartz.systemConfigPage.migrationNoSteps') }}
        </div>

        <div v-else>
          <div v-for="step in migrationSteps" :key="step.key" class="step-row">
            <span class="step-icon">
              <span
                v-if="step.status === MigrationStepStatus.Pending"
                class="h-2.5 w-2.5 rounded-full border-2 border-border"
              />
              <span v-else-if="step.status === MigrationStepStatus.Running" class="spin" />
              <span
                v-else-if="step.status === MigrationStepStatus.Completed"
                class="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-emerald-500 text-white"
              >
                <svg
                  class="h-2.5 w-2.5"
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2.5"
                  viewBox="0 0 16 16"
                >
                  <path d="M4 8.6l2.8 2.8L12 5.4" />
                </svg>
              </span>
              <span
                v-else-if="step.status === MigrationStepStatus.Failed"
                class="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-red-500 text-white"
              >
                <svg
                  class="h-2.5 w-2.5"
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2.5"
                  viewBox="0 0 16 16"
                >
                  <path d="M4.8 4.8l6.4 6.4M11.2 4.8l-6.4 6.4" />
                </svg>
              </span>
              <span
                v-else
                class="h-2.5 w-2.5 rounded-full border-2 border-amber-400"
              />
            </span>

            <div class="min-w-0 flex-1">
              <div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5">
                <span class="truncate text-[13px] font-medium text-foreground">
                  {{ stepName(step) }}
                </span>
                <span class="flex shrink-0 items-center gap-2 text-xs tabular-nums">
                  <span class="font-medium" :class="stepLabelClass(step.status)">
                    {{ $t(`page.quartz.systemConfigPage.${stepLabelKey(step.status)}`) }}
                  </span>
                  <span
                    v-if="step.status !== MigrationStepStatus.Pending"
                    class="text-muted-foreground"
                  >
                    {{ step.migratedCount }}/{{ step.totalCount }}
                    <span
                      v-if="step.skippedCount > 0"
                      class="text-amber-600 dark:text-amber-400"
                    >
                      +{{ step.skippedCount }}
                    </span>
                    <span
                      v-if="step.updatedCount"
                      class="text-blue-600 dark:text-blue-400"
                    >
                      {{ $t('page.quartz.systemConfigPage.migrationUpdatedCount') }}
                      {{ step.updatedCount }}
                    </span>
                    <span
                      v-if="step.failedCount"
                      class="text-red-600 dark:text-red-400"
                    >
                      {{ $t('page.quartz.systemConfigPage.migrationFailedCount') }}
                      {{ step.failedCount }}
                    </span>
                  </span>
                </span>
              </div>
              <div
                v-if="step.status === MigrationStepStatus.Failed && step.errorMessage"
                class="mt-1 break-all text-xs leading-5 text-red-600 dark:text-red-400"
              >
                {{ step.errorMessage }}
              </div>
            </div>
          </div>
        </div>

        <!-- 失败原因 -->
        <div
          v-if="(overallState === 'failed' || overallState === 'cancelled') && migrationStatus?.errorMessage"
          class="break-all rounded-md bg-red-500/10 px-3 py-2 text-xs leading-5 text-red-600 dark:text-red-400"
        >
          {{ migrationStatus.errorMessage }}
        </div>

        <!-- 告警 -->
        <div v-if="migrationWarnings.length" class="rounded-md bg-amber-500/10 px-3 py-2">
          <div class="text-xs font-medium text-amber-600 dark:text-amber-400">
            {{ $t('page.quartz.systemConfigPage.migrationWarnings') }}
          </div>
          <div
            v-for="(warning, index) in migrationWarnings"
            :key="index"
            class="mt-1 break-all text-xs leading-5 text-amber-600 dark:text-amber-400"
          >
            • {{ warning }}
          </div>
        </div>

        <!-- 错误样本 -->
        <div
          v-if="migrationErrorSamples.length"
          class="rounded-md bg-red-500/5 px-3 py-2"
        >
          <div class="text-xs font-medium text-red-600 dark:text-red-400">
            {{ $t('page.quartz.systemConfigPage.migrationErrorSamples') }}
          </div>
          <div
            v-for="(sample, index) in migrationErrorSamples"
            :key="index"
            class="mt-1 flex flex-wrap gap-x-2 text-xs leading-5 text-red-600 dark:text-red-400"
          >
            <span class="font-mono">
              {{ sample.entity }}<template v-if="sample.field">.{{ sample.field }}</template>
              · {{ sample.key }}
            </span>
            <span class="break-all">{{ sample.message }}</span>
          </div>
        </div>

        <!-- 迁移后配置建议 -->
        <div
          v-if="overallState === 'success' && migrationStatus?.suggestedConfig"
          class="rounded-md border border-border/60 px-3 py-2"
        >
          <div class="text-xs font-medium text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationSuggestedConfig') }}
          </div>
          <pre class="mt-1 overflow-x-auto whitespace-pre-wrap break-all font-mono text-xs leading-5 text-foreground">{{ migrationStatus.suggestedConfig }}</pre>
        </div>

        <!-- 完成信息 -->
        <div
          v-if="migrationStatus?.isCompleted"
          class="flex flex-wrap gap-x-5 gap-y-1 border-t border-dashed border-border pt-3 text-xs text-muted-foreground"
        >
          <span>
            {{ $t('page.quartz.systemConfigPage.migrationStartTime') }}
            {{ formatTime(migrationStatus.startTime) }}
          </span>
          <span>
            {{ $t('page.quartz.systemConfigPage.migrationEndTime') }}
            {{ formatTime(migrationStatus.endTime) }}
          </span>
          <span>
            {{ $t('page.quartz.systemConfigPage.migrationDuration') }}
            {{ formatDuration(migrationStatus.durationMs) }}
          </span>
        </div>
      </div>

      <div class="mt-5 flex justify-end gap-2">
        <Button
          v-if="isMigrationRunning"
          danger
          :loading="migrationLoading"
          @click="handleCancelMigration"
        >
          {{ $t('page.quartz.systemConfigPage.migrationCancel') }}
        </Button>
        <Button @click="summaryModalOpen = false">
          {{ $t('page.quartz.systemConfigPage.migrationClose') }}
        </Button>
      </div>
    </Modal>
  </Page>
</template>

<style scoped>
/* ============ 基础信息紧凑布局 ============ */
.compact-form :deep(.ant-form-item) {
  margin-bottom: 12px;
}

.compact-form :deep(.ant-form-item-label) {
  padding-bottom: 4px;
}

/* ============ 数据迁移 ============ */

/* 运行中状态点脉冲 */
.pulse-dot {
  position: relative;
}

.pulse-dot::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 9999px;
  background: #3b82f6;
  animation: pulse 1.6s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

/* 步骤运行中 spinner */
.spin {
  display: inline-block;
  width: 12px;
  height: 12px;
  border: 2px solid rgb(59 130 246 / 0.25);
  border-top-color: #3b82f6;
  border-radius: 9999px;
  animation: rotate 0.7s linear infinite;
}

/* 步骤行：图标 + 连接线 */
.step-row {
  position: relative;
  display: flex;
  gap: 12px;
  padding-bottom: 16px;
}

.step-row:last-child {
  padding-bottom: 0;
}

.step-row:not(:last-child)::before {
  content: '';
  position: absolute;
  left: 6.5px;
  top: 18px;
  bottom: 0;
  width: 1px;
  background: hsl(var(--border));
}

.step-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  flex-shrink: 0;
  margin-top: 3px;
}

/* 折叠动画（grid-rows，高度自适应） */
.collapse-grid {
  display: grid;
  grid-template-rows: 1fr;
}

.collapse-inner {
  min-height: 0;
  overflow: hidden;
}

.expand-enter-active,
.expand-leave-active {
  transition:
    grid-template-rows 0.3s ease,
    opacity 0.25s ease;
}

.expand-enter-from,
.expand-leave-to {
  grid-template-rows: 0fr;
  opacity: 0;
}

@keyframes pulse {
  0% {
    transform: scale(1);
    opacity: 0.6;
  }

  70%,
  100% {
    transform: scale(2.6);
    opacity: 0;
  }
}

@keyframes rotate {
  to {
    transform: rotate(360deg);
  }
}
</style>