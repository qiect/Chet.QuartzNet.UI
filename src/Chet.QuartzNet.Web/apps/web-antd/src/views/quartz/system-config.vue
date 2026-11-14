<script lang="ts" setup>
import { computed, h, onUnmounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';
import { formatDateTime } from '@vben/utils';

import {
  Button,
  Card,
  Checkbox,
  Drawer,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popover,
  Segmented,
  Select,
  Steps,
  Switch,
  Tag,
} from 'ant-design-vue';

import { $t } from '#/locales';

import {
  cancelMigration,
  getMigrationOptions,
  getMigrationStatus,
  getSystemConfig,
  MigrationStepStatus,
  previewMigration,
  saveSystemConfig,
  triggerMigration,
} from '../../api/quartz/system-config';
import type {
  CatchUpStrategy,
  DataMigrationStatusDto,
  MigrationEntity,
  MigrationOptionsDto,
  MigrationPreviewDto,
  MigrationStepInfo,
  MigrationStoreDescriptor,
  MigrationStoreType,
  MigrationWriteMode,
  ShutdownMode,
  SystemConfigDto,
} from '../../api/quartz/system-config';
import { useSystemConfig } from '../../composables/use-system-config';
import SettingRow from './components/SettingRow.vue';

const configLoading = ref(false);
const saveLoading = ref(false);

// ============ 分区导航 ============
const activeTab = ref<string>('recovery');
const tabOptions = computed(() => [
  {
    label: $t('page.quartz.systemConfigPage.recoverySection'),
    value: 'recovery',
  },
  {
    label: $t('page.quartz.systemConfigPage.migrationSection'),
    value: 'migration',
  },
]);

// ============ 服务身份 ============
const serviceName = ref('');
const environment = ref('DEV');
const serviceDescription = ref('');

interface EnvVisual {
  dotClass: string;
  labelKey: string;
  pillClass: string;
}

const ENV_VISUALS: Record<string, EnvVisual> = {
  DEV: {
    labelKey: 'envDEV',
    dotClass: 'bg-slate-400',
    pillClass: 'bg-slate-500/10 text-slate-600 dark:text-slate-300',
  },
  TEST: {
    labelKey: 'envTEST',
    dotClass: 'bg-blue-500',
    pillClass: 'bg-blue-500/10 text-blue-600 dark:text-blue-400',
  },
  UAT: {
    labelKey: 'envUAT',
    dotClass: 'bg-amber-500',
    pillClass: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  },
  PROD: {
    labelKey: 'envPROD',
    dotClass: 'bg-red-500',
    pillClass: 'bg-red-500/10 text-red-600 dark:text-red-400',
  },
};

const ENV_FALLBACK: EnvVisual = ENV_VISUALS.DEV!;

// 环境标识：身份条与下拉选项共用同一套配色，保证两处始终一致
function envVisual(value?: string): EnvVisual {
  return (value && ENV_VISUALS[value]) || ENV_FALLBACK;
}

const environmentMeta = computed(() => envVisual(environment.value));
const environmentLabel = computed(
  () =>
    `${$t(`page.quartz.systemConfigPage.${environmentMeta.value.labelKey}`)} (${environment.value})`,
);

const environmentOptions = computed(() =>
  Object.keys(ENV_VISUALS).map((value) => {
    const meta = envVisual(value);
    return {
      value,
      label: h('span', { class: 'inline-flex items-center gap-2' }, [
        h('span', {
          class: `inline-block h-2 w-2 shrink-0 rounded-full ${meta.dotClass}`,
        }),
        h(
          'span',
          { class: meta.pillClass.replace(/bg-[^\s]+/, '') },
          `${$t(`page.quartz.systemConfigPage.${meta.labelKey}`)} (${value})`,
        ),
      ]),
    };
  }),
);

// 身份信息走独立弹窗（草稿态），避免与表单区的未保存修改互相污染
const identityModalOpen = ref(false);
const identitySaving = ref(false);
const identityDraft = ref({
  serviceName: '',
  environment: 'DEV',
  serviceDescription: '',
});

// ============ 日志清理（编辑入口已迁移到日志管理，此处仅原样透传） ============
const logSettings = ref({
  logRetentionDays: 0,
  logMaxCount: 0,
  logCleanupIntervalHours: 6,
});

// ============ 停机与恢复（启动期配置） ============
const shutdownMode = ref<ShutdownMode>('Drain');
const shutdownWaitTimeoutSeconds = ref(30);
const drainTimeoutSeconds = ref(60);
const recoveryEnabled = ref(true);
const compensationDelaySeconds = ref(30);
const heartbeatIntervalSeconds = ref(15);
const leaseDurationSeconds = ref(60);
const skipIfNextFireWithinSeconds = ref(120);
const catchUpStrategy = ref<CatchUpStrategy>('Latest');
const maxRecoveryAttempts = ref(3);
const defaultRerunOnRecovery = ref(true);
const interruptedLogRetentionDays = ref(30);

// 启动期配置状态
const restartRequired = ref(false);
const effectiveConfig = ref<null | SystemConfigDto>(null);
const baseline = ref<null | SystemConfigDto>(null);

// 补偿关闭或「只标记不补偿」时，补跑相关参数无效
const catchUpOptionsVisible = computed(
  () => recoveryEnabled.value && catchUpStrategy.value !== 'None',
);

// 租约必须不小于心跳间隔的两倍，实时提示而不是保存时才报错
const leaseMinSeconds = computed(
  () => (heartbeatIntervalSeconds.value || 0) * 2,
);
const leaseInvalid = computed(
  () => leaseMinSeconds.value > (leaseDurationSeconds.value || 0),
);
const leaseRequirement = computed(() =>
  $t('page.quartz.systemConfigPage.leaseRequirement', {
    seconds: leaseMinSeconds.value,
  }),
);

const advancedDisabledHint = computed(() => {
  if (!recoveryEnabled.value) {
    return $t('page.quartz.systemConfigPage.recoveryDisabledHint');
  }
  if (catchUpStrategy.value === 'None') {
    return $t('page.quartz.systemConfigPage.advancedDisabledHint');
  }
  return '';
});

const shutdownModeOptions = computed<
  Array<{ label: string; value: ShutdownMode }>
>(() => [
  {
    label: $t('page.quartz.systemConfigPage.shutdownModeImmediate'),
    value: 'Immediate',
  },
  {
    label: $t('page.quartz.systemConfigPage.shutdownModeDrain'),
    value: 'Drain',
  },
]);

const catchUpStrategyOptions = computed<
  Array<{ label: string; value: CatchUpStrategy }>
>(() => [
  {
    label: $t('page.quartz.systemConfigPage.catchUpNone'),
    value: 'None',
  },
  {
    label: $t('page.quartz.systemConfigPage.catchUpLatest'),
    value: 'Latest',
  },
  {
    label: $t('page.quartz.systemConfigPage.catchUpAll'),
    value: 'All',
  },
]);

// 停机模式与补偿策略的差异本身就是决策依据，用带说明的卡片平铺，而不是下拉 + Tooltip
const shutdownModeCards = computed(() =>
  shutdownModeOptions.value.map((option) => ({
    ...option,
    desc: $t(`page.quartz.systemConfigPage.shutdownMode${option.value}Desc`),
  })),
);

const catchUpCards = computed(() =>
  catchUpStrategyOptions.value.map((option) => ({
    ...option,
    desc: $t(`page.quartz.systemConfigPage.catchUp${option.value}Desc`),
  })),
);

// 「排水后停止」的超时：立即停止模式不需要，非立即模式统一使用排水超时
const shutdownTimeoutModel = computed<number | undefined>({
  get() {
    return drainTimeoutSeconds.value;
  },
  set(value) {
    drainTimeoutSeconds.value = value ?? 0;
  },
});

const shutdownTimeoutLabel = computed(() =>
  $t('page.quartz.systemConfigPage.drainTimeout'),
);

const shutdownTimeoutHint = computed(() =>
  $t('page.quartz.systemConfigPage.hintDrainTimeout'),
);

// 启动期字段与展示名，用于「重启后生效的变化」对比
const STARTUP_FIELD_META: Array<{
  key: keyof SystemConfigDto;
  labelKey: string;
}> = [
  { key: 'shutdownMode', labelKey: 'shutdownMode' },
  { key: 'drainTimeoutSeconds', labelKey: 'drainTimeout' },
  { key: 'recoveryEnabled', labelKey: 'recoveryEnabled' },
  { key: 'catchUpStrategy', labelKey: 'catchUpStrategy' },
  { key: 'compensationDelaySeconds', labelKey: 'compensationDelay' },
  { key: 'heartbeatIntervalSeconds', labelKey: 'heartbeatInterval' },
  { key: 'leaseDurationSeconds', labelKey: 'leaseDuration' },
  { key: 'skipIfNextFireWithinSeconds', labelKey: 'skipIfNextFire' },
  { key: 'maxRecoveryAttempts', labelKey: 'maxRecoveryAttempts' },
  { key: 'defaultRerunOnRecovery', labelKey: 'defaultRerunOnRecovery' },
  {
    key: 'interruptedLogRetentionDays',
    labelKey: 'interruptedLogRetentionDays',
  },
];

const STARTUP_FIELD_KEYS = new Set<string>(
  STARTUP_FIELD_META.map((item) => item.key as string),
);

function isSameValue(a: unknown, b: unknown): boolean {
  return (a ?? null) === (b ?? null);
}

const buildConfig = (): SystemConfigDto => ({
  serviceName: serviceName.value,
  environment: environment.value,
  serviceDescription: serviceDescription.value,
  logRetentionDays: logSettings.value.logRetentionDays ?? 0,
  logMaxCount: logSettings.value.logMaxCount ?? 0,
  logCleanupIntervalHours: logSettings.value.logCleanupIntervalHours ?? 6,
  shutdownMode: shutdownMode.value,
  shutdownWaitTimeoutSeconds: shutdownWaitTimeoutSeconds.value,
  drainTimeoutSeconds: drainTimeoutSeconds.value,
  recoveryEnabled: recoveryEnabled.value,
  compensationDelaySeconds: compensationDelaySeconds.value,
  heartbeatIntervalSeconds: heartbeatIntervalSeconds.value,
  leaseDurationSeconds: leaseDurationSeconds.value,
  skipIfNextFireWithinSeconds: skipIfNextFireWithinSeconds.value,
  catchUpStrategy: catchUpStrategy.value,
  maxRecoveryAttempts: maxRecoveryAttempts.value,
  defaultRerunOnRecovery: defaultRerunOnRecovery.value,
  interruptedLogRetentionDays: interruptedLogRetentionDays.value,
});

// 未保存的字段（与上次加载/保存的持久化值比较）
const dirtyFields = computed<Array<keyof SystemConfigDto>>(() => {
  const base = baseline.value;
  if (!base) return [];
  const current = buildConfig();
  return STARTUP_FIELD_META.map((item) => item.key).filter(
    (key) => !isSameValue(current[key], base[key]),
  );
});

const dirtyCount = computed(() => dirtyFields.value.length);

const startupDirty = computed(() =>
  dirtyFields.value.some((key) => STARTUP_FIELD_KEYS.has(key as string)),
);

function formatStartupValue(
  key: keyof SystemConfigDto,
  value: unknown,
): string {
  if (value === null || value === undefined) return '-';
  if (key === 'shutdownMode') {
    return (
      shutdownModeOptions.value.find((item) => item.value === value)?.label ??
      String(value)
    );
  }
  if (key === 'catchUpStrategy') {
    return (
      catchUpStrategyOptions.value.find((item) => item.value === value)
        ?.label ?? String(value)
    );
  }
  if (typeof value === 'boolean') {
    return value
      ? $t('page.quartz.systemConfigPage.rerunEnabled')
      : $t('page.quartz.systemConfigPage.rerunDisabled');
  }
  if (String(key).endsWith('Seconds')) {
    return `${value}${$t('page.quartz.systemConfigPage.unitSeconds')}`;
  }
  if (String(key).endsWith('Days')) {
    return `${value}${$t('page.quartz.systemConfigPage.unitDays')}`;
  }
  if (key === 'maxRecoveryAttempts') {
    return `${value}${$t('page.quartz.systemConfigPage.unitTimes')}`;
  }
  return String(value);
}

// 保存并重启后会发生变化的启动期字段（与当前进程生效值比较）
const startupChanges = computed(() => {
  const eff = effectiveConfig.value;
  if (!eff) return [];
  const current = buildConfig();
  return STARTUP_FIELD_META.filter(({ key }) => {
    const before = eff[key];
    if (before === null || before === undefined) return false;
    return !isSameValue(current[key], before);
  }).map(({ key, labelKey }) => ({
    key: key as string,
    label: $t(`page.quartz.systemConfigPage.${labelKey}`),
    from: formatStartupValue(key, eff[key]),
    to: formatStartupValue(key, current[key]),
  }));
});

function applyConfigData(data: any) {
  serviceName.value = data?.serviceName || '';
  environment.value = data?.environment || 'DEV';
  serviceDescription.value = data?.serviceDescription || '';
  logSettings.value = {
    logRetentionDays: data?.logRetentionDays ?? 0,
    logMaxCount: data?.logMaxCount ?? 0,
    logCleanupIntervalHours: data?.logCleanupIntervalHours ?? 6,
  };

  // 历史「Wait（等待完成）」配置等价于「排水后停止」，加载时归一化
  shutdownMode.value =
    (data?.shutdownMode as ShutdownMode) === 'Immediate' ? 'Immediate' : 'Drain';
  shutdownWaitTimeoutSeconds.value = data?.shutdownWaitTimeoutSeconds ?? 30;
  drainTimeoutSeconds.value = data?.drainTimeoutSeconds ?? 60;
  recoveryEnabled.value = data?.recoveryEnabled ?? true;
  compensationDelaySeconds.value = data?.compensationDelaySeconds ?? 30;
  heartbeatIntervalSeconds.value = data?.heartbeatIntervalSeconds ?? 15;
  leaseDurationSeconds.value = data?.leaseDurationSeconds ?? 60;
  skipIfNextFireWithinSeconds.value = data?.skipIfNextFireWithinSeconds ?? 120;
  catchUpStrategy.value = (data?.catchUpStrategy as CatchUpStrategy) || 'Latest';
  maxRecoveryAttempts.value = data?.maxRecoveryAttempts ?? 3;
  defaultRerunOnRecovery.value = data?.defaultRerunOnRecovery ?? true;
  interruptedLogRetentionDays.value = data?.interruptedLogRetentionDays ?? 30;

  effectiveConfig.value = data?.effectiveConfig ?? null;
  restartRequired.value = data?.restartRequired === true;
  // 基线必须在所有字段赋值之后生成，用于未保存改动判定
  baseline.value = buildConfig();
}

async function loadConfig() {
  configLoading.value = true;
  try {
    const response = (await getSystemConfig()) as any;
    applyConfigData(response?.data ?? response);
  } catch (error) {
    message.error($t('page.quartz.systemConfigPage.getConfigFailed'));
    console.error($t('page.quartz.systemConfigPage.getConfigFailed'), error);
  } finally {
    configLoading.value = false;
  }
}

function openIdentityModal() {
  identityDraft.value = {
    serviceName: serviceName.value,
    environment: environment.value,
    serviceDescription: serviceDescription.value,
  };
  identityModalOpen.value = true;
}

// 身份信息单独保存：其余字段沿用最近一次持久化值，不带上表单区的未保存修改
async function saveIdentity() {
  const name = identityDraft.value.serviceName.trim();
  if (!name) {
    message.warning($t('page.quartz.systemConfigPage.serviceNameRequired'));
    return;
  }

  identitySaving.value = true;
  try {
    const payload: SystemConfigDto = {
      ...(baseline.value ?? buildConfig()),
      serviceName: name,
      environment: identityDraft.value.environment,
      serviceDescription: identityDraft.value.serviceDescription,
    };
    const response = await saveSystemConfig(payload);
    if (!response.success) {
      message.error(
        response.message || $t('page.quartz.systemConfigPage.saveFailed'),
      );
      return;
    }

    serviceName.value = name;
    environment.value = identityDraft.value.environment;
    serviceDescription.value = identityDraft.value.serviceDescription;
    if (baseline.value) {
      baseline.value = {
        ...baseline.value,
        serviceName: name,
        environment: identityDraft.value.environment,
        serviceDescription: identityDraft.value.serviceDescription,
      };
    }

    const { systemConfig } = useSystemConfig();
    systemConfig.value = {
      serviceName: name,
      environment: identityDraft.value.environment,
      serviceDescription: identityDraft.value.serviceDescription,
    };

    identityModalOpen.value = false;
    message.success(
      response.message || $t('page.quartz.systemConfigPage.saveSuccess'),
    );
  } catch (error: any) {
    message.error(
      error.message || $t('page.quartz.systemConfigPage.saveFailed'),
    );
    console.error($t('page.quartz.systemConfigPage.saveFailed'), error);
  } finally {
    identitySaving.value = false;
  }
}

async function saveConfig() {
  saveLoading.value = true;
  try {
    const response = await saveSystemConfig(buildConfig());
    if (response.success) {
      message.success(
        response.message || $t('page.quartz.systemConfigPage.saveRestartHint'),
      );
      await loadConfig();
      const { systemConfig } = useSystemConfig();
      systemConfig.value = {
        serviceName: serviceName.value,
        environment: environment.value,
        serviceDescription: serviceDescription.value,
      };
    } else {
      message.error(
        response.message || $t('page.quartz.systemConfigPage.saveFailed'),
      );
    }
  } catch (error: any) {
    message.error(
      error.message || $t('page.quartz.systemConfigPage.saveFailed'),
    );
    console.error($t('page.quartz.systemConfigPage.saveFailed'), error);
  } finally {
    saveLoading.value = false;
  }
}

function validateConfig() {
  if (!serviceName.value.trim()) {
    message.warning($t('page.quartz.systemConfigPage.serviceNameRequired'));
    return false;
  }

  if (leaseInvalid.value) {
    message.warning($t('page.quartz.systemConfigPage.leaseInvalid'));
    return false;
  }

  return true;
}

// 全页只有一个保存入口：仅在涉及启动期配置或「全部补偿」时才二次确认
async function handleSave() {
  if (!validateConfig()) return;

  const contents: string[] = [];
  if (startupDirty.value) {
    contents.push($t('page.quartz.systemConfigPage.confirmSaveContent'));
  }
  if (recoveryEnabled.value && catchUpStrategy.value === 'All') {
    contents.push($t('page.quartz.systemConfigPage.catchUpAllConfirm'));
  }

  if (contents.length > 0) {
    Modal.confirm({
      title: startupDirty.value
        ? $t('page.quartz.systemConfigPage.confirmSaveTitle')
        : $t('page.quartz.systemConfigPage.catchUpStrategy'),
      content: contents.join(' '),
      okText: $t('page.quartz.systemConfigPage.save'),
      onOk: saveConfig,
    });
    return;
  }

  await saveConfig();
}

async function handleReset() {
  await loadConfig();
  message.info($t('page.quartz.systemConfigPage.resetSuccess'));
}

loadConfig();

// ============ 数据迁移 ============

type MigrationState = 'cancelled' | 'failed' | 'idle' | 'running' | 'success';

const migrationStatus = ref<DataMigrationStatusDto | null>(null);
const migrationOptions = ref<MigrationOptionsDto | null>(null);
const migrationPreview = ref<MigrationPreviewDto | null>(null);
const migrationLoading = ref(false);
const previewLoading = ref(false);
const summaryModalOpen = ref(false);

// 迁移向导
const wizardOpen = ref(false);
const wizardStep = ref(0);
const migrationWizardSteps = computed(() => [
  { title: $t('page.quartz.systemConfigPage.migrationStepStore') },
  { title: $t('page.quartz.systemConfigPage.migrationStepOptions') },
  { title: $t('page.quartz.systemConfigPage.migrationStepPreview') },
]);

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
  return (
    !!status &&
    (status.isRunning || status.isCompleted || status.isCancelled || false)
  );
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
  return key ? $t(`page.quartz.systemConfigPage.${key}`) : step.name;
}

interface StorageOption {
  disabled?: boolean;
  label: string;
  title?: string;
  value: string;
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

const availableStoreCount = computed(
  () => storageOptions.value.filter((item) => !item.disabled).length,
);

// 至少需要两种可用存储才能组合出迁移的源和目标
const hasAvailableStores = computed(() => availableStoreCount.value >= 2);

const entityOptions = computed(() => [
  {
    label: $t('page.quartz.systemConfigPage.migrationStepJobs'),
    value: 'Jobs',
  },
  {
    label: $t('page.quartz.systemConfigPage.migrationStepLogs'),
    value: 'Logs',
  },
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

const storeSelectionValid = computed(
  () =>
    !!migrationSource.value &&
    !!migrationTarget.value &&
    migrationSource.value !== migrationTarget.value,
);

const canTrigger = computed(() => {
  return (
    storeSelectionValid.value &&
    migrationEntities.value.length > 0 &&
    !migrationStatus.value?.isRunning
  );
});

const isMigrationRunning = computed(() => !!migrationStatus.value?.isRunning);

// 运行中优先展示进度；否则有预检结果就展示预检，最后回退到历史迁移结果
const showMigrationProgress = computed(
  () => isMigrationRunning.value || (!migrationPreview.value && hasMigrationSummary.value),
);

const migrationWarnings = computed(() => migrationStatus.value?.warnings ?? []);

const migrationErrorSamples = computed(
  () => migrationStatus.value?.errorSamples ?? [],
);

interface StorageVisual {
  dotClass: string;
  pillClass: string;
}

// 存储类型标识：文件用青色、数据库用紫色，与环境色块（灰/蓝/橙/红）区分
const STORAGE_VISUALS: Record<string, StorageVisual> = {
  File: {
    dotClass: 'bg-cyan-500',
    pillClass: 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400',
  },
  Database: {
    dotClass: 'bg-violet-500',
    pillClass: 'bg-violet-500/10 text-violet-600 dark:text-violet-400',
  },
};

const STORAGE_FALLBACK: StorageVisual = {
  dotClass: 'bg-slate-300 dark:bg-slate-600',
  pillClass: 'border border-border/60 text-muted-foreground',
};

const storageVisual = computed<StorageVisual>(() => {
  const type =
    migrationOptions.value?.currentStorageType ??
    migrationStatus.value?.storageType;
  return (type && STORAGE_VISUALS[type]) || STORAGE_FALLBACK;
});

// 当前实际生效的存储模式，兼容选项未加载完成的情况
const currentStorageDisplay = computed(() => {
  const options = migrationOptions.value;
  if (!options) {
    return migrationStatus.value?.storageType || '-';
  }

  if (options.currentStorageType === 'Database') {
    return options.currentDatabaseProvider
      ? `${$t('page.quartz.systemConfigPage.storageDatabase')} · ${options.currentDatabaseProvider}`
      : $t('page.quartz.systemConfigPage.storageDatabase');
  }

  return $t('page.quartz.systemConfigPage.storageFile');
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

function swapStores() {
  const source = migrationSource.value;
  migrationSource.value = migrationTarget.value;
  migrationTarget.value = source;
}

function openMigrationWizard() {
  wizardStep.value = hasMigrationSummary.value ? 2 : 0;
  wizardOpen.value = true;
}

async function loadMigrationStatus() {
  try {
    const response = await getMigrationStatus();
    if (response.success && response.data) {
      migrationStatus.value = response.data;
      if (response.data.isRunning && !migrationPollTimer) {
        startPolling();
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
      // 预检通过后直接进入向导最后一步
      wizardStep.value = 2;
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
      migrationPreview.value = null;
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
loadMigrationStatus();
</script>

<template>
  <!-- 内容区随视口铺满，不再限宽 -->
  <Page content-class="flex w-full flex-col gap-4">
    <!-- 服务身份：一眼看清当前是哪个服务、什么环境、什么存储 -->
    <Card class="identity-card">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-2">
            <span class="truncate text-base font-semibold leading-6">
              {{
                serviceName || $t('page.quartz.systemConfigPage.unnamedService')
              }}
            </span>
            <span
              class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium"
              :class="environmentMeta.pillClass"
            >
              <span
                class="h-1.5 w-1.5 rounded-full"
                :class="environmentMeta.dotClass"
              />
              {{ environmentLabel }}
            </span>
            <span
              class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium"
              :class="storageVisual.pillClass"
            >
              <span
                class="h-1.5 w-1.5 rounded-full"
                :class="storageVisual.dotClass"
              />
              {{ currentStorageDisplay }}
            </span>
            <Tag
              v-if="restartRequired"
              color="warning"
              :bordered="false"
              class="!m-0"
            >
              {{ $t('page.quartz.systemConfigPage.restartRequiredShort') }}
            </Tag>
          </div>
          <div
            class="mt-1.5 line-clamp-2 text-xs leading-5 text-muted-foreground"
          >
            {{
              serviceDescription ||
              $t('page.quartz.systemConfigPage.descriptionEmpty')
            }}
          </div>
        </div>
        <Button size="small" @click="openIdentityModal">
          {{ $t('page.quartz.systemConfigPage.identityEdit') }}
        </Button>
      </div>
    </Card>

    <Segmented
      v-model:value="activeTab"
      :options="tabOptions"
      class="system-config-tabs w-fit"
    />

    <!-- 停机与恢复 -->
    <Card v-if="activeTab === 'recovery'" :loading="configLoading">
      <template #title>
        <div class="flex flex-col">
          <span>{{ $t('page.quartz.systemConfigPage.recoverySection') }}</span>
          <span class="text-xs font-normal leading-4 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.recoveryDescription') }}
          </span>
        </div>
      </template>

      <template #extra>
        <Tag
          v-if="restartRequired"
          color="warning"
          :bordered="false"
          class="!m-0"
        >
          {{ $t('page.quartz.systemConfigPage.restartRequiredShort') }}
        </Tag>
      </template>

      <!-- 重启后生效：一条摘要 + 可展开明细，避免「旧 → 新」铺满一整行 -->
      <div
        v-if="startupChanges.length"
        class="mb-5 flex flex-wrap items-center gap-x-2 gap-y-1 rounded-lg border border-amber-500/30 bg-amber-500/[0.07] px-3 py-2 text-xs"
      >
        <span class="h-1.5 w-1.5 shrink-0 rounded-full bg-amber-500" />
        <span class="text-amber-700 dark:text-amber-300">
          {{
            $t('page.quartz.systemConfigPage.startupChangesCount', {
              count: startupChanges.length,
            })
          }}
        </span>
        <Popover placement="bottomLeft" trigger="click">
          <template #content>
            <div class="max-h-72 min-w-[280px] space-y-1.5 overflow-y-auto">
              <div
                v-for="item in startupChanges"
                :key="item.key"
                class="flex items-baseline justify-between gap-4 text-xs"
              >
                <span class="text-muted-foreground">{{ item.label }}</span>
                <span class="shrink-0 tabular-nums">
                  <span class="text-muted-foreground line-through">
                    {{ item.from }}
                  </span>
                  <span class="mx-1 text-muted-foreground/60">→</span>
                  <span class="font-medium text-amber-600 dark:text-amber-400">
                    {{ item.to }}
                  </span>
                </span>
              </div>
            </div>
          </template>
          <button
            type="button"
            class="ml-auto text-amber-700 underline-offset-2 hover:underline dark:text-amber-300"
          >
            {{ $t('page.quartz.systemConfigPage.startupChangesView') }}
          </button>
        </Popover>
      </div>

      <!-- 停机策略：三种模式的差异直接平铺，超时输入常驻，不因模式切换而消失 -->
      <div class="config-block">
        <div class="config-block__title">
          {{ $t('page.quartz.systemConfigPage.shutdownGroup') }}
        </div>
        <div class="option-cards">
          <button
            v-for="option in shutdownModeCards"
            :key="option.value"
            type="button"
            class="option-card"
            :class="{ 'is-active': shutdownMode === option.value }"
            @click="shutdownMode = option.value"
          >
            <span class="option-card__title">{{ option.label }}</span>
            <span class="option-card__desc">{{ option.desc }}</span>
          </button>
        </div>
        <SettingRow
          v-if="shutdownMode !== 'Immediate'"
          :hint="shutdownTimeoutHint"
          :label="shutdownTimeoutLabel"
        >
          <InputNumber
            v-model:value="shutdownTimeoutModel"
            :min="0"
            :max="3600"
            :addon-after="$t('page.quartz.systemConfigPage.unitSeconds')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
        <p v-else class="text-xs leading-5 text-muted-foreground">
          {{ $t('page.quartz.systemConfigPage.shutdownImmediateNoTimeout') }}
        </p>
      </div>

      <!-- 中断恢复：主开关提到分组标题，策略平铺，从属参数紧随其后 -->
      <div class="config-block">
        <div class="config-block__title">
          {{ $t('page.quartz.systemConfigPage.recoveryGroup') }}
          <span class="config-block__action">
            <span class="text-xs font-normal text-muted-foreground">
              {{ $t('page.quartz.systemConfigPage.recoveryEnabled') }}
            </span>
            <Switch v-model:checked="recoveryEnabled" size="small" />
          </span>
        </div>
        <p
          v-if="advancedDisabledHint"
          class="mb-3 rounded-md bg-muted/40 px-3 py-2 text-xs leading-5 text-muted-foreground"
        >
          {{ advancedDisabledHint }}
        </p>
        <div class="option-cards" :class="{ 'is-disabled': !recoveryEnabled }">
          <button
            v-for="option in catchUpCards"
            :key="option.value"
            type="button"
            class="option-card"
            :class="{ 'is-active': catchUpStrategy === option.value }"
            @click="catchUpStrategy = option.value"
          >
            <span class="option-card__title">{{ option.label }}</span>
            <span class="option-card__desc">{{ option.desc }}</span>
          </button>
        </div>
        <SettingRow
          :disabled="!catchUpOptionsVisible"
          :hint="$t('page.quartz.systemConfigPage.hintCompensationDelay')"
          :label="$t('page.quartz.systemConfigPage.compensationDelay')"
        >
          <InputNumber
            v-model:value="compensationDelaySeconds"
            :min="0"
            :max="3600"
            :disabled="!catchUpOptionsVisible"
            :addon-after="$t('page.quartz.systemConfigPage.unitSeconds')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
        <SettingRow
          :disabled="!catchUpOptionsVisible"
          :hint="$t('page.quartz.systemConfigPage.hintSkipNearFire')"
          :label="$t('page.quartz.systemConfigPage.skipIfNextFire')"
        >
          <InputNumber
            v-model:value="skipIfNextFireWithinSeconds"
            :min="0"
            :max="86400"
            :disabled="!catchUpOptionsVisible"
            :addon-after="$t('page.quartz.systemConfigPage.unitSeconds')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
        <SettingRow
          :disabled="!catchUpOptionsVisible"
          :hint="$t('page.quartz.systemConfigPage.hintMaxAttempts')"
          :label="$t('page.quartz.systemConfigPage.maxRecoveryAttempts')"
        >
          <InputNumber
            v-model:value="maxRecoveryAttempts"
            :min="1"
            :max="10"
            :disabled="!catchUpOptionsVisible"
            :addon-after="$t('page.quartz.systemConfigPage.unitTimes')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
        <SettingRow
          :disabled="!catchUpOptionsVisible"
          :hint="$t('page.quartz.systemConfigPage.hintDefaultRerun')"
          :label="$t('page.quartz.systemConfigPage.defaultRerunOnRecovery')"
        >
          <Switch
            v-model:checked="defaultRerunOnRecovery"
            :disabled="!catchUpOptionsVisible"
          />
        </SettingRow>
        <SettingRow
          :hint="$t('page.quartz.systemConfigPage.hintInterruptedRetention')"
          :label="
            $t('page.quartz.systemConfigPage.interruptedLogRetentionDays')
          "
        >
          <InputNumber
            v-model:value="interruptedLogRetentionDays"
            :min="0"
            :max="3650"
            :addon-after="$t('page.quartz.systemConfigPage.unitDays')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
      </div>

      <!-- 心跳与租约：两条相互约束的参数相邻展示，非法时就地报错 -->
      <div class="config-block">
        <div class="config-block__title">
          {{ $t('page.quartz.systemConfigPage.heartbeatGroup') }}
        </div>
        <SettingRow
          :hint="$t('page.quartz.systemConfigPage.hintHeartbeat')"
          :label="$t('page.quartz.systemConfigPage.heartbeatInterval')"
        >
          <InputNumber
            v-model:value="heartbeatIntervalSeconds"
            :min="5"
            :max="300"
            :addon-after="$t('page.quartz.systemConfigPage.unitSeconds')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
        <SettingRow
          :error="
            leaseInvalid
              ? $t('page.quartz.systemConfigPage.leaseInvalid')
              : ''
          "
          :hint="leaseRequirement"
          :label="$t('page.quartz.systemConfigPage.leaseDuration')"
        >
          <InputNumber
            v-model:value="leaseDurationSeconds"
            :min="10"
            :max="3600"
            :status="leaseInvalid ? 'error' : ''"
            :addon-after="$t('page.quartz.systemConfigPage.unitSeconds')"
            class="w-full"
            size="middle"
          />
        </SettingRow>
      </div>
    </Card>

    <!-- 数据迁移：设置页只保留入口与状态，具体操作进向导 -->
    <Card v-else>
      <template #title>
        <div class="flex flex-col">
          <span>{{ $t('page.quartz.systemConfigPage.migrationSection') }}</span>
          <span class="text-xs font-normal leading-4 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationDescription') }}
          </span>
        </div>
      </template>

      <template #extra>
        <span
          v-if="overallState !== 'idle'"
          class="inline-flex select-none items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium"
          :class="stateConfig[overallState].pill"
        >
          <span
            class="h-1.5 w-1.5 rounded-full"
            :class="[
              stateConfig[overallState].dot,
              overallState === 'running' ? 'pulse-dot' : '',
            ]"
          />
          {{ overallStatusText }}
        </span>
      </template>

      <div class="space-y-4">
        <!-- 存储概览：三段信息平铺，避免整页只有一行文字 -->
        <div class="grid grid-cols-1 gap-3 md:grid-cols-3">
          <div class="stat-card">
            <div class="stat-card__label">
              {{ $t('page.quartz.systemConfigPage.migrationCurrentStorage') }}
            </div>
            <div class="stat-card__value">
              {{ currentStorageDisplay }}
            </div>
          </div>
          <div class="stat-card">
            <div class="stat-card__label">
              {{ $t('page.quartz.systemConfigPage.migrationFileStoragePath') }}
            </div>
            <div class="stat-card__value flex items-center gap-2">
              <span
                class="min-w-0 truncate font-mono text-xs"
                :title="migrationOptions?.fileStoragePath"
              >
                {{ migrationOptions?.fileStoragePath || '-' }}
              </span>
              <Tag
                v-if="migrationOptions"
                :color="
                  migrationOptions.fileStoragePathExists ? 'success' : 'error'
                "
                :bordered="false"
                class="!m-0 shrink-0 !text-[11px]"
              >
                {{
                  migrationOptions.fileStoragePathExists
                    ? $t(
                        'page.quartz.systemConfigPage.migrationFileStorageExists',
                      )
                    : $t(
                        'page.quartz.systemConfigPage.migrationFileStorageNotExists',
                      )
                }}
              </Tag>
            </div>
          </div>
          <div class="stat-card">
            <div class="stat-card__label">
              {{ $t('page.quartz.systemConfigPage.migrationAvailableStores') }}
            </div>
            <div class="stat-card__value">
              {{
                $t('page.quartz.systemConfigPage.migrationAvailableStoresValue', {
                  count: availableStoreCount,
                })
              }}
            </div>
          </div>
        </div>

        <!-- 阻塞项：只有一种存储时无法组合源和目标 -->
        <div
          v-if="!hasAvailableStores"
          class="flex gap-2 rounded-lg bg-amber-500/10 px-3 py-2.5 text-xs leading-5 text-amber-600 dark:text-amber-400"
        >
          <span class="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-amber-500" />
          <span>
            {{ $t('page.quartz.systemConfigPage.migrationNoAvailableStore') }}
          </span>
        </div>

        <div
          v-if="unavailableProviders.length"
          class="flex gap-2 rounded-lg bg-amber-500/10 px-3 py-2.5 text-xs leading-5 text-amber-600 dark:text-amber-400"
        >
          <span class="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-amber-500" />
          <span>
            {{ $t('page.quartz.systemConfigPage.migrationMissingProvider') }}:
            {{ unavailableProviders.join('、') }}
          </span>
        </div>

        <!-- 迁移进度/上次结果：概览进卡片，明细进弹窗 -->
        <div
          v-if="hasMigrationSummary"
          class="rounded-lg border border-border/60 bg-muted/20 px-4 py-3"
        >
          <div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
            <span class="flex items-center gap-2 text-xs">
              <span class="font-medium">
                {{ $t('page.quartz.systemConfigPage.migrationSummary') }}
              </span>
              <span class="tabular-nums text-muted-foreground">
                {{ migrationStatus?.progressPercent ?? 0 }}% ·
                {{ migrationStatus?.processed ?? 0 }}/{{
                  migrationStatus?.total ?? 0
                }}
              </span>
            </span>
            <span
              v-if="
                isMigrationRunning &&
                (migrationStatus?.stage || migrationStatus?.currentEntity)
              "
              class="text-xs text-muted-foreground"
            >
              {{ migrationStatus?.stage }}
              <template v-if="migrationStatus?.currentEntity">
                · {{ migrationStatus?.currentEntity }}
              </template>
            </span>
          </div>
          <div class="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-muted">
            <div
              class="h-full rounded-full bg-blue-500 transition-all duration-300"
              :style="{ width: `${migrationStatus?.progressPercent ?? 0}%` }"
            />
          </div>
          <div
            v-if="migrationStatus?.isCompleted"
            class="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground"
          >
            <span>
              {{ $t('page.quartz.systemConfigPage.migrationStartTime') }}
              {{ formatTime(migrationStatus.startTime) }}
            </span>
            <span>
              {{ $t('page.quartz.systemConfigPage.migrationDuration') }}
              {{ formatDuration(migrationStatus.durationMs) }}
            </span>
          </div>
        </div>

        <!-- 操作：说明在左，按钮在右 -->
        <div
          class="flex flex-wrap items-center justify-between gap-3 border-t border-border/60 pt-4"
        >
          <span class="text-xs leading-5 text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationCardHint') }}
          </span>
          <div class="flex flex-wrap items-center gap-2">
            <Button
              v-if="isMigrationRunning"
              danger
              :loading="migrationLoading"
              @click="handleCancelMigration"
            >
              {{ $t('page.quartz.systemConfigPage.migrationCancel') }}
            </Button>
            <Button
              v-if="hasMigrationSummary"
              @click="summaryModalOpen = true"
            >
              {{ $t('page.quartz.systemConfigPage.migrationViewSummary') }}
            </Button>
            <Button
              type="primary"
              :disabled="!hasAvailableStores"
              @click="openMigrationWizard"
            >
              {{ $t('page.quartz.systemConfigPage.migrationOpenWizard') }}
            </Button>
          </div>
        </div>
      </div>
    </Card>

    <!-- 有未保存修改时才浮出操作条：居中悬浮，避开右下角固定按钮 -->
    <Transition name="save-bar">
      <div
        v-if="activeTab !== 'migration' && dirtyCount > 0"
        class="pointer-events-none sticky bottom-5 z-10 mt-1 flex justify-center"
      >
        <div
          class="pointer-events-auto flex items-center gap-2 rounded-full border border-border/60 bg-card/95 py-1.5 pl-4 pr-1.5 shadow-lg backdrop-blur"
        >
          <span class="flex items-center gap-2 text-xs">
            <span class="h-1.5 w-1.5 rounded-full bg-amber-500" />
            <span class="font-medium text-amber-600 dark:text-amber-400">
              {{
                $t('page.quartz.systemConfigPage.unsavedCount', {
                  count: dirtyCount,
                })
              }}
            </span>
          </span>
          <span class="mx-1.5 h-4 w-px bg-border" />
          <Button
            class="w-[72px]"
            shape="round"
            :disabled="saveLoading"
            @click="handleReset"
          >
            {{ $t('page.quartz.systemConfigPage.reset') }}
          </Button>
          <!-- 固定宽度 + 延迟出现 spinner：快速保存时按钮不发生尺寸/图标突变 -->
          <Button
            class="w-[84px]"
            shape="round"
            type="primary"
            :loading="saveLoading ? { delay: 200 } : false"
            @click="handleSave"
          >
            {{ $t('page.quartz.systemConfigPage.save') }}
          </Button>
        </div>
      </div>
    </Transition>

    <!-- 身份信息编辑 -->
    <Modal
      v-model:open="identityModalOpen"
      :title="$t('page.quartz.systemConfigPage.identityEditTitle')"
      :width="560"
      centered
      :confirm-loading="identitySaving"
      :ok-text="$t('page.quartz.systemConfigPage.save')"
      :cancel-text="$t('page.quartz.systemConfigPage.cancel')"
      @ok="saveIdentity"
    >
      <Form layout="vertical" class="config-form" :colon="false">
        <FormItem
          :label="$t('page.quartz.systemConfigPage.serviceName')"
          :extra="$t('page.quartz.systemConfigPage.description')"
        >
          <Input
            v-model:value="identityDraft.serviceName"
            :placeholder="
              $t('page.quartz.systemConfigPage.serviceNamePlaceholder')
            "
            allow-clear
            size="middle"
          />
        </FormItem>
        <FormItem :label="$t('page.quartz.systemConfigPage.environment')">
          <Select
            v-model:value="identityDraft.environment"
            :options="environmentOptions"
            :placeholder="
              $t('page.quartz.systemConfigPage.environmentPlaceholder')
            "
            size="middle"
            class="w-full"
          />
        </FormItem>
        <FormItem :label="$t('page.quartz.systemConfigPage.serviceDescription')">
          <Input.TextArea
            v-model:value="identityDraft.serviceDescription"
            :placeholder="
              $t('page.quartz.systemConfigPage.serviceDescriptionPlaceholder')
            "
            :auto-size="{ minRows: 2, maxRows: 4 }"
            :maxlength="200"
            show-count
            size="middle"
          />
        </FormItem>
      </Form>
    </Modal>

    <!-- 迁移向导 -->
    <Drawer
      v-model:open="wizardOpen"
      :title="$t('page.quartz.systemConfigPage.migrationTool')"
      :width="760"
      placement="right"
      :body-style="{ padding: '20px 24px' }"
      :footer-style="{ padding: '12px 24px' }"
    >
      <Steps
        :current="wizardStep"
        size="small"
        :items="migrationWizardSteps"
        class="mb-6"
      />

      <!-- 第一步：选择存储，交换按钮放在源与目标之间 -->
      <div v-if="wizardStep === 0" class="space-y-4">
        <div
          class="grid grid-cols-1 items-end gap-3 sm:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)]"
        >
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
          <Button
            shape="circle"
            :disabled="isMigrationRunning"
            :title="$t('page.quartz.systemConfigPage.migrationSwap')"
            @click="swapStores"
          >
            <svg
              class="h-4 w-4"
              fill="none"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="1.6"
              viewBox="0 0 16 16"
            >
              <path d="M2.5 6h9M11.5 6 9 3.5M11.5 6 9 8.5" />
              <path d="M13.5 10h-9M4.5 10 7 7.5M4.5 10 7 12.5" />
            </svg>
          </Button>
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
        <div class="text-xs leading-5 text-muted-foreground">
          {{ $t('page.quartz.systemConfigPage.migrationStoreHint') }}
        </div>
      </div>

      <!-- 第二步：迁移选项 -->
      <div v-else-if="wizardStep === 1" class="space-y-4">
        <div class="space-y-3">
          <div class="sm:max-w-[240px]">
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
              class="migration-entities"
              :value="migrationEntities"
              :options="entityOptions"
              :disabled="isMigrationRunning"
              @change="handleEntitiesChange"
            />
          </div>
        </div>

        <div>
          <div class="mb-1 text-xs font-medium text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationExecutionOptions') }}
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
              {{
                $t('page.quartz.systemConfigPage.migrationAllowUpgradeSource')
              }}
            </Checkbox>
          </div>
        </div>

        <div
          v-if="unavailableProviders.length"
          class="rounded-md bg-amber-500/10 px-3 py-2 text-xs leading-5 text-amber-600 dark:text-amber-400"
        >
          {{ $t('page.quartz.systemConfigPage.migrationMissingProvider') }}:
          {{ unavailableProviders.join('、') }}
        </div>
      </div>

      <!-- 第三步：预检与执行 -->
      <div v-else class="space-y-3">
        <!-- 运行中或已有迁移记录：展示进度 -->
        <template v-if="showMigrationProgress">
          <div class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
            <span
              class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium"
              :class="stateConfig[overallState].pill"
            >
              <span
                class="h-1.5 w-1.5 rounded-full"
                :class="[
                  stateConfig[overallState].dot,
                  overallState === 'running' ? 'pulse-dot' : '',
                ]"
              />
              {{ overallStatusText }}
            </span>
            <span
              v-if="migrationStatus?.total"
              class="text-xs tabular-nums text-muted-foreground"
            >
              {{ migrationStatus?.progressPercent ?? 0 }}% ·
              {{ migrationStatus?.processed ?? 0 }}/{{
                migrationStatus?.total ?? 0
              }}
            </span>
          </div>

          <div class="h-1.5 w-full overflow-hidden rounded-full bg-muted">
            <div
              class="h-full rounded-full bg-blue-500 transition-all duration-300"
              :style="{ width: `${migrationStatus?.progressPercent ?? 0}%` }"
            />
          </div>

          <div
            v-if="
              migrationStatus?.isRunning &&
              (migrationStatus.stage || migrationStatus.currentEntity)
            "
            class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
          >
            <span v-if="migrationStatus.stage">
              {{ $t('page.quartz.systemConfigPage.migrationStage') }}:
              <span class="font-mono text-foreground">
                {{ migrationStatus.stage }}
              </span>
            </span>
            <span v-if="migrationStatus.currentEntity">
              {{ $t('page.quartz.systemConfigPage.migrationCurrentEntity') }}:
              <span class="font-mono text-foreground">
                {{ migrationStatus.currentEntity }}
              </span>
            </span>
          </div>

          <div class="flex flex-wrap items-center gap-2">
            <Button size="small" @click="summaryModalOpen = true">
              {{ $t('page.quartz.systemConfigPage.migrationViewSummary') }}
            </Button>
            <span class="text-xs text-muted-foreground">
              {{ $t('page.quartz.systemConfigPage.migrationRunHint') }}
            </span>
          </div>
        </template>

        <!-- 预检结果 -->
        <template v-else-if="migrationPreview">
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
              <span>
                {{ $t('page.quartz.systemConfigPage.migrationEntities') }}
              </span>
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
              <span
                class="text-right tabular-nums text-emerald-600 dark:text-emerald-400"
              >
                {{ item.willInsert }}
              </span>
              <span
                class="text-right tabular-nums text-blue-600 dark:text-blue-400"
              >
                {{ item.willUpdate }}
              </span>
              <span
                class="text-right tabular-nums"
                :class="
                  item.willSkip > 0
                    ? 'text-amber-600 dark:text-amber-400'
                    : 'text-muted-foreground'
                "
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
        </template>

        <div v-else class="text-xs text-muted-foreground">
          {{ $t('page.quartz.systemConfigPage.migrationPreviewEmpty') }}
        </div>
      </div>

      <template #footer>
        <div class="flex flex-wrap items-center justify-between gap-2">
          <span class="text-xs text-muted-foreground">
            {{ $t('page.quartz.systemConfigPage.migrationCurrentStorage') }}：{{
              currentStorageDisplay
            }}
          </span>
          <div class="flex items-center gap-2">
            <Button
              v-if="wizardStep > 0"
              :disabled="isMigrationRunning"
              @click="wizardStep -= 1"
            >
              {{ $t('page.quartz.systemConfigPage.migrationPrev') }}
            </Button>
            <Button
              v-if="wizardStep === 0"
              type="primary"
              :disabled="!storeSelectionValid"
              @click="wizardStep = 1"
            >
              {{ $t('page.quartz.systemConfigPage.migrationNext') }}
            </Button>
            <Button
              v-else-if="wizardStep === 1"
              type="primary"
              :loading="previewLoading"
              :disabled="!storeSelectionValid || !migrationEntities.length"
              @click="handlePreviewMigration"
            >
              {{
                previewLoading
                  ? $t('page.quartz.systemConfigPage.migrationPreviewing')
                  : $t('page.quartz.systemConfigPage.migrationPreview')
              }}
            </Button>
            <template v-else>
              <Button
                v-if="isMigrationRunning"
                danger
                :loading="migrationLoading"
                @click="handleCancelMigration"
              >
                {{ $t('page.quartz.systemConfigPage.migrationCancel') }}
              </Button>
              <Button
                v-else-if="migrationStatus?.isCompleted && migrationStatus?.isSuccess"
                :loading="migrationLoading"
                @click="handleTriggerMigration(true)"
              >
                {{ $t('page.quartz.systemConfigPage.migrationTriggerForce') }}
              </Button>
              <Button
                v-else
                type="primary"
                :loading="migrationLoading"
                :disabled="!canTrigger"
                @click="handleTriggerMigration(false)"
              >
                {{ $t('page.quartz.systemConfigPage.migrationTrigger') }}
              </Button>
            </template>
            <Button @click="wizardOpen = false">
              {{ $t('page.quartz.systemConfigPage.migrationClose') }}
            </Button>
          </div>
        </div>
      </template>
    </Drawer>

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
              :class="[
                stateConfig[overallState].dot,
                overallState === 'running' ? 'pulse-dot' : '',
              ]"
            />
            {{ overallStatusText }}
          </span>
          <span
            v-if="migrationStatus?.total"
            class="text-xs tabular-nums text-muted-foreground"
          >
            {{ migrationStatus?.progressPercent ?? 0 }}% ·
            {{ migrationStatus?.processed ?? 0 }}/{{
              migrationStatus?.total ?? 0
            }}
          </span>
        </div>

        <div class="h-1.5 w-full overflow-hidden rounded-full bg-muted">
          <div
            class="h-full rounded-full bg-blue-500 transition-all duration-300"
            :style="{ width: `${migrationStatus?.progressPercent ?? 0}%` }"
          />
        </div>

        <div
          v-if="
            migrationStatus?.isRunning &&
            (migrationStatus.stage || migrationStatus.currentEntity)
          "
          class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground"
        >
          <span v-if="migrationStatus.stage">
            {{ $t('page.quartz.systemConfigPage.migrationStage') }}:
            <span class="font-mono text-foreground">
              {{ migrationStatus.stage }}
            </span>
          </span>
          <span v-if="migrationStatus.currentEntity">
            {{ $t('page.quartz.systemConfigPage.migrationCurrentEntity') }}:
            <span class="font-mono text-foreground">
              {{ migrationStatus.currentEntity }}
            </span>
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
              <span
                v-else-if="step.status === MigrationStepStatus.Running"
                class="spin"
              />
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
              <div
                class="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5"
              >
                <span class="truncate text-[13px] font-medium text-foreground">
                  {{ stepName(step) }}
                </span>
                <span
                  class="flex shrink-0 items-center gap-2 text-xs tabular-nums"
                >
                  <span class="font-medium" :class="stepLabelClass(step.status)">
                    {{
                      $t(
                        `page.quartz.systemConfigPage.${stepLabelKey(step.status)}`,
                      )
                    }}
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
                v-if="
                  step.status === MigrationStepStatus.Failed &&
                  step.errorMessage
                "
                class="mt-1 break-all text-xs leading-5 text-red-600 dark:text-red-400"
              >
                {{ step.errorMessage }}
              </div>
            </div>
          </div>
        </div>

        <!-- 失败原因 -->
        <div
          v-if="
            (overallState === 'failed' || overallState === 'cancelled') &&
            migrationStatus?.errorMessage
          "
          class="break-all rounded-md bg-red-500/10 px-3 py-2 text-xs leading-5 text-red-600 dark:text-red-400"
        >
          {{ migrationStatus.errorMessage }}
        </div>

        <!-- 告警 -->
        <div
          v-if="migrationWarnings.length"
          class="rounded-md bg-amber-500/10 px-3 py-2"
        >
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
              {{ sample.entity
              }}<template v-if="sample.field">.{{ sample.field }}</template>
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
          <pre
            class="mt-1 overflow-x-auto whitespace-pre-wrap break-all font-mono text-xs leading-5 text-foreground"
>{{ migrationStatus.suggestedConfig }}</pre>
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
/* ============ 分区导航 ============ */
.system-config-tabs :deep(.ant-segmented-item-label) {
  min-height: 40px;
  padding: 0 28px;
  font-size: 15px;
  font-weight: 500;
  line-height: 40px;
}

/* ============ 迁移选项：数据项按列排布，避免窄栏下参差换行 ============ */
.migration-entities {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 8px 16px;
  min-height: 32px;
  align-content: center;
}

.migration-entities :deep(.ant-checkbox-wrapper) {
  margin-inline-start: 0;
}

/* ============ 分组 ============ */
.config-block + .config-block {
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid hsl(var(--border) / 0.6);
}

.config-block__title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
  font-size: 13px;
  font-weight: 600;
  line-height: 1.4;
  color: hsl(var(--foreground));
}

.config-block__action {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 400;
}

/* ============ 选项卡片：把模式/策略的差异平铺出来 ============ */
.option-cards {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 14px;
}

.option-card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 10px 12px;
  text-align: left;
  cursor: pointer;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  transition:
    border-color 0.15s ease,
    background-color 0.15s ease;
}

.option-card:hover {
  border-color: hsl(var(--primary) / 0.55);
}

.option-card.is-active {
  background: hsl(var(--primary) / 0.06);
  border-color: hsl(var(--primary));
}

.option-card__title {
  font-size: 13px;
  font-weight: 500;
  line-height: 20px;
  color: hsl(var(--foreground));
}

.option-card.is-active .option-card__title {
  color: hsl(var(--primary));
}

.option-card__desc {
  font-size: 12px;
  line-height: 18px;
  color: hsl(var(--muted-foreground));
}

.option-cards.is-disabled {
  pointer-events: none;
  opacity: 0.55;
}

@media (max-width: 900px) {
  .option-cards {
    grid-template-columns: 1fr;
  }
}

/* ============ 表单（仅身份信息弹窗使用） ============ */
.config-form :deep(.ant-form-item) {
  margin-bottom: 18px;
}

.config-form :deep(.ant-form-item-label) {
  text-align: left;
}

.config-form :deep(.ant-form-item-label > label) {
  height: auto;
  font-size: 13px;
  line-height: 1.4;
  text-align: left;
  white-space: normal;
}

.config-form :deep(.ant-form-item-extra) {
  min-height: 0;
  padding-top: 0;
  margin-top: 4px;
  font-size: 12px;
  line-height: 1.5;
  color: hsl(var(--muted-foreground));
}

.config-form :deep(.ant-form-item-control) {
  width: 100%;
}

.config-form :deep(.ant-form-item-control-input-content) {
  width: 100%;
}

/* 单位后缀不换行，与数值输入保持同一视觉层级 */
.config-form :deep(.ant-input-number-group-addon) {
  font-size: 12px;
  white-space: nowrap;
  color: hsl(var(--muted-foreground));
}

/* ============ 数据迁移 ============ */
.stat-card {
  padding: 12px 16px;
  background: hsl(var(--muted) / 0.25);
  border: 1px solid hsl(var(--border) / 0.6);
  border-radius: 10px;
}

.stat-card__label {
  font-size: 12px;
  line-height: 18px;
  color: hsl(var(--muted-foreground));
}

.stat-card__value {
  margin-top: 2px;
  font-size: 14px;
  font-weight: 500;
  line-height: 22px;
  color: hsl(var(--foreground));
}

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

/* ============ 浮动保存条 ============ */
.save-bar-enter-active,
.save-bar-leave-active {
  transition:
    opacity 0.18s ease,
    transform 0.18s ease;
}

.save-bar-enter-from,
.save-bar-leave-to {
  opacity: 0;
  transform: translateY(8px);
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
