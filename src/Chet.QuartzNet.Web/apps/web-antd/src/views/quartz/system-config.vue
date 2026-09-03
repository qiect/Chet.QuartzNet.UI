<script lang="ts" setup>
import { ref, computed, watch, onUnmounted } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Card,
  message,
  Progress,
  Button,
  Tag,
  Tooltip,
  Alert,
  Input,
  Select,
  Form,
  FormItem,
} from 'ant-design-vue';

import { $t } from '#/locales';
import { useI18n } from '@vben/locales';

import {
  getSystemConfig,
  saveSystemConfig,
  getMigrationStatus,
  triggerMigration,
  MigrationStepStatus,
} from '../../api/quartz/system-config';
import type { DataMigrationStatusDto } from '../../api/quartz/system-config';
import { useSystemConfig } from '../../composables/use-system-config';

const { locale } = useI18n();

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

const migrationStatus = ref<DataMigrationStatusDto | null>(null);
const migrationLoading = ref(false);
const showDetail = ref(false);
let migrationPollTimer: ReturnType<typeof setInterval> | null = null;

const stepTagColorMap: Record<number, string> = {
  [MigrationStepStatus.Pending]: 'default',
  [MigrationStepStatus.Running]: 'processing',
  [MigrationStepStatus.Completed]: 'success',
  [MigrationStepStatus.Failed]: 'error',
  [MigrationStepStatus.Skipped]: 'warning',
};

const stepStatusKeyMap: Record<number, string> = {
  [MigrationStepStatus.Pending]: 'migrationStepPending',
  [MigrationStepStatus.Running]: 'migrationStepRunning',
  [MigrationStepStatus.Completed]: 'migrationStepCompleted',
  [MigrationStepStatus.Failed]: 'migrationStepFailed',
  [MigrationStepStatus.Skipped]: 'migrationStepSkipped',
};

const overallStatusText = computed(() => {
  const s = migrationStatus.value;
  if (!s) return $t('page.quartz.systemConfigPage.migrationIdle');
  if (s.isRunning) return $t('page.quartz.systemConfigPage.migrationRunning');
  if (s.isCompleted && s.isSuccess) return $t('page.quartz.systemConfigPage.migrationCompleted');
  if (s.isCompleted && !s.isSuccess) return $t('page.quartz.systemConfigPage.migrationFailed');
  return $t('page.quartz.systemConfigPage.migrationIdle');
});

const overallStatusColor = computed(() => {
  const s = migrationStatus.value;
  if (!s) return 'default';
  if (s.isRunning) return 'processing';
  if (s.isCompleted && s.isSuccess) return 'success';
  if (s.isCompleted && !s.isSuccess) return 'error';
  return 'default';
});

const progressStatus = computed(() => {
  const s = migrationStatus.value;
  if (!s) return 'normal' as const;
  if (s.isCompleted && s.isSuccess) return 'success' as const;
  if (s.isCompleted && !s.isSuccess) return 'exception' as const;
  if (s.isRunning) return 'active' as const;
  return 'normal' as const;
});

const canTrigger = computed(() => {
  const s = migrationStatus.value;
  if (!s) return true;
  return !s.isRunning;
});

function formatDuration(ms?: number): string {
  if (ms === undefined || ms === null) return '-';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function getStepDotColor(status: number): string {
  const map: Record<number, string> = {
    [MigrationStepStatus.Running]: 'bg-blue-500',
    [MigrationStepStatus.Completed]: 'bg-emerald-500',
    [MigrationStepStatus.Failed]: 'bg-red-500',
    [MigrationStepStatus.Skipped]: 'bg-amber-400',
  };
  return map[status] || 'bg-gray-300';
}

function getStepLineColor(status: number): string {
  const map: Record<number, string> = {
    [MigrationStepStatus.Running]: 'border-blue-300',
    [MigrationStepStatus.Completed]: 'border-emerald-300',
    [MigrationStepStatus.Failed]: 'border-red-300',
    [MigrationStepStatus.Skipped]: 'border-amber-300',
  };
  return map[status] || 'border-gray-200';
}

async function loadMigrationStatus() {
  try {
    const response = await getMigrationStatus();
    if (response.success && response.data) {
      migrationStatus.value = response.data;
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
      showDetail.value = true;
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
  loadMigrationStatus();
  migrationPollTimer = setInterval(async () => {
    await loadMigrationStatus();
    if (migrationStatus.value?.isCompleted) {
      stopPolling();
    }
  }, 1000);
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

loadMigrationStatus();
</script>

<template>
  <Page content-class="flex flex-col gap-4">
    <!-- 基础信息 -->
    <Card :title="$t('page.quartz.systemConfigPage.basicSection')">
      <div class="text-muted-foreground text-sm mb-4">
        {{ $t('page.quartz.systemConfigPage.description') }}
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <!-- 服务名称 -->
        <div class="rounded-lg border border-gray-150 bg-gray-50/60 p-3">
          <div class="flex items-center gap-2 text-xs text-muted-foreground mb-2">
            {{ $t('page.quartz.systemConfigPage.serviceName') }}
          </div>
          <Input
            v-model:value="serviceName"
            :placeholder="$t('page.quartz.systemConfigPage.serviceNamePlaceholder')"
            allow-clear
            size="middle"
          />
        </div>

        <!-- 运行环境 -->
        <div class="rounded-lg border border-gray-150 bg-gray-50/60 p-3">
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-2 text-xs text-muted-foreground">
              {{ $t('page.quartz.systemConfigPage.environment') }}
            </div>
            <Tag :color="envTagColor" size="small" class="text-xs">{{ environment }}</Tag>
          </div>
          <Select
            v-model:value="environment"
            :options="environmentOptions"
            :placeholder="$t('page.quartz.systemConfigPage.environmentPlaceholder')"
            size="middle"
            class="w-full"
          />
        </div>
      </div>

      <!-- 服务描述 -->
      <div class="rounded-lg border border-gray-150 bg-gray-50/60 p-3 mb-4">
        <div class="flex items-center gap-2 text-xs text-muted-foreground mb-2">
          {{ $t('page.quartz.systemConfigPage.serviceDescription') }}
        </div>
        <Input.TextArea
          v-model:value="serviceDescription"
          :placeholder="$t('page.quartz.systemConfigPage.serviceDescriptionPlaceholder')"
          :rows="3"
          :maxlength="200"
          show-count
          size="middle"
        />
      </div>

      <!-- 操作按钮 -->
      <div class="flex gap-2">
        <Button type="primary" :loading="saveLoading" @click="handleSave">
          {{ $t('page.quartz.systemConfigPage.save') }}
        </Button>
        <Button @click="handleReset">
          {{ $t('page.quartz.systemConfigPage.reset') }}
        </Button>
      </div>
    </Card>

    <!-- 数据迁移 -->
    <Card :title="$t('page.quartz.systemConfigPage.migrationSection')">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">
        <div class="rounded-lg border border-gray-150 bg-gray-50/60 p-3">
          <div class="flex items-center gap-2 text-xs text-muted-foreground mb-1.5">
            {{ $t('page.quartz.systemConfigPage.migrationFileStoragePath') }}
          </div>
          <div class="flex items-center gap-2">
            <code class="text-sm text-foreground break-all">{{ migrationStatus?.fileStoragePath || '-' }}</code>
            <Tag
              v-if="migrationStatus"
              :color="migrationStatus.fileStoragePathExists ? 'success' : 'error'"
              class="shrink-0"
            >
              {{
                migrationStatus.fileStoragePathExists
                  ? $t('page.quartz.systemConfigPage.migrationFileStorageExists')
                  : $t('page.quartz.systemConfigPage.migrationFileStorageNotExists')
              }}
            </Tag>
          </div>
        </div>
        <div class="rounded-lg border border-gray-150 bg-gray-50/60 p-3">
          <div class="flex items-center gap-2 text-xs text-muted-foreground mb-1.5">
            {{ $t('page.quartz.systemConfigPage.migrationStorageType') }}
          </div>
          <div class="flex items-center gap-2">
            <Tag color="blue" class="text-sm">{{ migrationStatus?.storageType || '-' }}</Tag>
          </div>
        </div>
      </div>

      <div v-if="migrationStatus && !migrationStatus.fileStoragePathExists" class="mb-4">
        <Alert :message="$t('page.quartz.systemConfigPage.migrationPathNotExist')" type="warning" show-icon />
      </div>
      <div v-if="migrationStatus && migrationStatus.storageType !== 'Database'" class="mb-4">
        <Alert :message="$t('page.quartz.systemConfigPage.migrationNotDatabase')" type="warning" show-icon />
      </div>

      <div class="mb-4">
        <div class="flex items-center justify-between mb-2">
          <Tag :color="overallStatusColor" class="text-sm">
            {{ overallStatusText }}
          </Tag>
          <span
            v-if="migrationStatus?.isCompleted && migrationStatus?.durationMs !== undefined && migrationStatus?.durationMs !== null"
            class="text-xs text-muted-foreground"
          >
            {{ formatDuration(migrationStatus.durationMs) }}
          </span>
        </div>
        <Progress
          :percent="migrationStatus?.progressPercent ?? 0"
          :status="progressStatus"
          :stroke-width="10"
          :format="(percent?: number) => `${percent ?? 0}%`"
        />
      </div>

      <div v-if="migrationStatus?.currentStep && migrationStatus?.isRunning" class="mb-4">
        <Alert
          :message="`${$t('page.quartz.systemConfigPage.migrationCurrentStep')}: ${migrationStatus.currentStep}`"
          type="info"
          show-icon
        />
      </div>

      <div v-if="migrationStatus?.isCompleted && !migrationStatus?.isSuccess && migrationStatus?.errorMessage" class="mb-4">
        <Alert :message="migrationStatus.errorMessage" type="error" show-icon />
      </div>

      <div class="mb-4">
        <div
          class="flex items-center gap-1.5 text-sm text-muted-foreground cursor-pointer select-none hover:text-foreground transition-colors"
          @click="showDetail = !showDetail"
        >
          <span
            class="text-xs transition-transform duration-200"
            :style="{ display: 'inline-block', transform: showDetail ? 'rotate(0deg)' : 'rotate(-90deg)' }"
          >▼</span>
          {{ $t('page.quartz.systemConfigPage.migrationSummary') }}
        </div>

        <Transition name="slide">
          <div v-if="showDetail" class="mt-3 pl-1">
            <div v-if="!migrationStatus?.steps?.length" class="text-muted-foreground text-sm py-2">
              {{ $t('page.quartz.systemConfigPage.migrationNoSteps') }}
            </div>

            <div class="relative">
              <div
                v-for="(step, idx) in migrationStatus?.steps"
                :key="step.key"
                class="relative pb-4 last:pb-0"
              >
                <div
                  v-if="idx < (migrationStatus?.steps?.length ?? 0) - 1"
                  class="absolute left-[7px] top-[22px] bottom-0 w-px border-l-2"
                  :class="getStepLineColor(step.status)"
                />
                <div class="flex items-start gap-3">
                  <div
                    class="w-[15px] h-[15px] rounded-full mt-0.5 shrink-0"
                    :class="getStepDotColor(step.status)"
                  />
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 flex-wrap">
                      <span class="font-medium text-sm">{{ step.name }}</span>
                      <Tag :color="stepTagColorMap[step.status] || 'default'" size="small">
                        {{ $t(`page.quartz.systemConfigPage.${stepStatusKeyMap[step.status]}`) }}
                      </Tag>
                    </div>
                    <div v-if="step.status !== MigrationStepStatus.Pending" class="mt-1 flex items-center gap-3 text-xs text-muted-foreground">
                      <span>
                        {{ $t('page.quartz.systemConfigPage.migrationMigrated') }}
                        <span class="text-emerald-600 font-semibold">{{ step.migratedCount }}</span>
                        / {{ step.totalCount }}
                      </span>
                      <span v-if="step.skippedCount > 0" class="text-amber-600">
                        {{ $t('page.quartz.systemConfigPage.migrationSkipped') }} {{ step.skippedCount }}
                      </span>
                    </div>
                    <div v-if="step.status === MigrationStepStatus.Failed && step.errorMessage" class="mt-1 text-xs text-red-500">
                      {{ step.errorMessage }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div
              v-if="migrationStatus?.isCompleted"
              class="mt-3 pt-3 border-t border-dashed border-gray-200 flex items-center gap-4 text-xs text-muted-foreground flex-wrap"
            >
              <span v-if="migrationStatus.startTime">
                {{ $t('page.quartz.systemConfigPage.migrationStartTime') }} {{ new Date(migrationStatus.startTime).toLocaleString() }}
              </span>
              <span v-if="migrationStatus.endTime">
                {{ $t('page.quartz.systemConfigPage.migrationEndTime') }} {{ new Date(migrationStatus.endTime).toLocaleString() }}
              </span>
              <span v-if="migrationStatus.durationMs !== undefined && migrationStatus.durationMs !== null">
                {{ $t('page.quartz.systemConfigPage.migrationDuration') }} {{ formatDuration(migrationStatus.durationMs) }}
              </span>
            </div>
          </div>
        </Transition>
      </div>

      <div class="flex gap-2">
        <Button
          type="primary"
          :loading="migrationLoading || migrationStatus?.isRunning"
          :disabled="!canTrigger || !migrationStatus?.fileStoragePathExists || migrationStatus?.storageType !== 'Database'"
          @click="handleTriggerMigration(false)"
        >
          {{ migrationStatus?.isRunning ? $t('page.quartz.systemConfigPage.migrationTriggering') : $t('page.quartz.systemConfigPage.migrationTrigger') }}
        </Button>
        <Tooltip
          v-if="migrationStatus?.isCompleted && migrationStatus?.isSuccess"
          :title="$t('page.quartz.systemConfigPage.migrationTriggerForce')"
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
    </Card>
  </Page>
</template>

<style scoped>
.slide-enter-active,
.slide-leave-active {
  transition: all 0.25s ease;
  overflow: hidden;
}
.slide-enter-from,
.slide-leave-to {
  opacity: 0;
  max-height: 0;
  margin-top: 0;
}
.slide-enter-to,
.slide-leave-from {
  opacity: 1;
  max-height: 500px;
}
</style>