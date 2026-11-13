<script setup lang="ts">
import {
  ref,
  computed,
  onActivated,
  onDeactivated,
  onMounted,
  onUnmounted,
  nextTick,
  watch,
} from 'vue';
import { useRoute } from 'vue-router';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import dayjs from 'dayjs';
import { Page } from '@vben/common-ui';
// 导入 vbenadmin 的 Vxe Table 适配器
import { useVbenVxeGrid } from '@vben/plugins/vxe-table';
import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
import {
  Button,
  Checkbox,
  Descriptions,
  DescriptionsItem,
  Modal,
  Popover,
  Tag,
  Tooltip,
  message,
} from 'ant-design-vue';

// 导入i18n
import { $t } from '#/locales';
import { useI18n } from '@vben/locales';

// 导入日志相关类型和API
import {
  LogStatusEnum,
  getLogList,
  getJobLog,
  clearLogs,
  rerunInterruptedLog,
} from '../../api/quartz/log';
import type {
  CompensationStatus,
  LogQueryParams,
  LogResponseDto,
  TriggerType,
} from '../../api/quartz/log';
// 导入可拖动 Modal 组合式函数
import { useDraggableModal } from './composables/use-draggable-modal';

const { locale } = useI18n();
const route = useRoute();

// 与 RecoveryNotice.vue 约定的一次性筛选标记
const LOG_PENDING_FILTER_KEY = 'quartz-log-pending-filter';

// 日志状态映射（列表/筛选/详情共用）
const logStatusMap = {
  [LogStatusEnum.SUCCESS]: { text: () => $t('page.quartz.logPage.statusSuccess'), status: 'success' },
  [LogStatusEnum.ERROR]: { text: () => $t('page.quartz.logPage.statusError'), status: 'error' },
  [LogStatusEnum.RUNNING]: { text: () => $t('page.quartz.logPage.statusRunning'), status: 'processing' },
  [LogStatusEnum.SKIPPED]: { text: () => $t('page.quartz.logPage.statusSkipped'), status: 'warning' },
  [LogStatusEnum.INTERRUPTED]: { text: () => $t('page.quartz.logPage.statusInterrupted'), status: 'magenta' },
};

const statusOptions = computed(() => [
  { label: $t('page.quartz.logPage.statusSuccess'), value: LogStatusEnum.SUCCESS },
  { label: $t('page.quartz.logPage.statusError'), value: LogStatusEnum.ERROR },
  { label: $t('page.quartz.logPage.statusRunning'), value: LogStatusEnum.RUNNING },
  { label: $t('page.quartz.logPage.statusSkipped'), value: LogStatusEnum.SKIPPED },
  { label: $t('page.quartz.logPage.statusInterrupted'), value: LogStatusEnum.INTERRUPTED },
]);

const triggerTypeMap: Record<TriggerType, { text: () => string; color: string }> = {
  Cron: { text: () => $t('page.quartz.logPage.triggerCron'), color: 'default' },
  Manual: { text: () => $t('page.quartz.logPage.triggerManual'), color: 'blue' },
  Recovery: { text: () => $t('page.quartz.logPage.triggerRecovery'), color: 'cyan' },
  Unknown: { text: () => $t('page.quartz.logPage.triggerUnknown'), color: 'default' },
};

const compensationStatusText = (status?: CompensationStatus) => {
  switch (status) {
    case 'Pending':
      return $t('page.quartz.logPage.compensationPending');
    case 'Triggered':
      return $t('page.quartz.logPage.compensationTriggered');
    case 'Skipped':
      return $t('page.quartz.logPage.compensationSkipped');
    case 'Failed':
      return $t('page.quartz.logPage.compensationFailed');
    default:
      return $t('page.quartz.logPage.compensationNone');
  }
};

// 行级重跑 loading / 补偿来源 / 运行中计时
const rerunningIds = ref<Set<string>>(new Set());
const sourceLoading = ref(false);
const sourceDetail = ref<LogResponseDto | null>(null);
const nowTick = ref(Date.now());
const hasRunningRows = ref(false);
// 当前页数据快照，用于只探测运行中行的最终状态，不做整表反复刷新
const currentRows = ref<LogResponseDto[]>([]);
let tickerTimer: ReturnType<typeof setInterval> | null = null;
let statusTimer: ReturnType<typeof setInterval> | null = null;

// 「仅看需要处理」= 同时筛选失败 + 中断两类日志，放在工具栏即时生效
const needsAttentionOnly = ref(false);
const handleNeedsAttentionChange = async () => {
  if (needsAttentionOnly.value) {
    await gridApi.formApi.setValues({ status: undefined });
  }
  await gridApi.query();
};

// 响应式数据

// 详情对话框
const detailModalVisible = ref(false);
const logDetail = ref<LogResponseDto | null>(null);

// 执行时长格式化：根据毫秒数自动选择合适单位（ms/s/min/h）
const formatDuration = (ms?: number | null) => {
  if (ms == null) return '-';
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${parseFloat((ms / 1000).toFixed(2))} s`;
  if (ms < 3_600_000) return `${parseFloat((ms / 60_000).toFixed(2))} min`;
  return `${parseFloat((ms / 3_600_000).toFixed(2))} h`;
};

// 列表耗时展示：运行中按当前时间动态计算
const displayDuration = (row: LogResponseDto) => {
  if (row.status === LogStatusEnum.RUNNING) {
    const start = dayjs(row.startTime).valueOf();
    return formatDuration(Math.max(0, nowTick.value - start));
  }
  return formatDuration(row.duration);
};

// JSON 字段格式化：字符串可能是被转义过的 JSON 字符串，先 parse 一次解层转义，再美化输出
const formatJsonField = (value: any): string => {
  if (value == null) return '';
  let result = value;
  if (typeof value === 'string') {
    try {
      result = JSON.parse(value);
    } catch {
      return value;
    }
  }
  // 兼容历史日志格式：{ JobData: "<json字符串>", IsManualTrigger: "True" }，解包内层 JobData
  if (
    result &&
    typeof result === 'object' &&
    !Array.isArray(result) &&
    typeof result.JobData === 'string'
  ) {
    try {
      result = JSON.parse(result.JobData);
    } catch {
      // 内层不是合法 JSON 时保持原样
    }
  }
  try {
    return JSON.stringify(result, null, 2);
  } catch {
    return String(value);
  }
};

// 搜索条件由 VbenForm 自动注入到 query 的 formValues



// 列配置
const columns = computed(() => [
  { type: 'seq', width: 60, title: '#', fixed: 'left' },
  {
    field: 'jobName',
    title: $t('page.quartz.logPage.jobName'),
    minWidth: 160,
    showOverflow: true,
  },
  {
    field: 'jobGroup',
    title: $t('page.quartz.logPage.jobGroup'),
    minWidth: 120,
    showOverflow: true,
  },
  {
    field: 'status',
    title: $t('page.quartz.logPage.status'),
    width: 100,
    align: 'center' as const,
    slots: { default: 'status' },
  },
  {
    field: 'triggerType',
    title: $t('page.quartz.logPage.triggerSource'),
    width: 130,
    align: 'center' as const,
    slots: { default: 'triggerType' },
  },
  {
    field: 'startTime',
    title: $t('page.quartz.logPage.startTime'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'endTime',
    title: $t('page.quartz.logPage.endTime'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'duration',
    title: $t('page.quartz.logPage.duration'),
    width: 130,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'duration' },
  },
  {
    field: 'action',
    title: $t('page.quartz.logPage.action'),
    width: 110,
    align: 'center' as const,
    fixed: 'right',
    slots: { default: 'action' },
  },
]);

// 排序持久化：读取上次排序列
const SORT_KEY = 'quartz-log-sort';
// 搜索条件持久化 key（保存表单输入值，日期范围用 ISO 字符串保存）
const SEARCH_KEY = 'quartz-log-search';
const savedSort = (() => {
  try {
    const raw = localStorage.getItem(SORT_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();
const savedSearch = (() => {
  try {
    const raw = localStorage.getItem(SEARCH_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();

// 构造 Vxe Grid 配置
const gridOptions: VxeTableGridOptions<LogResponseDto> = {
  id: 'quartz-log-grid',
  columns: columns.value as any,
  height: 'auto',
  showOverflow: true,
  rowConfig: { keyField: 'logId', isHover: true },
  sortConfig: {
    trigger: 'cell',
    remote: true,
    defaultSort: savedSort,
  },
  customConfig: { storage: true },
  columnConfig: { resizable: true },
  pagerConfig: { enabled: true },
  proxyConfig: {
    enabled: true,
    autoLoad: false,
    ajax: {
      query: async ({ page, sort }: any, formValues: any) => {
        // autoLoad 首次 query 时 defaultSort 可能未注入，从 localStorage 兜底
        let sortField = sort?.field;
        let sortOrderRaw = sort?.order;
        if (!sortField) {
          try {
            const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
            if (saved) {
              sortField = saved.field;
              sortOrderRaw = saved.order;
            }
          } catch {}
        }
        // 保持原有行为：sortOrder 使用 asc/desc 形式
        const sortOrder =
          sortOrderRaw === 'asc' ? 'asc' : sortOrderRaw === 'desc' ? 'desc' : '';
        // 主动从 formApi 获取表单值（避开 vxe-table reload 路径下 wrapper 注入 formValues 为空的问题）
        let currentValues: any = formValues || {};
        try {
          const formApiValues = await gridApi.formApi.getValues();
          if (formApiValues && Object.keys(formApiValues).length > 0) {
            currentValues = formApiValues;
          }
        } catch {}
        // RangePicker 返回 Day.js 数组 [begin, end]，拆分为后端范围参数
        // startTimeRange 查 StartTime 字段范围，endTimeRange 查 EndTime 字段范围
        const startTimeRange = currentValues?.startTimeRange;
        const endTimeRange = currentValues?.endTimeRange;
        // 持久化搜索条件（日期范围用 ISO 字符串保存，回填时用 dayjs 反序列化）
        try {
          const persisted: Record<string, any> = {};
          for (const k of ['jobName', 'jobGroup', 'status']) {
            if (currentValues[k] != null && currentValues[k] !== '') {
              persisted[k] = currentValues[k];
            }
          }
          if (Array.isArray(startTimeRange) && startTimeRange.length === 2) {
            persisted.startTimeRange = [
              startTimeRange[0]?.format('YYYY-MM-DDTHH:mm:ss') ?? null,
              startTimeRange[1]?.format('YYYY-MM-DDTHH:mm:ss') ?? null,
            ];
          }
          if (Array.isArray(endTimeRange) && endTimeRange.length === 2) {
            persisted.endTimeRange = [
              endTimeRange[0]?.format('YYYY-MM-DDTHH:mm:ss') ?? null,
              endTimeRange[1]?.format('YYYY-MM-DDTHH:mm:ss') ?? null,
            ];
          }
          localStorage.setItem(SEARCH_KEY, JSON.stringify(persisted));
        } catch {}
        const needsAttention = needsAttentionOnly.value;
        const params = {
          jobName: currentValues?.jobName,
          jobGroup: currentValues?.jobGroup,
          status: needsAttention ? undefined : currentValues?.status,
          statuses: needsAttention
            ? [LogStatusEnum.ERROR, LogStatusEnum.INTERRUPTED]
            : undefined,
          startStartTime: startTimeRange?.[0]?.format('YYYY-MM-DDTHH:mm:ss'),
          endStartTime: startTimeRange?.[1]?.format('YYYY-MM-DDTHH:mm:ss'),
          startEndTime: endTimeRange?.[0]?.format('YYYY-MM-DDTHH:mm:ss'),
          endEndTime: endTimeRange?.[1]?.format('YYYY-MM-DDTHH:mm:ss'),
          pageIndex: page.currentPage || 1,
          pageSize: page.pageSize || 10,
          sortBy: sortField ?? '',
          sortOrder,
        } as LogQueryParams;

        try {
          const response = await getLogList(params);
          if (response.success) {
            // 根据API定义，响应数据应该包含data字段，其中包含items和totalCount，现在还包含totalPages
            if (
              response.data &&
              response.data.items &&
              Array.isArray(response.data.items)
            ) {
              currentRows.value = response.data.items;
              hasRunningRows.value = response.data.items.some(
                (item) => item.status === LogStatusEnum.RUNNING,
              );
              return {
                result: response.data.items,
                page: {
                  total: response.data.totalCount || 0,
                },
              };
            }
            currentRows.value = [];
            hasRunningRows.value = false;
            return { result: [], page: { total: 0 } };
          }
          // 处理错误情况，包括可能的errorCode
          const errorMsg = response.errorCode
            ? `${response.message || $t('page.quartz.logPage.loadListFailed')} (${$t('page.quartz.logPage.errorCode')}: ${response.errorCode})`
            : response.message || $t('page.quartz.logPage.loadListFailed');
          message.error(errorMsg);
          return { result: [], page: { total: 0 } };
        } catch (error) {
          console.log($t('page.quartz.logPage.loadListFailed'), error);
          message.error(
            typeof error === 'object' && error !== null && 'message' in error
              ? String((error as any).message)
              : $t('page.quartz.logPage.loadListFailed'),
          );
          return { result: [], page: { total: 0 } };
        }
      },
    },
    sort: true,
  },
  toolbarConfig: {
    custom: true,
    refresh: true,
    zoom: true,
  },
};

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions,
  formOptions: {
    schema: [
      {
        component: 'Input',
        componentProps: { placeholder: $t('page.quartz.logPage.placeholderJobName') },
        fieldName: 'jobName',
        label: $t('page.quartz.logPage.jobName'),
      },
      {
        component: 'Input',
        componentProps: { placeholder: $t('page.quartz.logPage.placeholderJobGroup') },
        fieldName: 'jobGroup',
        label: $t('page.quartz.logPage.jobGroup'),
      },
      {
        component: 'Select',
        componentProps: {
          allowClear: true,
          placeholder: $t('page.quartz.logPage.placeholderStatus'),
          options: statusOptions.value,
        },
        fieldName: 'status',
        label: $t('page.quartz.logPage.executionStatus'),
      },
      {
        component: 'RangePicker',
        componentProps: { showTime: true },
        fieldName: 'startTimeRange',
        label: $t('page.quartz.logPage.startTime'),
      },
      {
        component: 'RangePicker',
        componentProps: { showTime: true },
        fieldName: 'endTimeRange',
        label: $t('page.quartz.logPage.endTime'),
      },
    ],
    showCollapseButton: true,
    collapsed: true,
    submitOnChange: false,
    submitOnEnter: true,
  },
  gridEvents: {
    sortChange: ({ property, field, order }: any) => {
      const sortField = property || field;
      if (sortField && order) {
        localStorage.setItem(
          SORT_KEY,
          JSON.stringify({ field: sortField, order }),
        );
      } else {
        localStorage.removeItem(SORT_KEY);
      }
    },
  },
});

// 详情对话框支持拖动
useDraggableModal(detailModalVisible, 'quartz-log-detail-modal');

// 监听语言切换，更新表格列头和搜索表单
watch(locale, () => {
  gridApi.setGridOptions({ columns: columns.value as any });
  gridApi.formApi.updateSchema([
    {
      fieldName: 'jobName',
      label: $t('page.quartz.logPage.jobName'),
      componentProps: { placeholder: $t('page.quartz.logPage.placeholderJobName') },
    },
    {
      fieldName: 'jobGroup',
      label: $t('page.quartz.logPage.jobGroup'),
      componentProps: { placeholder: $t('page.quartz.logPage.placeholderJobGroup') },
    },
    {
      fieldName: 'status',
      label: $t('page.quartz.logPage.executionStatus'),
      componentProps: {
        allowClear: true,
        placeholder: $t('page.quartz.logPage.placeholderStatus'),
        options: statusOptions.value,
      },
    },
    {
      fieldName: 'startTimeRange',
      label: $t('page.quartz.logPage.startTime'),
    },
    {
      fieldName: 'endTimeRange',
      label: $t('page.quartz.logPage.endTime'),
    },
  ]);
});

// 搜索/重置由 VbenForm 内置提交按钮触发，无需手动处理

// 清空日志
const handleClear = () => {
  Modal.confirm({
    title: $t('page.quartz.logPage.confirmClear'),
    content: `${$t('page.quartz.logPage.confirmClearContent')} ${$t('page.quartz.logPage.clearInterruptHint')}`,
    onOk: async () => {
      try {
        // 传递空的查询参数，清空所有日志，而不是使用当前搜索条件
        const response = await clearLogs({
          jobName: '',
          jobGroup: '',
          status: undefined,
          startTime: undefined,
          endTime: undefined,
        });
        if (response.success) {
          message.success($t('page.quartz.logPage.clearSuccess'));
          // 清空后重新加载日志列表
          await gridApi.query();
        } else {
          message.error(response.message || $t('page.quartz.logPage.clearFailed'));
        }
      } catch (error: any) {
        console.error($t('page.quartz.logPage.clearFailed'), error);
        message.error(error.message || $t('page.quartz.logPage.clearFailed'));
      }
    },
  });
};

// 查看详情
const handleDetail = async (log: LogResponseDto) => {
  try {
    logDetail.value = log;
    detailModalVisible.value = true;

    // 补偿记录：按需拉取源记录的开始时间，用于「补偿自 ...」说明
    if (log.triggerType === 'Recovery' && log.recoverySourceLogId) {
      try {
        const response = await getJobLog(log.recoverySourceLogId);
        if (
          response.success &&
          response.data &&
          logDetail.value?.logId === log.logId
        ) {
          logDetail.value = {
            ...log,
            recoverySourceStartTime: response.data.startTime,
          };
        }
      } catch {
        // 源记录不可用时保留原样
      }
    }
  } catch (error) {
    message.error($t('page.quartz.logPage.showDetailFailed'));
    console.log($t('page.quartz.logPage.showDetailFailed'), error);
  }
};

// 重新执行
const isRerunning = (logId: string) => rerunningIds.value.has(logId);

const handleRerun = (row: LogResponseDto) => {
  Modal.confirm({
    title: $t('page.quartz.logPage.confirmRerun'),
    content: `${row.jobName} · ${row.jobGroup}\n${$t('page.quartz.logPage.rerunStartTime')}: ${formatDateTime(row.startTime)}\n${$t('page.quartz.logPage.rerunWarning')}`,
    okText: $t('page.quartz.logPage.rerunOk'),
    cancelText: $t('page.quartz.logPage.cancel'),
    onOk: async () => {
      const next = new Set(rerunningIds.value);
      next.add(row.logId);
      rerunningIds.value = next;
      try {
        const response = await rerunInterruptedLog(row.logId);
        if (response.success) {
          message.success($t('page.quartz.logPage.rerunSubmitted'));
        } else {
          message.warning(
            response.message || $t('page.quartz.logPage.rerunFailed'),
          );
        }
        await gridApi.query();
      } catch (error: any) {
        message.error(error?.message || $t('page.quartz.logPage.rerunFailed'));
      } finally {
        const current = new Set(rerunningIds.value);
        current.delete(row.logId);
        rerunningIds.value = current;
      }
    },
  });
};

// 补偿来源详情：点击角标时按需加载
const loadSourceDetail = async (row: LogResponseDto) => {
  const sourceLogId = row.recoverySourceLogId;
  if (!sourceLogId) return;
  sourceLoading.value = true;
  sourceDetail.value = null;
  try {
    const response = await getJobLog(sourceLogId);
    if (response.success && response.data) {
      sourceDetail.value = response.data;
    }
  } catch {
    sourceDetail.value = null;
  } finally {
    sourceLoading.value = false;
  }
};

// 运行中耗时：本地每秒计算，不请求后端
const startTicker = () => {
  if (tickerTimer) return;
  tickerTimer = setInterval(() => {
    nowTick.value = Date.now();
  }, 1000);
};
const stopTicker = () => {
  if (tickerTimer) {
    clearInterval(tickerTimer);
    tickerTimer = null;
  }
};

// 只探测当前页运行中行的最终状态；有行结束/失败时才整表刷新一次，
// 避免运行中期间反复重新加载列表，也避免完成后一直停留在「运行中」。
let checkingRunning = false;
async function checkRunningRows() {
  if (checkingRunning || document.visibilityState !== 'visible') return;
  const running = currentRows.value.filter(
    (item) => item.status === LogStatusEnum.RUNNING,
  );
  if (running.length === 0) {
    stopStatusPolling();
    return;
  }

  checkingRunning = true;
  try {
    const results = await Promise.all(
      running.map((row) => getJobLog(row.logId).catch(() => null)),
    );
    const changed = results.some(
      (res) =>
        res?.success &&
        res.data &&
        res.data.status !== LogStatusEnum.RUNNING,
    );
    if (changed) {
      await gridApi.query();
    }
  } catch {
    // 单次探测失败不影响下一轮
  } finally {
    checkingRunning = false;
  }
}

const startStatusPolling = () => {
  if (statusTimer) return;
  statusTimer = setInterval(checkRunningRows, 5000);
};
const stopStatusPolling = () => {
  if (statusTimer) {
    clearInterval(statusTimer);
    statusTimer = null;
  }
};
const stopLiveUpdates = () => {
  stopTicker();
  stopStatusPolling();
};

watch(hasRunningRows, (hasRunning) => {
  if (hasRunning) {
    startTicker();
    startStatusPolling();
  } else {
    stopLiveUpdates();
  }
});

onDeactivated(stopLiveUpdates);
onUnmounted(stopLiveUpdates);

// 启动提示 / 深链筛选：优先消费一次性标记，兼容旧的 URL query
const applyRouteFilter = async (): Promise<boolean> => {
  const pending = sessionStorage.getItem(LOG_PENDING_FILTER_KEY);
  const query = route.query;
  const hasStatus = typeof query.status === 'string' && query.status !== '';
  const needsAttention =
    pending === 'needsAttention' ||
    query.needsAttention === '1' ||
    query.needsAttention === 'true';
  if (!hasStatus && !needsAttention) {
    return false;
  }

  if (pending) {
    sessionStorage.removeItem(LOG_PENDING_FILTER_KEY);
  }

  const values: Record<string, any> = {};
  if (needsAttention) {
    needsAttentionOnly.value = true;
    values.status = undefined;
  } else {
    values.status = Number(query.status);
    needsAttentionOnly.value = false;
  }

  await gridApi.formApi.setValues(values);
  await gridApi.query();
  return true;
};

let pageMounted = false;

onActivated(async () => {
  if (!pageMounted) return;
  const applied = await applyRouteFilter();
  if (!applied) {
    // 切回页面时只刷新一次
    await gridApi.query();
  }
  if (hasRunningRows.value) {
    startTicker();
    startStatusPolling();
  }
});

// 恢复表格排序视觉状态（列头箭头）
onMounted(async () => {
  const routeFilterApplied = await applyRouteFilter();
  // 恢复搜索条件到表单（日期范围从 ISO 字符串反序列化为 Day.js 对象）
  if (!routeFilterApplied && savedSearch) {
    try {
      const restored: Record<string, any> = { ...savedSearch };
      if (Array.isArray(savedSearch.startTimeRange)) {
        restored.startTimeRange = savedSearch.startTimeRange.map((s: string) =>
          s ? dayjs(s) : null,
        );
      }
      if (Array.isArray(savedSearch.endTimeRange)) {
        restored.endTimeRange = savedSearch.endTimeRange.map((s: string) =>
          s ? dayjs(s) : null,
        );
      }
      await gridApi.formApi.setValues(restored);
    } catch {}
  }
  // 手动触发首次查询（autoLoad: false，此时 formApi 已回填搜索条件）
  if (!routeFilterApplied) {
    await gridApi.query();
  }
  // 数据加载后恢复排序视觉状态
  await nextTick();
  try {
    const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
    if (saved) {
      gridApi.grid?.setSort({ field: saved.field, order: saved.order });
    }
  } catch {}
  pageMounted = true;
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <!-- 日志列表 -->
      <Grid>
        <!-- 工具栏：快捷筛选 + 清空日志 -->
        <template #toolbar-actions>
          <div class="flex w-full items-center justify-between gap-2">
            <Checkbox
              v-model:checked="needsAttentionOnly"
              class="log-quick-filter"
              @change="handleNeedsAttentionChange"
            >
              <span class="text-xs">{{ $t('page.quartz.logPage.needsAttention') }}</span>
            </Checkbox>
            <Button danger @click="handleClear">{{ $t('page.quartz.logPage.clearLogs') }}</Button>
          </div>
        </template>

        <!-- 日志状态 -->
        <template #status="{ row }">
          <Tag :color="logStatusMap[row.status as LogStatusEnum]?.status || 'default'">
            {{ logStatusMap[row.status as LogStatusEnum]?.text?.() || $t('page.quartz.logPage.unknown') }}
          </Tag>
        </template>

        <!-- 触发来源 -->
        <template #triggerType="{ row }">
          <template v-if="row.triggerType === 'Recovery'">
            <Popover trigger="click" @openChange="(open) => open && loadSourceDetail(row)">
              <template #content>
                <div v-if="sourceLoading" class="text-xs">
                  {{ $t('page.quartz.logPage.sourceLoading') }}
                </div>
                <div v-else-if="sourceDetail" class="text-xs leading-6">
                  <div>
                    {{ $t('page.quartz.logPage.sourceOriginalTime') }}:
                    {{ formatDateTime(sourceDetail.startTime) }}
                  </div>
                  <div>
                    {{ $t('page.quartz.logPage.sourceAttempt') }}:
                    {{ row.recoveryAttempt ?? 2 }}
                  </div>
                  <div v-if="sourceDetail.triggeredBy">
                    {{ $t('page.quartz.logPage.sourceOperator') }}:
                    {{ sourceDetail.triggeredBy }}
                  </div>
                </div>
                <div v-else class="text-xs text-muted-foreground">
                  {{ $t('page.quartz.logPage.sourceUnavailable') }}
                </div>
              </template>
              <Tag color="cyan" class="cursor-pointer">
                {{ $t('page.quartz.logPage.triggerRecovery') }}
              </Tag>
            </Popover>
          </template>
          <Tag v-else-if="row.triggerType === 'Manual'" color="blue">
            {{ $t('page.quartz.logPage.triggerManual') }}
          </Tag>
          <span
            v-else-if="!row.triggerType || row.triggerType === 'Cron'"
            class="text-xs text-muted-foreground"
          >
            {{ $t('page.quartz.logPage.triggerCron') }}
          </span>
          <span v-else class="text-xs text-muted-foreground">—</span>
        </template>

        <!-- 通用日期时间渲染 -->
        <template #datetime="{ row, column }">
          {{ (row as any)[column.field] ? formatDateTime((row as any)[column.field]) : '-' }}
        </template>

        <!-- 执行时长 -->
        <template #duration="{ row }">
          {{ displayDuration(row) }}
        </template>

        <!-- 操作列 -->
        <template #action="{ row }">
          <div class="flex items-center justify-center gap-1">
            <Tooltip :title="$t('page.quartz.logPage.detail')">
              <i class="vxe-icon-info-circle-fill text-primary cursor-pointer hover:opacity-80 px-1" @click="handleDetail(row)"></i>
            </Tooltip>
            <Tooltip
              v-if="row.status === LogStatusEnum.INTERRUPTED"
              :title="$t('page.quartz.logPage.rerun')"
            >
              <i
                class="vxe-icon-refresh text-warning cursor-pointer hover:opacity-80 px-1"
                :class="{ 'pointer-events-none opacity-50': isRerunning(row.logId) }"
                @click="!isRerunning(row.logId) && handleRerun(row)"
              ></i>
            </Tooltip>
          </div>
        </template>
      </Grid>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="$t('page.quartz.logPage.logDetail')" width="800px"
        :body-style="{ padding: '24px' }" :footer="null" :destroyOnClose="true" centered
        wrapClassName="quartz-log-detail-modal">
        <div v-if="logDetail" class="log-detail">
          <!-- 顶部：标题 + 状态标签 -->
          <div class="detail-header">
            <span class="header-title">{{ logDetail.jobName }} · {{ logDetail.jobGroup }}</span>
            <div class="flex items-center gap-2">
              <Tag v-if="logDetail.triggerType === 'Recovery'" color="cyan">
                {{ $t('page.quartz.logPage.triggerRecovery') }}
              </Tag>
              <Tag :color="logStatusMap[logDetail.status]?.status || 'default'">
                {{ logStatusMap[logDetail.status]?.text?.() || $t('page.quartz.logPage.unknown') }}
              </Tag>
            </div>
          </div>

          <!-- 基础元数据 -->
          <Descriptions :column="3" size="small" bordered class="detail-desc">
            <DescriptionsItem :label="$t('page.quartz.logPage.executionDuration')" :span="1">
              {{ formatDuration(logDetail.duration) }}
            </DescriptionsItem>
            <DescriptionsItem :label="$t('page.quartz.logPage.startTime')">
              {{ formatDateTime(logDetail.startTime) }}
            </DescriptionsItem>
            <DescriptionsItem :label="$t('page.quartz.logPage.endTime')">
              {{ logDetail.endTime ? formatDateTime(logDetail.endTime) : '—' }}
            </DescriptionsItem>
          </Descriptions>

          <!-- 恢复信息：仅中断记录或补偿记录展示 -->
          <div
            v-if="logDetail.status === LogStatusEnum.INTERRUPTED || logDetail.triggerType === 'Recovery'"
            class="recovery-panel"
          >
            <div class="recovery-panel__header">
              <span>{{ $t('page.quartz.logPage.recoveryInfo') }}</span>
              <Tag v-if="logDetail.triggerType === 'Recovery'" color="cyan">
                {{ $t('page.quartz.logPage.triggerRecovery') }}
              </Tag>
            </div>
            <div class="recovery-panel__grid">
              <div class="recovery-item">
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.triggerSource') }}
                </span>
                <span class="recovery-item__value">
                  {{ triggerTypeMap[(logDetail.triggerType || 'Unknown') as TriggerType]?.text?.() || $t('page.quartz.logPage.triggerUnknown') }}
                </span>
              </div>
              <div v-if="logDetail.triggerType === 'Recovery'" class="recovery-item">
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.sourceOriginalTime') }}
                </span>
                <span class="recovery-item__value">
                  {{ formatDateTime(logDetail.recoverySourceStartTime || logDetail.startTime) }}
                </span>
              </div>
              <div v-if="logDetail.triggerType === 'Recovery'" class="recovery-item">
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.sourceAttempt') }}
                </span>
                <span class="recovery-item__value">
                  {{ logDetail.recoveryAttempt ?? 2 }}
                </span>
              </div>
              <div v-if="logDetail.heartbeatTime" class="recovery-item">
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.lastHeartbeat') }}
                </span>
                <span class="recovery-item__value">
                  {{ formatDateTime(logDetail.heartbeatTime) }}
                </span>
              </div>
              <div
                v-if="logDetail.status === LogStatusEnum.INTERRUPTED"
                class="recovery-item"
              >
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.compensationStatus') }}
                </span>
                <span class="recovery-item__value">
                  {{ compensationStatusText(logDetail.compensationStatus) }}
                </span>
              </div>
              <div v-if="logDetail.skipReason" class="recovery-item recovery-item--wide">
                <span class="recovery-item__label">
                  {{ $t('page.quartz.logPage.skipReason') }}
                </span>
                <span class="recovery-item__value">{{ logDetail.skipReason }}</span>
              </div>
            </div>
          </div>

          <!-- 内容区 -->
          <div class="detail-body">
            <section class="detail-section">
              <div class="section-title">{{ $t('page.quartz.logPage.executionInfo') }}</div>
              <pre class="code-panel">{{ logDetail.message || $t('page.quartz.logPage.noExecutionInfo') }}</pre>
            </section>

            <section v-if="logDetail.errorMessage" class="detail-section">
              <div class="section-title">
                {{ $t('page.quartz.logPage.errorInfo') }}
                <span class="section-tag section-tag--error">Error</span>
              </div>
              <pre class="code-panel code-panel--error">{{ logDetail.errorMessage }}</pre>
            </section>

            <section v-if="logDetail.exception" class="detail-section">
              <div class="section-title">
                {{ $t('page.quartz.logPage.exceptionInfo') }}
                <span class="section-tag section-tag--error">Exception</span>
              </div>
              <pre class="code-panel code-panel--error">{{ logDetail.exception }}</pre>
            </section>

            <section v-if="logDetail.result" class="detail-section">
              <div class="section-title">
                {{ $t('page.quartz.logPage.executionResult') }}
                <span class="section-tag section-tag--success">Result</span>
              </div>
              <pre class="code-panel">{{ formatJsonField(logDetail.result) }}</pre>
            </section>

            <section v-if="logDetail.jobData" class="detail-section">
              <div class="section-title">{{ $t('page.quartz.logPage.jobData') }}</div>
              <pre class="code-panel">{{ formatJsonField(logDetail.jobData) }}</pre>
            </section>
          </div>

          <!-- 底部按钮 -->
          <div class="detail-footer">
            <Button
              v-if="logDetail.status === LogStatusEnum.INTERRUPTED"
              class="mr-2"
              :loading="isRerunning(logDetail.logId)"
              @click="handleRerun(logDetail)"
            >
              {{ $t('page.quartz.logPage.rerun') }}
            </Button>
            <Button @click="detailModalVisible = false" type="primary">
              {{ $t('page.quartz.logPage.close') }}
            </Button>
          </div>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
/* 工具栏快捷筛选：紧凑勾选，不占用表单行 */
.log-quick-filter {
  white-space: nowrap;
  font-size: 12px;
}

.log-quick-filter :deep(.ant-checkbox-wrapper) {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

/* ============ 详情对话框 ============ */
.log-detail {
  --space-lg: 20px;
}

/* 顶部：标题 + 状态标签 */
.detail-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: var(--space-lg);
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: hsl(var(--foreground));
  line-height: 1.4;
  word-break: break-word;
}

/* 恢复信息面板 */
.recovery-panel {
  margin-bottom: var(--space-lg);
  overflow: hidden;
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.recovery-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 8px 12px;
  border-bottom: 1px solid hsl(var(--border));
  background: hsl(var(--muted) / 0.4);
  font-size: 12px;
  font-weight: 600;
}

.recovery-panel__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 20px;
  padding: 12px;
}

.recovery-item {
  display: flex;
  min-width: 0;
  align-items: baseline;
  gap: 8px;
  font-size: 12px;
  line-height: 1.6;
}

.recovery-item--wide {
  grid-column: 1 / -1;
}

.recovery-item__label {
  flex-shrink: 0;
  color: hsl(var(--muted-foreground));
}

.recovery-item__value {
  color: hsl(var(--foreground));
  word-break: break-all;
}

@media (max-width: 640px) {
  .recovery-panel__grid {
    grid-template-columns: 1fr;
  }
}

/* Descriptions 元数据 */
.detail-desc {
  margin-bottom: var(--space-lg);
}

.detail-desc :deep(.ant-descriptions-item-label) {
  min-width: 100px;
}

/* 内容区 */
.detail-body {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.detail-section {
  min-width: 0;
}

/* section 标题 */
.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin-bottom: 8px;
}

.section-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 1px 7px;
  border-radius: 3px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  line-height: 1.5;
}

.section-tag--error {
  background: rgba(255, 77, 79, 0.1);
  color: #ff4d4f;
}

.section-tag--success {
  background: rgba(82, 196, 26, 0.1);
  color: #52c41a;
}

/* 代码面板 */
.code-panel {
  margin: 0;
  padding: 12px 14px;
  background: hsl(var(--accent) / 0.5);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  color: hsl(var(--foreground));
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12.5px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-x: auto;
  max-height: 360px;
  overflow-y: auto;
}

.code-panel--error {
  border-left: 3px solid #ff4d4f;
  background: rgba(255, 77, 79, 0.04);
}

/* 底部按钮 */
.detail-footer {
  margin-top: var(--space-lg);
  display: flex;
  justify-content: flex-end;
}
</style>