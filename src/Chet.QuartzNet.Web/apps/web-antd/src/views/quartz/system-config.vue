<script lang="ts" setup>
import { ref, computed, onUnmounted } from 'vue';

import { Page } from '@vben/common-ui';
import { formatDateTime } from '@vben/utils';

import {
  Button,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Select,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { $t } from '#/locales';

import {
  getSystemConfig,
  saveSystemConfig,
  getMigrationStatus,
  triggerMigration,
  MigrationStepStatus,
} from '../../api/quartz/system-config';
import type {
  DataMigrationStatusDto,
  MigrationStepInfo,
} from '../../api/quartz/system-config';
import { useSystemConfig } from '../../composables/use-system-config';

const configLoading = ref(false);
const saveLoading = ref(false);

const serviceName = ref('');
const environment = ref('DEV');
const serviceDescription = ref('');

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

type MigrationState = 'idle' | 'running' | 'success' | 'failed';

const migrationStatus = ref<DataMigrationStatusDto | null>(null);
const migrationLoading = ref(false);
const migrationCollapsed = ref(true);
let migrationPollTimer: ReturnType<typeof setInterval> | null = null;

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
  if (s.isCompleted && s.isSuccess) return 'success';
  if (s.isCompleted) return 'failed';
  return 'idle';
});

const overallStatusText = computed(() =>
  $t(`page.quartz.systemConfigPage.${stateConfig[overallState.value].textKey}`),
);

const migrationSteps = computed(() => migrationStatus.value?.steps ?? []);

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

const canTrigger = computed(() => {
  const s = migrationStatus.value;
  return (
    !!s && !s.isRunning && s.fileStoragePathExists && s.storageType === 'Database'
  );
});

const triggerDisabledReason = computed(() => {
  const s = migrationStatus.value;
  if (!s) return '';
  if (!s.fileStoragePathExists) {
    return $t('page.quartz.systemConfigPage.migrationPathNotExist');
  }
  if (s.storageType !== 'Database') {
    return $t('page.quartz.systemConfigPage.migrationNotDatabase');
  }
  return '';
});

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

async function handleTriggerMigration(force: boolean = false) {
  migrationLoading.value = true;
  try {
    const response = await triggerMigration({ force });
    if (response.success) {
      message.success($t('page.quartz.systemConfigPage.migrationTriggerSuccess'));
      migrationCollapsed.value = false;
      startPolling();
    } else {
      message.error(response.message || $t('page.quartz.systemConfigPage.migrationTriggerFailed'));
    }
  } catch (error: any) {
    message.error(error.message || $t('page.quartz.systemConfigPage.migrationTriggerFailed'));
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

      <Form layout="vertical" class="mb-2">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-0">
          <FormItem :label="$t('page.quartz.systemConfigPage.serviceName')">
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
            :rows="3"
            :maxlength="200"
            show-count
            size="middle"
          />
        </FormItem>
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
          </span>
          <span class="pl-[22px] text-xs font-normal leading-4 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationDescription') }}
          </span>
        </div>
      </template>

      <template #extra>
        <span
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
            <!-- 迁移前提 -->
            <div class="space-y-2">
              <div class="flex items-center gap-2 text-sm">
                <svg
                  class="h-4 w-4 shrink-0"
                  :class="migrationStatus?.fileStoragePathExists ? 'text-emerald-500' : 'text-red-500'"
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  viewBox="0 0 16 16"
                >
                  <circle cx="8" cy="8" r="6.2" />
                  <path v-if="migrationStatus?.fileStoragePathExists" d="M5.4 8.3l1.8 1.8 3.4-3.6" />
                  <path v-else d="M5.8 5.8l4.4 4.4M10.2 5.8l-4.4 4.4" />
                </svg>
                <span class="shrink-0 text-muted-foreground">
                  {{ $t('page.quartz.systemConfigPage.migrationFileStoragePath') }}
                </span>
                <code class="min-w-0 flex-1 truncate font-mono text-[13px] text-foreground" :title="migrationStatus?.fileStoragePath">
                  {{ migrationStatus?.fileStoragePath || '-' }}
                </code>
              </div>
              <div class="flex items-center gap-2 text-sm">
                <svg
                  class="h-4 w-4 shrink-0"
                  :class="migrationStatus?.storageType === 'Database' ? 'text-emerald-500' : 'text-red-500'"
                  fill="none"
                  stroke="currentColor"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  viewBox="0 0 16 16"
                >
                  <circle cx="8" cy="8" r="6.2" />
                  <path v-if="migrationStatus?.storageType === 'Database'" d="M5.4 8.3l1.8 1.8 3.4-3.6" />
                  <path v-else d="M5.8 5.8l4.4 4.4M10.2 5.8l-4.4 4.4" />
                </svg>
                <span class="shrink-0 text-muted-foreground">
                  {{ $t('page.quartz.systemConfigPage.migrationStorageType') }}
                </span>
                <span class="font-mono text-[13px] text-foreground">
                  {{ migrationStatus?.storageType || '-' }}
                </span>
              </div>
            </div>

            <!-- 前提不满足时的提示 -->
            <div
              v-if="migrationStatus && !migrationStatus.fileStoragePathExists"
              class="mt-3 rounded-md bg-amber-500/10 px-3 py-2 text-xs leading-5 text-amber-600 dark:text-amber-400"
            >
              {{ $t('page.quartz.systemConfigPage.migrationPathNotExist') }}
            </div>
            <div
              v-if="migrationStatus && migrationStatus.storageType !== 'Database'"
              class="mt-3 rounded-md bg-amber-500/10 px-3 py-2 text-xs leading-5 text-amber-600 dark:text-amber-400"
            >
              {{ $t('page.quartz.systemConfigPage.migrationNotDatabase') }}
            </div>

            <!-- 迁移步骤 -->
            <div class="mt-5">
              <div class="text-xs font-medium text-muted-foreground">
                {{ $t('page.quartz.systemConfigPage.migrationSummary') }}
              </div>

              <div v-if="!migrationSteps.length" class="mt-3 text-[13px] text-muted-foreground">
                {{ $t('page.quartz.systemConfigPage.migrationNoSteps') }}
              </div>

              <div v-else class="mt-3">
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
                        <span v-if="step.status !== MigrationStepStatus.Pending" class="text-muted-foreground">
                          {{ step.migratedCount }}/{{ step.totalCount }}
                          <span
                            v-if="step.skippedCount > 0"
                            class="text-amber-600 dark:text-amber-400"
                          >
                            +{{ step.skippedCount }}
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
            </div>

            <!-- 失败原因 -->
            <div
              v-if="overallState === 'failed' && migrationStatus?.errorMessage"
              class="mt-3 break-all rounded-md bg-red-500/10 px-3 py-2 text-xs leading-5 text-red-600 dark:text-red-400"
            >
              {{ migrationStatus.errorMessage }}
            </div>

            <!-- 完成信息 -->
            <div
              v-if="migrationStatus?.isCompleted"
              class="mt-4 flex flex-wrap gap-x-5 gap-y-1 border-t border-dashed border-border pt-3 text-xs text-muted-foreground"
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

            <!-- 操作 -->
            <div class="mt-4 flex items-center gap-2">
              <Tooltip v-if="!canTrigger && triggerDisabledReason" :title="triggerDisabledReason">
                <span>
                  <Button type="primary" disabled>
                    {{ $t('page.quartz.systemConfigPage.migrationTrigger') }}
                  </Button>
                </span>
              </Tooltip>
              <Button
                v-else
                type="primary"
                :loading="migrationLoading || migrationStatus?.isRunning"
                :disabled="!canTrigger"
                @click="handleTriggerMigration(false)"
              >
                {{
                  migrationStatus?.isRunning
                    ? $t('page.quartz.systemConfigPage.migrationTriggering')
                    : $t('page.quartz.systemConfigPage.migrationTrigger')
                }}
              </Button>

              <Tooltip
                v-if="migrationStatus?.isCompleted && migrationStatus?.isSuccess"
                :title="$t('page.quartz.systemConfigPage.migrationAlreadyCompleted')"
              >
                <Button
                  :loading="migrationLoading"
                  :disabled="migrationStatus?.isRunning"
                  @click="handleTriggerMigration(true)"
                >
                  {{ $t('page.quartz.systemConfigPage.migrationTriggerForce') }}
                </Button>
              </Tooltip>
            </div>
          </div>
        </div>
      </Transition>
    </Card>
  </Page>
</template>

<style scoped>
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