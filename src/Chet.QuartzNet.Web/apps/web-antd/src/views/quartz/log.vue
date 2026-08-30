<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import dayjs from 'dayjs';
import { Page } from '@vben/common-ui';
// 导入 vbenadmin 的 Vxe Table 适配器
import { useVbenVxeGrid } from '@vben/plugins/vxe-table';
import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
import {
  Button,
  Descriptions,
  DescriptionsItem,
  Modal,
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
  clearLogs,
} from '../../api/quartz/log';
import type { LogQueryParams, LogResponseDto } from '../../api/quartz/log';
// 导入可拖动 Modal 组合式函数
import { useDraggableModal } from './composables/use-draggable-modal';

const { locale } = useI18n();

// 日志状态映射
const logStatusMap = {
  [LogStatusEnum.SUCCESS]: { text: () => $t('page.quartz.logPage.statusSuccess'), status: 'success' },
  [LogStatusEnum.ERROR]: { text: () => $t('page.quartz.logPage.statusError'), status: 'error' },
  [LogStatusEnum.RUNNING]: { text: () => $t('page.quartz.logPage.statusRunning'), status: 'processing' },
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

// 详情顶部状态条颜色：成功绿 / 错误红 / 运行中蓝
const logStatusColor = computed(() => {
  const status = logDetail.value?.status;
  if (status === LogStatusEnum.SUCCESS) return '#52c41a';
  if (status === LogStatusEnum.ERROR) return '#ff4d4f';
  return '#1890ff';
});

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
    width: 70,
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
        const params = {
          jobName: currentValues?.jobName,
          jobGroup: currentValues?.jobGroup,
          status: currentValues?.status,
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
              return {
                result: response.data.items,
                page: {
                  total: response.data.totalCount || 0,
                },
              };
            }
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
          options: [
            { label: $t('page.quartz.logPage.statusSuccess'), value: LogStatusEnum.SUCCESS },
            { label: $t('page.quartz.logPage.statusError'), value: LogStatusEnum.ERROR },
            { label: $t('page.quartz.logPage.statusRunning'), value: LogStatusEnum.RUNNING },
          ],
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
        options: [
          { label: $t('page.quartz.logPage.statusSuccess'), value: LogStatusEnum.SUCCESS },
          { label: $t('page.quartz.logPage.statusError'), value: LogStatusEnum.ERROR },
          { label: $t('page.quartz.logPage.statusRunning'), value: LogStatusEnum.RUNNING },
        ],
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
    content: $t('page.quartz.logPage.confirmClearContent'),
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
const handleDetail = (log: LogResponseDto) => {
  try {
    logDetail.value = log;
    detailModalVisible.value = true;
  } catch (error) {
    message.error($t('page.quartz.logPage.showDetailFailed'));
    console.log($t('page.quartz.logPage.showDetailFailed'), error);
  }
};

// 恢复表格排序视觉状态（列头箭头）
onMounted(async () => {
  // 恢复搜索条件到表单（日期范围从 ISO 字符串反序列化为 Day.js 对象）
  if (savedSearch) {
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
  await gridApi.query();
  // 数据加载后恢复排序视觉状态
  await nextTick();
  try {
    const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
    if (saved) {
      gridApi.grid?.setSort({ field: saved.field, order: saved.order });
    }
  } catch {}
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <!-- 日志列表 -->
      <Grid>
        <!-- 工具栏：清空日志按钮 -->
        <template #toolbar-actions>
          <div class="flex w-full items-center justify-end">
            <Button danger @click="handleClear">{{ $t('page.quartz.logPage.clearLogs') }}</Button>
          </div>
        </template>

        <!-- 日志状态 -->
        <template #status="{ row }">
          <Tag :color="logStatusMap[row.status as LogStatusEnum]?.status || 'default'">
            {{ logStatusMap[row.status as LogStatusEnum]?.text?.() || $t('page.quartz.logPage.unknown') }}
          </Tag>
        </template>

        <!-- 通用日期时间渲染 -->
        <template #datetime="{ row, column }">
          {{ (row as any)[column.field] ? formatDateTime((row as any)[column.field]) : '-' }}
        </template>

        <!-- 执行时长 -->
        <template #duration="{ row }">
          {{ formatDuration(row.duration) }}
        </template>

        <!-- 操作列 -->
        <template #action="{ row }">
          <div class="flex items-center justify-center gap-1">
            <Tooltip :title="$t('page.quartz.logPage.detail')">
              <i class="vxe-icon-info-circle-fill text-primary cursor-pointer hover:opacity-80 px-1" @click="handleDetail(row)"></i>
            </Tooltip>
          </div>
        </template>
      </Grid>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="$t('page.quartz.logPage.logDetail')" width="800px" :footer="null"
        :destroyOnClose="true" centered wrapClassName="quartz-log-detail-modal">
        <div v-if="logDetail" class="log-detail">
          <!-- 顶部：标题 + 状态标签 -->
          <div class="detail-header">
            <span class="header-title">{{ logDetail.jobName }} · {{ logDetail.jobGroup }}</span>
            <Tag :color="logStatusMap[logDetail.status].status">
              {{ logStatusMap[logDetail.status].text() }}
            </Tag>
          </div>

          <!-- 元数据：Descriptions 组件统一展示 -->
          <Descriptions :column="3" size="small" bordered class="detail-desc" :labelStyle="{ minWidth: '100px' }">
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
  word-break: break-all;
}

/* Descriptions 元数据 */
.detail-desc {
  margin-bottom: var(--space-lg);
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

.mb-4 {
  margin-bottom: 16px;
}
</style>