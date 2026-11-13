<script setup lang="ts">
import {
  ref,
  computed,
  onActivated,
  onDeactivated,
  onMounted,
  onUnmounted,
  reactive,
  nextTick,
  watch,
} from 'vue';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import { Page } from '@vben/common-ui';
// 导入 vbenadmin 的 Vxe Table 适配器
import { useVbenVxeGrid } from '@vben/plugins/vxe-table';
import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
import {
  Button,
  Input,
  InputNumber,
  Select,
  Space,
  Modal,
  Form,
  Switch,
  message,
  notification,
  Tag,
  Row,
  Col,
  Dropdown,
  Menu,
  Tooltip,
  Textarea,
  Alert,
} from 'ant-design-vue';
import type { FormInstance } from 'ant-design-vue';
// 导入Cron帮助组件
import CronHelperModal from './components/cron-helper.vue';
// 导入可拖动 Modal 组合式函数
import { useDraggableModal } from './composables/use-draggable-modal';
// 导入表头排序标识恢复逻辑
import { applyGridSortIndicator } from './composables/grid-sort-indicator';

// 导入i18n
import { CircleHelp, CircleX, RotateCw, Zap } from '@vben/icons';
import { $t } from '#/locales';
import { useI18n } from '@vben/locales';

// 导入作业API服务
import {
  JobTypeEnum,
  JobStatusEnum,
  getJobs,
  getJob,
  addJob,
  updateJob,
  deleteJob,
  batchDeleteJob,
  batchPauseJobs,
  batchResumeJobs,
  batchSetEnabled,
  triggerJob,
  pauseJob,
  resumeJob,
} from '../../api/quartz/job';
import type {
  JobDependency,
  QuartzJobDto,
  QuartzJobResponseDto,
  QuartzJobQueryDto,
} from '../../api/quartz/job';
import {
  getSchedulerStatus,
  startScheduler,
  stopScheduler,
  drainScheduler,
  resumeScheduler,
} from '../../api/quartz/scheduler';
import type { SchedulerStatusDto } from '../../api/quartz/scheduler';
import { getJobClasses } from '../../api/quartz/extension';

const { locale } = useI18n();

// 作业类型和状态映射
const jobTypeMap = {
  // 支持数字枚举
  [JobTypeEnum.DLL]: { text: 'DLL', color: 'blue' },
  [JobTypeEnum.API]: { text: 'API', color: 'green' },
  // 支持字符串类型
  DLL: { text: 'DLL', color: 'blue' },
  API: { text: 'API', color: 'green' },
};

const jobStatusMap = {
  [JobStatusEnum.Normal]: { text: () => $t('page.quartz.jobPage.statusNormal'), status: 'success' },
  [JobStatusEnum.Paused]: { text: () => $t('page.quartz.jobPage.statusPaused'), status: 'error' },
  [JobStatusEnum.Completed]: { text: () => $t('page.quartz.jobPage.statusCompleted'), status: 'default' },
  [JobStatusEnum.Error]: { text: () => $t('page.quartz.jobPage.statusError'), status: 'error' },
  [JobStatusEnum.Blocked]: { text: () => $t('page.quartz.jobPage.statusBlocked'), status: 'warning' },
};

// 响应式数据
const loading = ref(false);
const toggleLoadingKeys = ref<Set<string>>(new Set());
// 批量操作相关
const selectedRows = ref<QuartzJobResponseDto[]>([]);

// 调度器状态
const schedulerStatus = ref<SchedulerStatusDto>({
  schedulerName: '',
  schedulerInstanceId: '',
  runtimeInstanceId: '',
  isStarted: false,
  isShutdown: true,
  inStandbyMode: false,
  status: '未知',
  jobCount: 0,
  executingJobCount: 0,
  threadPoolSize: 0,
  version: '',
  startTime: undefined,
  runningTime: 0,
  isDraining: false,
  remainingExecutingCount: 0,
});

// 排水期间轮询
let drainPollTimer: ReturnType<typeof setInterval> | null = null;
const stopDrainPolling = () => {
  if (drainPollTimer) {
    clearInterval(drainPollTimer);
    drainPollTimer = null;
  }
};
const startDrainPolling = () => {
  if (drainPollTimer) return;
  drainPollTimer = setInterval(async () => {
    const wasDraining = schedulerStatus.value.isDraining;
    await getSchedulerStatusInfo();
    if (!schedulerStatus.value.isDraining) {
      stopDrainPolling();
      if (wasDraining) {
        message.info($t('page.quartz.jobPage.drainCompleted'));
      }
      await gridApi.query();
    }
  }, 3000);
};
onDeactivated(stopDrainPolling);
onUnmounted(stopDrainPolling);

const drainBadgeText = computed(() => {
  const remaining =
    schedulerStatus.value.remainingExecutingCount ??
    schedulerStatus.value.executingJobCount ??
    0;
  return remaining > 0
    ? $t('page.quartz.jobPage.draining', { count: remaining })
    : $t('page.quartz.jobPage.drainingFinishing');
});

// 调度器操作态：按钮 loading，同时避免重复提交
const schedulerAction = ref<'drain' | 'resume' | 'start' | 'stop' | null>(null);
const isSchedulerBusy = computed(() => schedulerAction.value !== null);

const executingCount = computed(
  () =>
    schedulerStatus.value.remainingExecutingCount ??
    schedulerStatus.value.executingJobCount ??
    0,
);

const schedulerStateClass = computed(() =>
  schedulerStatus.value.isDraining
    ? 'is-draining'
    : schedulerStatus.value.isStarted
      ? 'is-running'
      : 'is-stopped',
);

// 编辑对话框
const editModalVisible = ref(false);
const editModalTitle = ref('add');
const editForm = reactive<QuartzJobDto & { apiUrl: string; jobClassName: string }>({
  jobName: '',
  jobGroup: '',
  jobType: JobTypeEnum.DLL,
  jobClassOrApi: '',
  // 作业类名与 API 地址在表单中各自独立，切换作业类型时不会互相带值
  jobClassName: '',
  apiUrl: '',
  cronExpression: '',
  description: '',
  jobData: '',
  apiMethod: 'GET',
  apiHeaders: '',
  apiBody: '',
  apiTimeout: 60,
  retryCount: 0,
  retryIntervalSeconds: 30,
  skipSslValidation: false,
  disallowConcurrentExecution: false,
  rerunOnRecovery: null,
  dependencies: [],
  startTime: undefined,
  endTime: undefined,
  isEnabled: true,
});

const formRef = ref<FormInstance>();

// 前置依赖可选项
const DEPENDENCY_SEPARATOR = '::';
const dependencyOptions = ref<Array<{ label: string; value: string }>>([]);

// 加载前置依赖可选项（当前作业不能依赖自身）
const loadDependencyOptions = async () => {
  try {
    const response = await getJobs({ pageIndex: 1, pageSize: 1000 });
    dependencyOptions.value = (response.data?.items || []).map((job) => ({
      label: `${job.jobGroup} / ${job.jobName}`,
      value: `${job.jobGroup}${DEPENDENCY_SEPARATOR}${job.jobName}`,
    }));
  } catch {
    dependencyOptions.value = [];
  }
};

const dependencySelectOptions = computed(() =>
  dependencyOptions.value.filter(
    (option) =>
      option.value !==
      `${editForm.jobGroup}${DEPENDENCY_SEPARATOR}${editForm.jobName}`,
  ),
);

// 表单依赖（对象列表）与下拉选择（字符串键）之间的双向映射
const dependencyKeys = computed<string[]>({
  get: () =>
    (editForm.dependencies || []).map(
      (item) => `${item.jobGroup}${DEPENDENCY_SEPARATOR}${item.jobName}`,
    ),
  set: (keys) => {
    editForm.dependencies = (keys || []).map((key): JobDependency => {
      const index = key.indexOf(DEPENDENCY_SEPARATOR);
      return {
        jobGroup: key.slice(0, index),
        jobName: key.slice(index + DEPENDENCY_SEPARATOR.length),
      };
    });
  },
});

// 作业类列表
const jobClasses = ref<string[]>([]);

// 加载作业类列表
const loadJobClasses = async () => {
  try {
    const response = await getJobClasses();
    if (response.success && response.data) {
      jobClasses.value = response.data;
    }
  } catch (error) {
    console.error($t('page.quartz.jobPage.jobClassesFailed'), error);
    message.error($t('page.quartz.jobPage.jobClassesFailed'));
  }
};

// Cron帮助模态框控制
const cronHelperVisible = ref(false);

// 打开Cron帮助
const openCronHelper = () => {
  cronHelperVisible.value = true;
};

// 关闭Cron帮助
const closeCronHelper = () => {
  cronHelperVisible.value = false;
};

// 选择Cron表达式
const selectCronExpression = (expression: string) => {
  editForm.cronExpression = expression;
  closeCronHelper();
};

// 作业类型变化事件处理
const handleJobTypeChange = async (jobType: JobTypeEnum) => {
  if (jobType === JobTypeEnum.DLL) {
    await loadJobClasses();
  }
};

// 缓存最近一次搜索条件（由 VbenForm 自动注入到 query 的 formValues）

// 计算编辑模态框标题
const editModalDisplayTitle = computed(() => {
  if (editModalTitle.value === 'edit') return $t('page.quartz.jobPage.editJob');
  if (editModalTitle.value === 'copy') return $t('page.quartz.jobPage.copyJob');
  return $t('page.quartz.jobPage.addJob');
});

// 判断是否为编辑模式
const isEditMode = computed(() => editModalTitle.value === 'edit');

// 三态开关：Select 用字符串值承载 null，避免 null 被当成未选择
const rerunOnRecoveryValue = computed<string>({
  get: () => {
    if (editForm.rerunOnRecovery === null || editForm.rerunOnRecovery === undefined) {
      return 'global';
    }
    return editForm.rerunOnRecovery ? 'on' : 'off';
  },
  set: (value) => {
    editForm.rerunOnRecovery =
      value === 'global' ? null : value === 'on';
  },
});

// 列表「自动重跑」图标三态
const rerunIconInfo = (row: QuartzJobResponseDto) => {
  const effective = row.effectiveRerunOnRecovery;
  if (effective === undefined || effective === null) {
    return {
      icon: CircleHelp,
      cls: 'text-muted-foreground',
      tip: $t('page.quartz.jobPage.rerunUnknown'),
    };
  }
  if (effective) {
    return {
      icon: Zap,
      cls: 'text-success',
      tip:
        row.rerunOnRecovery == null
          ? $t('page.quartz.jobPage.rerunAllowedGlobal')
          : $t('page.quartz.jobPage.rerunAllowedJob'),
    };
  }
  return {
    icon: CircleX,
    cls: 'text-muted-foreground',
    tip:
      row.rerunOnRecovery == null
        ? $t('page.quartz.jobPage.rerunDeniedGlobal')
        : $t('page.quartz.jobPage.rerunDeniedJob'),
  };
};

// 列配置
const columns = computed(() => [
  { type: 'checkbox', width: 50, fixed: 'left' },
  { type: 'seq', width: 60, title: '#', fixed: 'left' },
  {
    field: 'jobName',
    title: $t('page.quartz.jobPage.jobName'),
    minWidth: 160,
    sortable: true,
    showOverflow: true,
  },
  {
    field: 'jobGroup',
    title: $t('page.quartz.jobPage.jobGroup'),
    minWidth: 120,
    sortable: true,
    showOverflow: true,
  },
  {
    field: 'jobType',
    title: $t('page.quartz.jobPage.jobType'),
    width: 100,
    align: 'center' as const,
    slots: { default: 'jobType' },
  },
  {
    field: 'jobClassOrApi',
    title: $t('page.quartz.jobPage.jobClassOrApi'),
    minWidth: 220,
    showOverflow: true,
  },
  {
    field: 'cronExpression',
    title: $t('page.quartz.jobPage.cronExpression'),
    width: 160,
    showOverflow: true,
  },
  {
    field: 'previousRunTime',
    title: $t('page.quartz.jobPage.previousRun'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'nextRunTime',
    title: $t('page.quartz.jobPage.nextRun'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'status',
    title: $t('page.quartz.jobPage.status'),
    width: 90,
    align: 'center' as const,
    slots: { default: 'status' },
  },
  {
    field: 'rerunOnRecovery',
    title: $t('page.quartz.jobPage.colRerun'),
    width: 80,
    align: 'center' as const,
    slots: { default: 'rerunOnRecovery' },
  },
  {
    field: 'dependencies',
    title: $t('page.quartz.jobPage.dependencies'),
    minWidth: 160,
    showOverflow: true,
    slots: { default: 'dependencies' },
  },
  {
    field: 'isEnabled',
    title: $t('page.quartz.jobPage.isEnabled'),
    width: 90,
    align: 'center' as const,
    slots: { default: 'isEnabled' },
  },
  {
    field: 'createTime',
    title: $t('page.quartz.jobPage.createTime'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'action',
    title: $t('page.quartz.jobPage.action'),
    width: 70,
    align: 'center' as const,
    fixed: 'right',
    slots: { default: 'action' },
  },
]);

// 排序持久化：读取上次排序列
const SORT_KEY = 'quartz-job-sort';
// 搜索条件持久化 key（仅保存表单输入值，不含分页/排序）
const SEARCH_KEY = 'quartz-job-search';
const savedSort = (() => {
  try {
    const raw = localStorage.getItem(SORT_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();
// 默认排序：与后端缺省排序（创建时间倒序）保持一致，保证首次进入也有排序标识
const DEFAULT_SORT = { field: 'createTime', order: 'desc' as const };
const initialSort = savedSort ?? DEFAULT_SORT;
// 当前生效的排序：点击表头后会同步更新，搜索/刷新后据此恢复列头标识与查询参数
const currentSort = { field: initialSort.field, order: initialSort.order };
const savedSearch = (() => {
  try {
    const raw = localStorage.getItem(SEARCH_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();

// 构造 Vxe Grid 配置
const gridOptions: VxeTableGridOptions<QuartzJobResponseDto> = {
  id: 'quartz-job-grid',
  columns: columns.value as any,
  height: 'auto',
  showOverflow: true,
  rowConfig: { keyField: '_rowKey', isHover: true },
  sortConfig: {
    trigger: 'cell',
    remote: true,
    defaultSort: initialSort,
  },
  customConfig: { storage: true },
  checkboxConfig: { highlight: true, range: false },
  columnConfig: { resizable: true },
  pagerConfig: { enabled: true },
  proxyConfig: {
    enabled: true,
    autoLoad: false,
    ajax: {
      query: async ({ page, sort }: any, formValues: any) => {
        // 代理 reload（搜索/刷新）会清空列头排序，回退到当前生效的排序
        const sortField = sort?.field ?? currentSort.field;
        const sortOrderRaw = sort?.order ?? currentSort.order;
        // 保持原有行为：sortOrder 使用 ascend/descend 形式
        const sortOrder =
          sortOrderRaw === 'asc' ? 'ascend' : sortOrderRaw === 'desc' ? 'descend' : '';
        // 主动从 formApi 获取表单值（避开 vxe-table reload 路径下 wrapper 注入 formValues 为空的问题）
        let currentValues: any = formValues || {};
        try {
          const formApiValues = await gridApi.formApi.getValues();
          if (formApiValues && Object.keys(formApiValues).length > 0) {
            currentValues = formApiValues;
          }
        } catch { }
        const params: QuartzJobQueryDto = {
          pageIndex: page.currentPage,
          pageSize: page.pageSize,
          jobName: currentValues?.jobName,
          jobGroup: currentValues?.jobGroup,
          jobClassOrApi: currentValues?.jobClassOrApi,
          status: currentValues?.status,
          isEnabled: currentValues?.isEnabled,
          sortBy: sortField ?? '',
          sortOrder,
        };
        // 持久化搜索条件（仅保存非空值，避免 localStorage 膨胀）
        try {
          const persisted: Record<string, any> = {};
          for (const k of ['jobName', 'jobGroup', 'jobClassOrApi', 'status', 'isEnabled']) {
            if (currentValues[k] != null && currentValues[k] !== '') {
              persisted[k] = currentValues[k];
            }
          }
          localStorage.setItem(SEARCH_KEY, JSON.stringify(persisted));
        } catch { }
        try {
          const response = await getJobs(params);
          const items = (response.data?.items || []).map((item) => ({
            ...item,
            _rowKey: `${item.jobName}-${item.jobGroup}`,
          }));
          return {
            result: items,
            page: {
              total: response.data?.totalCount || 0,
            },
          };
        } catch (error) {
          message.error($t('page.quartz.jobPage.jobListFailed'));
          console.error($t('page.quartz.jobPage.jobListFailed'), error);
          return { result: [], page: { total: 0 } };
        }
      },
      // 搜索/刷新走的是代理 reload，vxe 在 reload 前会 clearSort 清空列头排序状态，
      // 数据仍按 initialSort 排序，这里把列头标识补回来，避免标识与数据不一致
      querySuccess: () => {
        restoreSortIndicator();
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
        componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobName') },
        fieldName: 'jobName',
        label: $t('page.quartz.jobPage.jobName'),
      },
      {
        component: 'Input',
        componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobGroup') },
        fieldName: 'jobGroup',
        label: $t('page.quartz.jobPage.jobGroup'),
      },
      {
        component: 'Select',
        componentProps: {
          allowClear: true,
          placeholder: $t('page.quartz.jobPage.placeholderStatus'),
          options: [
            { label: $t('page.quartz.jobPage.statusNormal'), value: JobStatusEnum.Normal },
            { label: $t('page.quartz.jobPage.statusPaused'), value: JobStatusEnum.Paused },
          ],
        },
        fieldName: 'status',
        label: $t('page.quartz.jobPage.status'),
      },
      {
        component: 'Input',
        componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobClassOrApi') },
        fieldName: 'jobClassOrApi',
        label: $t('page.quartz.jobPage.jobClassOrApi'),
      },
      {
        component: 'Select',
        componentProps: {
          allowClear: true,
          placeholder: $t('page.quartz.jobPage.placeholderIsEnabled'),
          options: [
            { label: $t('page.quartz.jobPage.enabledYes'), value: true },
            { label: $t('page.quartz.jobPage.enabledNo'), value: false },
          ],
        },
        fieldName: 'isEnabled',
        label: $t('page.quartz.jobPage.isEnabled'),
      },
    ],
    commonConfig: {
      // 关闭浏览器对搜索框的自动填充/历史建议
      componentProps: { autocomplete: 'off' },
    },
    showCollapseButton: false,
    submitOnChange: false,
    submitOnEnter: true,
  },
  gridEvents: {
    checkboxChange: () => {
      selectedRows.value = gridApi.grid?.getCheckboxRecords?.() ?? [];
    },
    checkboxAll: () => {
      selectedRows.value = gridApi.grid?.getCheckboxRecords?.() ?? [];
    },
    sortChange: ({ property, field, order }: any) => {
      const sortField = property || field;
      if (sortField && order) {
        currentSort.field = sortField;
        currentSort.order = order;
        localStorage.setItem(
          SORT_KEY,
          JSON.stringify({ field: sortField, order }),
        );
      } else {
        // 取消排序后回退到默认排序，保证列头标识与查询参数始终一致
        currentSort.field = DEFAULT_SORT.field;
        currentSort.order = DEFAULT_SORT.order;
        localStorage.removeItem(SORT_KEY);
      }
    },
  },
});

// 编辑对话框支持拖动
useDraggableModal(editModalVisible, 'quartz-job-edit-modal');

// 恢复表头排序标识：仅在当前排序与期望不一致时设置，避免触发多余查询
const restoreSortIndicator = () => applyGridSortIndicator(gridApi, currentSort);

// 页面被 keep-alive 缓存后重新激活时，表头排序标识会丢失，需要重新同步
let sortIndicatorActivated = false;
onActivated(async () => {
  if (!sortIndicatorActivated) {
    // 首次激活与 onMounted 中的恢复重叠，跳过
    sortIndicatorActivated = true;
    return;
  }
  await nextTick();
  await restoreSortIndicator();
});

// 搜索/重置由 VbenForm 内置提交按钮触发，无需手动处理

// 监听语言切换，更新表格列头和搜索表单
watch(locale, () => {
  gridApi.setGridOptions({ columns: columns.value as any });
  gridApi.formApi.updateSchema([
    {
      fieldName: 'jobName',
      label: $t('page.quartz.jobPage.jobName'),
      componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobName') },
    },
    {
      fieldName: 'jobGroup',
      label: $t('page.quartz.jobPage.jobGroup'),
      componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobGroup') },
    },
    {
      fieldName: 'status',
      label: $t('page.quartz.jobPage.status'),
      componentProps: {
        allowClear: true,
        placeholder: $t('page.quartz.jobPage.placeholderStatus'),
        options: [
          { label: $t('page.quartz.jobPage.statusNormal'), value: JobStatusEnum.Normal },
          { label: $t('page.quartz.jobPage.statusPaused'), value: JobStatusEnum.Paused },
        ],
      },
    },
    {
      fieldName: 'jobClassOrApi',
      label: $t('page.quartz.jobPage.jobClassOrApi'),
      componentProps: { placeholder: $t('page.quartz.jobPage.placeholderJobClassOrApi') },
    },
    {
      fieldName: 'isEnabled',
      label: $t('page.quartz.jobPage.isEnabled'),
      componentProps: {
        allowClear: true,
        placeholder: $t('page.quartz.jobPage.placeholderIsEnabled'),
        options: [
          { label: $t('page.quartz.jobPage.enabledYes'), value: true },
          { label: $t('page.quartz.jobPage.enabledNo'), value: false },
        ],
      },
    },
  ]);
});

// 打开新增对话框
const handleAdd = async () => {
  editModalTitle.value = 'add';
  Object.assign(editForm, {
    jobName: '',
    jobGroup: '',
    jobType: JobTypeEnum.DLL,
    jobClassOrApi: '',
    jobClassName: '',
    apiUrl: '',
    cronExpression: '0 0/1 * * * ?',
    description: '',
    jobData: '',
    apiMethod: 'GET',
    apiHeaders: '',
    apiBody: '',
    apiTimeout: 60,
    retryCount: 0,
    retryIntervalSeconds: 30,
    skipSslValidation: false,
    disallowConcurrentExecution: false,
    rerunOnRecovery: null,
    dependencies: [],
    startTime: undefined,
    endTime: undefined,
    isEnabled: true,
  });
  // 默认作业类型是DLL，加载作业类列表
  await loadJobClasses();
  await loadDependencyOptions();
  initHeaderEditor();
  editModalVisible.value = true;
};

// 复制作业功能
const handleCopyJob = async (job: QuartzJobResponseDto) => {
  editModalTitle.value = 'copy';
  try {
    const response = await getJob(job.jobName, job.jobGroup);
    // 转换响应数据到表单格式
    // 处理jobType：后端返回字符串，前端使用枚举数字
    let jobTypeValue = JobTypeEnum.DLL;
    if (response.data?.jobType === 'API') {
      jobTypeValue = JobTypeEnum.API;
    } else if (response.data?.jobType === 'DLL') {
      jobTypeValue = JobTypeEnum.DLL;
    } else if (typeof response.data?.jobType === 'number') {
      jobTypeValue = response.data.jobType;
    }

    const jobDetail = {
      jobName: `${response.data?.jobName}_Copy`,
      jobGroup: `${response.data?.jobGroup || ''}_Copy`,
      jobType: jobTypeValue,
      jobClassOrApi: response.data?.jobClassOrApi || '',
      jobClassName: jobTypeValue === JobTypeEnum.DLL ? (response.data?.jobClassOrApi || '') : '',
      apiUrl: jobTypeValue === JobTypeEnum.API ? (response.data?.jobClassOrApi || '') : '',
      cronExpression: response.data?.cronExpression || '',
      description: response.data?.description || '',
      jobData: response.data?.jobData || '',
      apiMethod: response.data?.apiMethod || 'GET',
      apiHeaders: response.data?.apiHeaders || '',
      apiBody: response.data?.apiBody || '',
      apiTimeout: response.data?.apiTimeout || 60,
      retryCount: response.data?.retryCount ?? 0,
      retryIntervalSeconds: response.data?.retryIntervalSeconds ?? 30,
      skipSslValidation: response.data?.skipSslValidation || false,
      disallowConcurrentExecution: response.data?.disallowConcurrentExecution || false,
      rerunOnRecovery: response.data?.rerunOnRecovery ?? null,
      dependencies: response.data?.dependencies || [],
      startTime: response.data?.startTime || undefined,
      endTime: response.data?.endTime || undefined,
      isEnabled: response.data?.isEnabled !== false,
    };
    Object.assign(editForm, jobDetail);

    // 如果作业类型是DLL，加载作业类列表
    if (editForm.jobType === JobTypeEnum.DLL) {
      await loadJobClasses();
    }

    await loadDependencyOptions();
    initHeaderEditor();
    editModalVisible.value = true;
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobDetailFailed'));
    console.error($t('page.quartz.jobPage.jobDetailFailed'), error);
  }
};


// 打开编辑对话框
const handleEdit = async (job: QuartzJobResponseDto) => {
  loading.value = true;
  try {
    const response = await getJob(job.jobName, job.jobGroup);
    editModalTitle.value = 'edit';
    // 转换响应数据到表单格式
    // 处理jobType：后端返回字符串，前端使用枚举数字
    let jobTypeValue = JobTypeEnum.DLL;
    if (response.data?.jobType === 'API') {
      jobTypeValue = JobTypeEnum.API;
    } else if (response.data?.jobType === 'DLL') {
      jobTypeValue = JobTypeEnum.DLL;
    } else if (typeof response.data?.jobType === 'number') {
      jobTypeValue = response.data.jobType;
    }

    const jobDetail = {
      jobName: response.data?.jobName || '',
      jobGroup: response.data?.jobGroup || '',
      jobType: jobTypeValue,
      jobClassOrApi: response.data?.jobClassOrApi || '',
      jobClassName: jobTypeValue === JobTypeEnum.DLL ? (response.data?.jobClassOrApi || '') : '',
      apiUrl: jobTypeValue === JobTypeEnum.API ? (response.data?.jobClassOrApi || '') : '',
      cronExpression: response.data?.cronExpression || '',
      description: response.data?.description || '',
      jobData: response.data?.jobData || '',
      apiMethod: response.data?.apiMethod || 'GET',
      apiHeaders: response.data?.apiHeaders || '',
      apiBody: response.data?.apiBody || '',
      apiTimeout: response.data?.apiTimeout || 60,
      retryCount: response.data?.retryCount ?? 0,
      retryIntervalSeconds: response.data?.retryIntervalSeconds ?? 30,
      skipSslValidation: response.data?.skipSslValidation || false,
      disallowConcurrentExecution: response.data?.disallowConcurrentExecution || false,
      rerunOnRecovery: response.data?.rerunOnRecovery ?? null,
      dependencies: response.data?.dependencies || [],
      startTime: response.data?.startTime || undefined,
      endTime: response.data?.endTime || undefined,
      isEnabled: response.data?.isEnabled !== false,
    };
    Object.assign(editForm, jobDetail);

    // 如果作业类型是DLL，加载作业类列表
    if (editForm.jobType === JobTypeEnum.DLL) {
      await loadJobClasses();
    }

    await loadDependencyOptions();
    initHeaderEditor();
    editModalVisible.value = true;
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobDetailFailed'));
    console.error($t('page.quartz.jobPage.jobDetailFailed'), error);
  } finally {
    loading.value = false;
  }
};

// 保存作业
const handleSave = async () => {
  if (!formRef.value) return;

  try {
    syncHeadersToJson();

    await formRef.value.validate();

    loading.value = true;

    // 准备提交数据，确保字段名称与后端一致
    const submitData = {
      jobName: editForm.jobName,
      jobGroup: editForm.jobGroup,
      jobType: editForm.jobType,
      jobClassOrApi: editForm.jobType === JobTypeEnum.API ? editForm.apiUrl : editForm.jobClassName,
      cronExpression: editForm.cronExpression,
      description: editForm.description,
      jobData: editForm.jobData,
      apiMethod: editForm.apiMethod,
      apiHeaders: editForm.apiHeaders,
      apiBody: editForm.apiBody,
      apiTimeout: editForm.apiTimeout,
      retryCount: editForm.retryCount ?? 0,
      retryIntervalSeconds: editForm.retryIntervalSeconds ?? 30,
      skipSslValidation: editForm.skipSslValidation,
      disallowConcurrentExecution: editForm.disallowConcurrentExecution,
      rerunOnRecovery: editForm.rerunOnRecovery ?? null,
      dependencies: editForm.dependencies,
      startTime: editForm.startTime,
      endTime: editForm.endTime,
      isEnabled: editForm.isEnabled,
    };

    let response;
    let successMessage;

    if (
      editForm.jobName &&
      editForm.jobGroup &&
      editModalTitle.value === 'edit'
    ) {
      // 更新作业
      response = await updateJob(submitData);
      successMessage = $t('page.quartz.jobPage.jobUpdateSuccess');
    } else {
      // 新增作业
      response = await addJob(submitData);
      successMessage = $t('page.quartz.jobPage.jobCreateSuccess');
    }

    // 检查API响应
    if (response.success) {
      message.success(successMessage);
      editModalVisible.value = false;
      gridApi.query();
    } else {
      // 显示API返回的错误信息
      message.error(response.message || $t('page.quartz.jobPage.operationFailed'));
    }
  } catch (error: any) {
    if (error.errorFields) {
      return; // 表单验证错误已显示
    }
    // 尝试从错误响应中提取更详细的信息
    const errorMessage =
      error.response?.data?.message ||
      error.message ||
      (editModalTitle.value === 'edit' ? $t('page.quartz.jobPage.jobUpdateFailed') : $t('page.quartz.jobPage.jobCreateFailed'));
    message.error(errorMessage);
  } finally {
    loading.value = false;
  }
};

// 删除作业
const handleDelete = (job: QuartzJobResponseDto) => {
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmDelete'),
    content: $t('page.quartz.jobPage.confirmDeleteContent', { name: job.jobName }),
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      try {
        await deleteJob(job.jobName, job.jobGroup);
        message.success($t('page.quartz.jobPage.jobDeletedSuccess'));
        gridApi.query();
      } catch (error) {
        message.error($t('page.quartz.jobPage.jobDeletedFailed'));
        console.error($t('page.quartz.jobPage.jobDeletedFailed'), error);
      }
    },
  });
};

// 停止作业
const handleStop = async (job: QuartzJobResponseDto) => {
  try {
    await pauseJob(job.jobName, job.jobGroup);
    message.success($t('page.quartz.jobPage.jobPausedSuccess'));
    gridApi.query();
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobPausedFailed'));
    console.error($t('page.quartz.jobPage.jobPausedFailed'), error);
  }
};

// 恢复作业
const handleResume = async (job: QuartzJobResponseDto) => {
  try {
    await resumeJob(job.jobName, job.jobGroup);
    message.success($t('page.quartz.jobPage.jobResumedSuccess'));
    gridApi.query();
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobResumedFailed'));
    console.error($t('page.quartz.jobPage.jobResumedFailed'), error);
  }
};

// 立即执行作业
const handleExecute = async (job: QuartzJobResponseDto) => {
  try {
    await triggerJob(job.jobName, job.jobGroup);
    message.success($t('page.quartz.jobPage.jobExecutedSuccess'));
    notification.success({
      message: $t('page.quartz.jobPage.jobExecutionNotify'),
      description: $t('page.quartz.jobPage.jobExecutionNotifyDesc', { name: job.jobName }),
    });
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobExecutedFailed'));
    console.error($t('page.quartz.jobPage.jobExecutedFailed'), error);
  }
};

// 切换作业启用状态
const handleToggleEnabled = (job: QuartzJobResponseDto, checked: boolean) => {
  const key = `${job.jobName}:${job.jobGroup}`;
  const action = checked ? $t('page.quartz.jobPage.enable') : $t('page.quartz.jobPage.disable');
  Modal.confirm({
    title: $t('page.quartz.jobPage.toggleEnabledTitle'),
    content: $t('page.quartz.jobPage.toggleEnabledContent', { action, name: job.jobName }),
    okText: $t('page.quartz.jobPage.ok'),
    cancelText: $t('page.quartz.jobPage.cancel'),
    onOk: async () => {
      toggleLoadingKeys.value = new Set([...toggleLoadingKeys.value, key]);
      try {
        await updateJob({
          jobName: job.jobName,
          jobGroup: job.jobGroup,
          jobType: job.jobType,
          jobClassOrApi: job.jobClassOrApi,
          cronExpression: job.cronExpression,
          description: job.description,
          jobData: job.jobData,
          apiMethod: job.apiMethod,
          apiHeaders: job.apiHeaders,
          apiBody: job.apiBody,
          apiTimeout: job.apiTimeout,
          retryCount: job.retryCount,
          retryIntervalSeconds: job.retryIntervalSeconds,
          skipSslValidation: job.skipSslValidation,
          disallowConcurrentExecution: job.disallowConcurrentExecution,
          dependencies: job.dependencies,
          startTime: job.startTime,
          endTime: job.endTime,
          isEnabled: checked,
        });
        message.success($t('page.quartz.jobPage.toggleEnabledSuccess', { action }));
        gridApi.query();
      } catch (error) {
        message.error($t('page.quartz.jobPage.toggleEnabledFailed', { action }));
        console.error($t('page.quartz.jobPage.toggleEnabledFailed', { action }), error);
      } finally {
        const next = new Set(toggleLoadingKeys.value);
        next.delete(key);
        toggleLoadingKeys.value = next;
      }
    },
  });
};

// 批量删除作业
const handleBatchDelete = () => {
  if (selectedRows.value.length === 0) {
    message.warning($t('page.quartz.jobPage.selectDeleteFirst'));
    return;
  }
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmBatchDelete'),
    content: $t('page.quartz.jobPage.confirmBatchDeleteContent', { count: selectedRows.value.length }),
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      try {
        // 准备删除参数 - 使用与后端匹配的格式
        const jobList = selectedRows.value.map((job) => {
          return {
            JobName: job.jobName,
            JobGroup: job.jobGroup,
          };
        });

        const result = await batchDeleteJob(jobList);

        if (result.success) {
          message.success($t('page.quartz.jobPage.batchDeleteSuccess'));
          // 清空选择
          selectedRows.value = [];
          gridApi.grid?.clearCheckboxRow?.();
          // 重新加载作业列表
          gridApi.query();
        } else {
          message.error(result.message || $t('page.quartz.jobPage.batchDeleteFailed'));
        }
      } catch (error: any) {
        message.error(error.message || $t('page.quartz.jobPage.batchDeleteFailed'));
      }
    },
  });
};

// 批量停止/恢复/启用/禁用
const handleBatchOperation = (
  action: 'disable' | 'enable' | 'pause' | 'resume',
) => {
  if (selectedRows.value.length === 0) {
    message.warning($t('page.quartz.jobPage.selectOperationFirst'));
    return;
  }

  const actionLabels: Record<string, string> = {
    pause: $t('page.quartz.jobPage.batchPause'),
    resume: $t('page.quartz.jobPage.batchResume'),
    enable: $t('page.quartz.jobPage.batchEnable'),
    disable: $t('page.quartz.jobPage.batchDisable'),
  };
  const actionText = actionLabels[action];

  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmBatchAction', { action: actionText }),
    content: $t('page.quartz.jobPage.confirmBatchActionContent', {
      action: actionText,
      count: selectedRows.value.length,
    }),
    okText: $t('page.quartz.jobPage.ok'),
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      try {
        const jobList = selectedRows.value.map((job) => ({
          JobName: job.jobName,
          JobGroup: job.jobGroup,
        }));

        let result;
        if (action === 'pause') {
          result = await batchPauseJobs(jobList);
        } else if (action === 'resume') {
          result = await batchResumeJobs(jobList);
        } else {
          result = await batchSetEnabled(jobList, action === 'enable');
        }

        if (result.success) {
          message.success(
            result.message ||
              $t('page.quartz.jobPage.batchActionSuccess', { action: actionText }),
          );
          selectedRows.value = [];
          gridApi.grid?.clearCheckboxRow?.();
          gridApi.query();
        } else {
          message.error(
            result.message ||
              $t('page.quartz.jobPage.batchActionFailed', { action: actionText }),
          );
        }
      } catch (error: any) {
        message.error(
          error.message ||
            $t('page.quartz.jobPage.batchActionFailed', { action: actionText }),
        );
      }
    },
  });
};

// 获取调度器状态
const getSchedulerStatusInfo = async () => {
  try {
    const response = await getSchedulerStatus();
    if (response.success && response.data) {
      // 直接使用后端返回的数据，不进行命名转换
      schedulerStatus.value = response.data;
      if (response.data.isDraining) {
        startDrainPolling();
      }
    }
  } catch (error) {
    console.error($t('page.quartz.jobPage.schedulerStatusFailed'), error);
    message.error($t('page.quartz.jobPage.schedulerStatusFailed'));
  }
};

// 启动调度器
const handleStartScheduler = async () => {
  if (isSchedulerBusy.value) return;
  schedulerAction.value = 'start';
  try {
    const response = await startScheduler();
    if (response.success) {
      message.success($t('page.quartz.jobPage.schedulerStartedSuccess'));
      await getSchedulerStatusInfo();
      gridApi.query();
    } else {
      message.warning(
        response.message || $t('page.quartz.jobPage.schedulerStartFailed'),
      );
    }
  } catch (error) {
    console.error($t('page.quartz.jobPage.schedulerStartFailed'), error);
    message.error($t('page.quartz.jobPage.schedulerStartFailed'));
  } finally {
    schedulerAction.value = null;
  }
};

// 停止调度器
const handleStopScheduler = () => {
  if (isSchedulerBusy.value) return;
  const draining = schedulerStatus.value.isDraining;
  const remaining =
    schedulerStatus.value.remainingExecutingCount ??
    schedulerStatus.value.executingJobCount ??
    0;
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmStopScheduler'),
    content: draining
      ? $t('page.quartz.jobPage.confirmStopDrainContent', { count: remaining })
      : $t('page.quartz.jobPage.confirmStopSchedulerContent'),
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      schedulerAction.value = 'stop';
      try {
        const response = await stopScheduler();
        if (response.success) {
          message.success($t('page.quartz.jobPage.schedulerStoppedSuccess'));
          stopDrainPolling();
          await getSchedulerStatusInfo();
          gridApi.query();
        } else {
          message.warning(
            response.message || $t('page.quartz.jobPage.schedulerStopFailed'),
          );
        }
      } catch (error) {
        console.error($t('page.quartz.jobPage.schedulerStopFailed'), error);
        message.error($t('page.quartz.jobPage.schedulerStopFailed'));
      } finally {
        schedulerAction.value = null;
      }
    },
  });
};

// 排水：停止接收新触发，等待执行中任务收尾
const handleDrain = () => {
  if (isSchedulerBusy.value) return;
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmDrain'),
    content: $t('page.quartz.jobPage.confirmDrainContent'),
    okText: $t('page.quartz.jobPage.ok'),
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      schedulerAction.value = 'drain';
      try {
        const response = await drainScheduler();
        if (response.success) {
          message.success($t('page.quartz.jobPage.drainSuccess'));
          await getSchedulerStatusInfo();
          startDrainPolling();
        } else {
          message.warning(
            response.message || $t('page.quartz.jobPage.drainFailed'),
          );
        }
      } catch (error: any) {
        message.error(error?.message || $t('page.quartz.jobPage.drainFailed'));
      } finally {
        schedulerAction.value = null;
      }
    },
  });
};

// 取消排水，恢复调度
const handleResumeScheduler = async () => {
  if (isSchedulerBusy.value) return;
  schedulerAction.value = 'resume';
  try {
    // 按钮可能来自上一次状态快照，先拉最新状态，避免对已结束的排水发恢复请求
    await getSchedulerStatusInfo();
    if (!schedulerStatus.value.isDraining) {
      stopDrainPolling();
      message.info($t('page.quartz.jobPage.drainCompleted'));
      await gridApi.query();
      return;
    }

    const response = await resumeScheduler();
    if (response.success) {
      message.success($t('page.quartz.jobPage.resumeSuccess'));
      stopDrainPolling();
      await getSchedulerStatusInfo();
      gridApi.query();
    } else {
      message.warning(
        response.message || $t('page.quartz.jobPage.resumeFailed'),
      );
      await getSchedulerStatusInfo();
    }
  } catch (error: any) {
    message.error(error?.message || $t('page.quartz.jobPage.resumeFailed'));
    await getSchedulerStatusInfo();
  } finally {
    schedulerAction.value = null;
  }
};

const API_METHOD_COLORS: Record<string, string> = {
  GET: '#52c41a',
  POST: '#1890ff',
  PUT: '#fa8c16',
  DELETE: '#ff4d4f',
  PATCH: '#722ed1',
};

const COMMON_HEADERS = [
  'Content-Type',
  'Authorization',
  'Accept',
  'Cache-Control',
  'User-Agent',
  'X-Request-ID',
  'X-API-Key',
];

interface HeaderRow {
  key: string;
  value: string;
  id: number;
}

let headerRowIdCounter = 0;

const headerRows = ref<HeaderRow[]>([]);
const selectedCommonHeader = ref<string | undefined>(undefined);

const isBodyDiscouraged = computed(() => {
  return editForm.apiMethod === 'GET' || editForm.apiMethod === 'DELETE';
});

const parseJsonToHeaders = (json: string): HeaderRow[] => {
  if (!json || !json.trim()) return [];
  try {
    const obj = JSON.parse(json);
    if (typeof obj !== 'object' || Array.isArray(obj)) return [];
    return Object.entries(obj).map(([k, v]) => ({
      key: k,
      value: String(v),
      id: ++headerRowIdCounter,
    }));
  } catch {
    return [];
  }
};

const syncHeadersToJson = () => {
  const validRows = headerRows.value.filter((r) => r.key.trim());
  if (validRows.length === 0) {
    editForm.apiHeaders = '';
    return;
  }
  const obj: Record<string, string> = {};
  validRows.forEach((r) => {
    obj[r.key.trim()] = r.value;
  });
  editForm.apiHeaders = JSON.stringify(obj, null, 2);
};

const addHeaderRow = (key = '', value = '') => {
  headerRows.value.push({ key, value, id: ++headerRowIdCounter });
  syncHeadersToJson();
};

const removeHeaderRow = (id: number) => {
  headerRows.value = headerRows.value.filter((r) => r.id !== id);
  syncHeadersToJson();
};

const handleAddHeaderRow = () => {
  const headerKey = selectedCommonHeader.value;
  if (headerKey) {
    const exists = headerRows.value.some((r) => r.key === headerKey);
    if (exists) {
      message.warning($t('page.quartz.jobPage.apiHeadersDuplicate'));
      return;
    }
    const defaultValues: Record<string, string> = {
      'Content-Type': 'application/json',
      Authorization: 'Bearer ',
      Accept: 'application/json',
    };
    addHeaderRow(headerKey, defaultValues[headerKey] || '');
    selectedCommonHeader.value = undefined;
  } else {
    addHeaderRow();
  }
};

const initHeaderEditor = () => {
  if (editForm.apiHeaders && editForm.apiHeaders.trim()) {
    headerRows.value = parseJsonToHeaders(editForm.apiHeaders);
  } else {
    headerRows.value = [];
  }
};

const formatJson = (property: keyof QuartzJobDto) => {
  try {
    const value = editForm[property];
    if (typeof value === 'string' && value) {
      const parsed = JSON.parse(value);
      (editForm as any)[property] = JSON.stringify(parsed, null, 2);
      message.success($t('page.quartz.jobPage.jsonFormatSuccess'));
    }
  } catch (error) {
    message.error($t('page.quartz.jobPage.invalidJson'));
  }
};

const formatJsonOnBlur = (property: keyof QuartzJobDto) => {
  try {
    const value = editForm[property];
    if (typeof value === 'string' && value.trim()) {
      const parsed = JSON.parse(value as string);
      (editForm as any)[property] = JSON.stringify(parsed, null, 2);
    }
  } catch {
    // silent on blur
  }
};

// 生命周期
onMounted(async () => {
  await getSchedulerStatusInfo();
  // 恢复搜索条件到表单（autoLoad: false 时，需在 setValues 后手动触发查询）
  if (savedSearch) {
    try {
      await gridApi.formApi.setValues(savedSearch);
    } catch { }
  }
  // 手动触发首次查询（此时 formApi 已回填搜索条件，query 回调能拿到值）
  await gridApi.query();
  // defaultSort 未生效时兜底恢复排序标识
  await restoreSortIndicator();
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <!-- 作业管理 -->
      <Grid>
        <!-- 工具栏：左侧调度器控制 + 右侧作业操作 -->
        <template #toolbar-actions>
          <div class="toolbar-shell">
            <div class="scheduler-bar">
              <Tooltip
                placement="bottomLeft"
                :overlay-style="{ maxWidth: '420px' }"
              >
                <template #title>
                  <div class="scheduler-tip">
                    <div>
                      {{ $t('page.quartz.jobPage.runtimeInstance') }}:
                      {{ schedulerStatus.runtimeInstanceId || schedulerStatus.schedulerInstanceId || '-' }}
                    </div>
                    <div>
                      {{ $t('page.quartz.jobPage.threadPoolSize') }}:
                      {{ schedulerStatus.threadPoolSize }} ·
                      {{ $t('page.quartz.jobPage.executingJobs') }}:
                      {{ executingCount }}
                    </div>
                    <div v-if="schedulerStatus.startTime">
                      {{ $t('page.quartz.jobPage.schedulerStartTime') }}:
                      {{ formatDateTime(schedulerStatus.startTime) }}
                    </div>
                  </div>
                </template>
                <span class="scheduler-status" :class="schedulerStateClass">
                  <i class="status-dot"></i>
                  <span class="scheduler-status__text">
                    {{ schedulerStatus.isDraining ? drainBadgeText : schedulerStatus.status }}
                  </span>
                  <span
                    v-if="schedulerStatus.isStarted && executingCount > 0"
                    class="scheduler-status__count"
                  >
                    {{ executingCount }}
                  </span>
                </span>
              </Tooltip>
              <span class="scheduler-sep"></span>
              <div class="scheduler-actions">
                <Button
                  v-if="!schedulerStatus.isStarted"
                  size="small"
                  type="primary"
                  :loading="schedulerAction === 'start'"
                  @click="handleStartScheduler"
                >
                  {{ $t('page.quartz.jobPage.startScheduler') }}
                </Button>
                <template v-else>
                  <Tooltip
                    v-if="!schedulerStatus.isDraining"
                    :title="$t('page.quartz.jobPage.drainHint')"
                    placement="bottom"
                  >
                    <Button
                      size="small"
                      :loading="schedulerAction === 'drain'"
                      :disabled="schedulerStatus.isShutdown"
                      @click="handleDrain"
                    >
                      {{ $t('page.quartz.jobPage.drainScheduler') }}
                    </Button>
                  </Tooltip>
                  <Button
                    v-else
                    size="small"
                    type="primary"
                    ghost
                    :loading="schedulerAction === 'resume'"
                    @click="handleResumeScheduler"
                  >
                    <RotateCw class="scheduler-btn-icon" />
                    {{ $t('page.quartz.jobPage.resumeScheduler') }}
                  </Button>
                  <Button
                    size="small"
                    danger
                    :loading="schedulerAction === 'stop'"
                    :disabled="schedulerStatus.isShutdown"
                    @click="handleStopScheduler"
                  >
                    {{ $t('page.quartz.jobPage.stopScheduler') }}
                  </Button>
                </template>
              </div>
            </div>
            <Space :size="8" class="toolbar-actions">
              <Button type="primary" @click="handleAdd"> {{ $t('page.quartz.jobPage.addJob') }} </Button>
              <Dropdown :trigger="['click']">
                <Button :disabled="selectedRows.length === 0">
                  {{ $t('page.quartz.jobPage.batchActions') }}
                  <i class="vxe-icon-arrow-down ml-1"></i>
                </Button>
                <template #overlay>
                  <Menu>
                    <Menu.Item key="batchPause" @click="handleBatchOperation('pause')"
                      :style="{ color: '#faad14' }">
                      <i class="vxe-icon-error-circle mr-1"></i>
                      {{ $t('page.quartz.jobPage.batchPause') }}
                    </Menu.Item>
                    <Menu.Item key="batchResume" @click="handleBatchOperation('resume')"
                      :style="{ color: '#52c41a' }">
                      <i class="vxe-icon-success-circle mr-1"></i>
                      {{ $t('page.quartz.jobPage.batchResume') }}
                    </Menu.Item>
                    <Menu.Item key="batchEnable" @click="handleBatchOperation('enable')"
                      :style="{ color: '#1890ff' }">
                      <i class="vxe-icon-check mr-1"></i>
                      {{ $t('page.quartz.jobPage.batchEnable') }}
                    </Menu.Item>
                    <Menu.Item key="batchDisable" @click="handleBatchOperation('disable')"
                      :style="{ color: '#8c8c8c' }">
                      <i class="vxe-icon-close mr-1"></i>
                      {{ $t('page.quartz.jobPage.batchDisable') }}
                    </Menu.Item>
                    <Menu.Item key="batchDelete" danger @click="handleBatchDelete">
                      <i class="vxe-icon-delete mr-1"></i>
                      {{ $t('page.quartz.jobPage.batchDelete') }}
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </Space>
          </div>
        </template>

        <!-- 作业类型 -->
        <template #jobType="{ row }">
          <Tag :color="jobTypeMap[row.jobType as JobTypeEnum]?.color || 'default'">
            {{ jobTypeMap[row.jobType as JobTypeEnum]?.text || $t('page.quartz.jobPage.unknown') }}
          </Tag>
        </template>

        <!-- 作业状态 -->
        <template #status="{ row }">
          <Tag :color="jobStatusMap[row.status as JobStatusEnum]?.status || 'default'">
            {{ jobStatusMap[row.status as JobStatusEnum]?.text?.() || row.status || $t('page.quartz.jobPage.unknown') }}
          </Tag>
        </template>

        <!-- 中断后自动重跑三态图标 -->
        <template #rerunOnRecovery="{ row }">
          <Tooltip :title="rerunIconInfo(row).tip">
            <component
              :is="rerunIconInfo(row).icon"
              :class="rerunIconInfo(row).cls"
              class="rerun-state-icon inline-flex h-4 w-4"
              tabindex="-1"
              focusable="false"
            />
          </Tooltip>
        </template>

        <!-- 前置依赖 -->
        <template #dependencies="{ row }">
          <span v-if="row.dependencies?.length">
            {{ row.dependencies.map((item: JobDependency) => `${item.jobGroup}/${item.jobName}`).join('、') }}
          </span>
          <span v-else>-</span>
        </template>

        <!-- 是否启用 -->
        <template #isEnabled="{ row }">
          <Switch :checked="row.isEnabled" :loading="toggleLoadingKeys.has(`${row.jobName}:${row.jobGroup}`)" @change="(checked: boolean) => handleToggleEnabled(row, checked)" />
        </template>

        <!-- 通用日期时间渲染 -->
        <template #datetime="{ row, column }">
          {{ (row as any)[column.field] ? formatDateTime((row as any)[column.field]) : '-' }}
        </template>

        <!-- 操作列 -->
        <template #action="{ row }">
          <Dropdown :trigger="['hover']" placement="bottomRight">
            <i class="vxe-icon-ellipsis-h text-base cursor-pointer hover:opacity-80" :class="{ 'opacity-50': loading }"></i>
            <template #overlay>
              <Menu>
                <Menu.Item key="edit" @click="handleEdit(row)">
                  <i class="vxe-icon-edit mr-1"></i>
                  {{ $t('page.quartz.jobPage.edit') }}
                </Menu.Item>
                <Menu.Item key="copy" @click="handleCopyJob(row)">
                  <i class="vxe-icon-copy mr-1"></i>
                  {{ $t('page.quartz.jobPage.copy') }}
                </Menu.Item>
                <Menu.Item key="delete" danger @click="handleDelete(row)">
                  <i class="vxe-icon-delete mr-1"></i>
                  {{ $t('page.quartz.jobPage.delete') }}
                </Menu.Item>
                <Menu.Item key="toggle"
                  @click="row.status === JobStatusEnum.Normal ? handleStop(row) : handleResume(row)"
                  :style="{ color: row.status === JobStatusEnum.Normal ? '#faad14' : '#52c41a' }">
                  <i :class="row.status === JobStatusEnum.Normal ? 'vxe-icon-error-circle' : 'vxe-icon-success-circle'"
                    class="mr-1"></i>
                  {{ row.status === JobStatusEnum.Normal ? $t('page.quartz.jobPage.stop') :
                    $t('page.quartz.jobPage.resume') }}
                </Menu.Item>
                <Menu.Item key="execute" @click="handleExecute(row)" :style="{ color: '#1890ff' }">
                  <i class="vxe-icon-arrow-right mr-1"></i>
                  {{ $t('page.quartz.jobPage.executeNow') }}
                </Menu.Item>
              </Menu>
            </template>
          </Dropdown>
        </template>
      </Grid>

      <!-- 新增编辑对话框 -->
      <Modal v-model:open="editModalVisible" :title="editModalDisplayTitle" width="800px"
        :body-style="{ padding: '24px' }" wrapClassName="quartz-job-edit-modal" destroyOnClose
        @cancel="editModalVisible = false">
        <Form ref="formRef" :model="editForm" layout="horizontal" :label-col="{ style: { width: '110px' } }"
          :wrapper-col="{ flex: 1 }" autocomplete="off">
          <!-- 基本信息 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionBasic') }}</div>
          <Row :gutter="16">
            <Col :xs="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobName')" name="jobName"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobNameRequired') }, { max: 100, message: $t('page.quartz.jobPage.jobNameMaxLen') }]">
                <Input v-model:value="editForm.jobName" :placeholder="$t('page.quartz.jobPage.placeholderJobName')"
                  :disabled="isEditMode" />
              </Form.Item>
            </Col>
            <Col :xs="24" :md="12">
              <Form.Item :label="$t('page.quartz.jobPage.jobGroup')" name="jobGroup"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobGroupRequired') }, { max: 100, message: $t('page.quartz.jobPage.jobGroupMaxLen') }]">
                <Input v-model:value="editForm.jobGroup" :placeholder="$t('page.quartz.jobPage.placeholderJobGroup')"
                  :disabled="isEditMode" />
              </Form.Item>
            </Col>
            <Col :xs="24" :md="12">
              <Form.Item :label="$t('page.quartz.jobPage.jobType')" name="jobType"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobTypeRequired') }]">
                <Select v-model:value="editForm.jobType" @change="handleJobTypeChange">
                  <Select.Option :value="JobTypeEnum.DLL">DLL</Select.Option>
                  <Select.Option :value="JobTypeEnum.API">API</Select.Option>
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <!-- 调度设置 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionSchedule') }}</div>
          <Row :gutter="16" class="schedule-grid">
            <Col :xs="24" :md="15" class="schedule-col">
              <Form.Item :label="$t('page.quartz.jobPage.cronExpression')" name="cronExpression"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.cronRequired') }, { max: 200, message: $t('page.quartz.jobPage.cronMaxLen') }]">
                <Space.Compact style="width: 100%">
                  <Input v-model:value="editForm.cronExpression"
                    :placeholder="$t('page.quartz.jobPage.cronPlaceholder')" style="flex: 1" />
                  <Button @click="openCronHelper">{{ $t('page.quartz.jobPage.cronGenerator') }}</Button>
                </Space.Compact>
              </Form.Item>
            </Col>
            <Col :xs="24" :md="9" class="schedule-col schedule-col--inline-label">
              <Form.Item :label="$t('page.quartz.jobPage.disallowConcurrent')" name="disallowConcurrentExecution">
                <Tooltip :title="$t('page.quartz.jobPage.disallowConcurrentHint')">
                  <Switch v-model:checked="editForm.disallowConcurrentExecution" />
                </Tooltip>
              </Form.Item>
            </Col>
            <!-- 前置依赖：独占整行 -->
            <Col :xs="24" class="schedule-col">
              <Form.Item :label="$t('page.quartz.jobPage.dependencies')" name="dependencies">
                <Tooltip :title="$t('page.quartz.jobPage.dependenciesHint')">
                  <Select v-model:value="dependencyKeys" mode="multiple" :options="dependencySelectOptions"
                    :placeholder="$t('page.quartz.jobPage.dependenciesPlaceholder')" allowClear
                    optionFilterProp="label" :maxTagCount="'responsive'" />
                </Tooltip>
              </Form.Item>
            </Col>
            <!-- 失败重试：次数与间隔紧凑组合 -->
            <Col :xs="24" :md="15" class="schedule-col">
              <Form.Item :label="$t('page.quartz.jobPage.retryLabel')" name="retryCount" :rules="[
                { type: 'number', min: 0, max: 10, message: $t('page.quartz.jobPage.retryCountRange') },
              ]">
                <Tooltip :title="$t('page.quartz.jobPage.retryCountHint')">
                  <div class="retry-group">
                    <InputNumber v-model:value="editForm.retryCount" :min="0" :max="10" class="retry-group__input"
                      :placeholder="$t('page.quartz.jobPage.placeholderRetryCount')" />
                    <span class="retry-group__unit">{{ $t('page.quartz.jobPage.retryTimesUnit') }}</span>
                    <span class="retry-group__sep" aria-hidden="true"></span>
                    <span class="retry-group__inline-label">{{ $t('page.quartz.jobPage.retryIntervalInline') }}</span>
                    <InputNumber v-model:value="editForm.retryIntervalSeconds" :min="1" :max="3600"
                      class="retry-group__input retry-group__input--interval"
                      :placeholder="$t('page.quartz.jobPage.placeholderRetryInterval')"
                      :disabled="!editForm.retryCount" />
                    <span class="retry-group__unit">{{ $t('page.quartz.jobPage.retrySecondsUnit') }}</span>
                  </div>
                </Tooltip>
              </Form.Item>
            </Col>
            <Col :xs="24" :md="9" class="schedule-col schedule-col--inline-label">
              <Form.Item :label="$t('page.quartz.jobPage.rerunOnRecovery')" name="rerunOnRecovery">
                <Tooltip :title="$t('page.quartz.jobPage.rerunOnRecoveryHint')">
                  <Select
                    v-model:value="rerunOnRecoveryValue"
                    :options="[
                      { label: $t('page.quartz.jobPage.rerunFollowGlobal'), value: 'global' },
                      { label: $t('page.quartz.jobPage.rerunEnabled'), value: 'on' },
                      { label: $t('page.quartz.jobPage.rerunDisabled'), value: 'off' },
                    ]"
                    style="width: 100%"
                  />
                </Tooltip>
              </Form.Item>
            </Col>
          </Row>

          <!-- 作业配置 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionConfig') }}</div>
          <Row :gutter="16">
            <Col :xs="24" v-if="editForm.jobType === JobTypeEnum.DLL">
              <Form.Item :label="$t('page.quartz.jobPage.jobClassName')" name="jobClassName"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobClassNameRequired') }, { max: 500, message: $t('page.quartz.jobPage.jobClassNameMaxLen') }]">
                <Select v-model:value="editForm.jobClassName"
                  :placeholder="$t('page.quartz.jobPage.placeholderJobClassName')" showSearch allowClear
                  mode="SECRET_COMBOBOX_MODE_DO_NOT_USE" :filter-option="(input, option) => {
                    return (option?.label || '')
                      .toLowerCase()
                      .includes(input.toLowerCase());
                  }
                    ">
                  <Select.Option v-for="jobClass in jobClasses" :key="jobClass" :value="jobClass" :label="jobClass">
                    {{ jobClass }}
                  </Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" v-if="editForm.jobType === JobTypeEnum.API">
              <Form.Item :label="$t('page.quartz.jobPage.apiUrl')" name="apiUrl"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.apiUrlRequired') }, { max: 500, message: $t('page.quartz.jobPage.apiUrlMaxLen') }]">
                <Input v-model:value="editForm.apiUrl"
                  :placeholder="$t('page.quartz.jobPage.placeholderApiUrl')" />
              </Form.Item>
            </Col>
            <!-- DLL: 作业数据 -->
            <Col :xs="24" v-if="editForm.jobType === JobTypeEnum.DLL">
              <Form.Item :label="$t('page.quartz.jobPage.jobData')" name="jobData" :rules="[
                {
                  validator: (rule, value, callback) => {
                    if (!value) return callback();
                    try {
                      JSON.parse(value);
                      callback();
                    } catch (e) {
                      callback(new Error($t('page.quartz.jobPage.invalidJsonFormat')));
                    }
                  },
                },
              ]">
                <div class="json-field">
                  <Textarea v-model:value="editForm.jobData"
                    :placeholder="$t('page.quartz.jobPage.placeholderJobData')" :rows="3"
                    class="json-textarea" @blur="formatJsonOnBlur('jobData')" />
                  <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                    <Button type="link" size="small" class="json-format-btn" @click="formatJson('jobData')">
                      {{ $t('page.quartz.jobPage.jsonFormat') }}
                    </Button>
                  </Tooltip>
                </div>
              </Form.Item>
            </Col>
            <!-- API 相关配置 -->
            <template v-if="editForm.jobType === JobTypeEnum.API">
              <Col :xs="24" :md="12">
                <Form.Item :label="$t('page.quartz.jobPage.apiMethod')" name="apiMethod"
                  :rules="[{ required: true, message: $t('page.quartz.jobPage.placeholderApiMethod') }, { max: 10, message: $t('page.quartz.jobPage.apiMethodMaxLen') }]">
                  <Select v-model:value="editForm.apiMethod">
                    <Select.Option value="GET">
                      <span class="api-method-option">
                        <span class="api-method-dot" :style="{ background: API_METHOD_COLORS['GET'] }"></span>GET
                      </span>
                    </Select.Option>
                    <Select.Option value="POST">
                      <span class="api-method-option">
                        <span class="api-method-dot" :style="{ background: API_METHOD_COLORS['POST'] }"></span>POST
                      </span>
                    </Select.Option>
                    <Select.Option value="PUT">
                      <span class="api-method-option">
                        <span class="api-method-dot" :style="{ background: API_METHOD_COLORS['PUT'] }"></span>PUT
                      </span>
                    </Select.Option>
                    <Select.Option value="DELETE">
                      <span class="api-method-option">
                        <span class="api-method-dot" :style="{ background: API_METHOD_COLORS['DELETE'] }"></span>DELETE
                      </span>
                    </Select.Option>
                    <Select.Option value="PATCH">
                      <span class="api-method-option">
                        <span class="api-method-dot" :style="{ background: API_METHOD_COLORS['PATCH'] }"></span>PATCH
                      </span>
                    </Select.Option>
                  </Select>
                </Form.Item>
              </Col>
              <Col :xs="24" :md="12">
                <Form.Item :label="$t('page.quartz.jobPage.apiTimeout')" name="apiTimeout" :rules="[
                  {
                    required: true,
                    message: $t('page.quartz.jobPage.apiTimeoutRequired'),
                    type: 'number',
                  },
                  { type: 'number', min: 1, max: 99999, message: $t('page.quartz.jobPage.apiTimeoutRange') },
                ]">
                  <div class="api-timeout-field">
                    <InputNumber v-model:value="editForm.apiTimeout" :min="1" :max="99999" :step="10"
                      style="flex: 1"
                      :placeholder="$t('page.quartz.jobPage.placeholderApiTimeout')" />
                    <span class="api-timeout-unit">{{ $t('page.quartz.jobPage.apiTimeoutUnit') }}</span>
                  </div>
                </Form.Item>
              </Col>
              <Col :xs="24">
                <Form.Item name="skipSslValidation" valuePropName="checked">
                  <template #label>
                    <Tooltip :title="$t('page.quartz.jobPage.skipSslWarning')">
                      <span>{{ $t('page.quartz.jobPage.skipSsl') }}</span>
                    </Tooltip>
                  </template>
                  <Switch v-model:checked="editForm.skipSslValidation" />
                </Form.Item>
              </Col>
              <Col :xs="24">
                <Form.Item :label="$t('page.quartz.jobPage.apiHeaders')" name="apiHeaders" :rules="[
                  {
                    validator: (rule, value, callback) => {
                      if (!value) return callback();
                      try {
                        JSON.parse(value);
                        callback();
                      } catch (e) {
                        callback(new Error($t('page.quartz.jobPage.invalidJsonFormat')));
                      }
                    },
                  },
                ]">
                  <div class="api-headers-editor">
                    <div v-for="row in headerRows" :key="row.id" class="header-row">
                      <Input v-model:value="row.key" class="header-row__key"
                        :placeholder="$t('page.quartz.jobPage.apiHeadersKey')"
                        @change="syncHeadersToJson" />
                      <Input v-model:value="row.value" class="header-row__value"
                        :placeholder="$t('page.quartz.jobPage.apiHeadersValue')"
                        @change="syncHeadersToJson" />
                      <Button type="text" danger size="small" class="header-row__remove"
                        @click="removeHeaderRow(row.id)">
                        <i class="vxe-icon-close"></i>
                      </Button>
                    </div>
                    <div class="header-actions">
                      <Select size="small" class="header-common-select"
                        v-model:value="selectedCommonHeader"
                        :placeholder="$t('page.quartz.jobPage.apiHeadersCommon')"
                        allowClear>
                        <Select.Option v-for="h in COMMON_HEADERS" :key="h" :value="h">{{ h }}</Select.Option>
                      </Select>
                      <Button type="dashed" size="small" @click="handleAddHeaderRow">
                        <i class="vxe-icon-plus" style="margin-right: 4px;"></i>
                        {{ $t('page.quartz.jobPage.apiHeadersAdd') }}
                      </Button>
                    </div>
                  </div>
                </Form.Item>
              </Col>
              <Col :xs="24">
                <Form.Item :label="$t('page.quartz.jobPage.apiBody')" name="apiBody" :rules="[
                  {
                    validator: (rule, value, callback) => {
                      if (!value) return callback();
                      try {
                        JSON.parse(value);
                        callback();
                      } catch (e) {
                        callback(new Error($t('page.quartz.jobPage.invalidJsonFormat')));
                      }
                    },
                  },
                ]">
                  <div class="api-body-editor">
                    <Alert v-if="isBodyDiscouraged" type="warning" show-icon class="api-body-hint"
                      :message="$t('page.quartz.jobPage.apiBodyNoBodyHint', { method: editForm.apiMethod })" />
                    <div class="json-field">
                      <Textarea v-model:value="editForm.apiBody"
                        :placeholder="$t('page.quartz.jobPage.placeholderApiBody')" :rows="3"
                        class="json-textarea" @blur="formatJsonOnBlur('apiBody')" />
                      <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                        <Button type="link" size="small" class="json-format-btn" @click="formatJson('apiBody')">
                          {{ $t('page.quartz.jobPage.jsonFormat') }}
                        </Button>
                      </Tooltip>
                    </div>
                  </div>
                </Form.Item>
              </Col>
            </template>
          </Row>

          <!-- 其他设置 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionOther') }}</div>
          <Row :gutter="16">
            <Col :xs="24">
              <Form.Item :label="$t('page.quartz.jobPage.description')" name="description"
                :rules="[{ max: 500, message: $t('page.quartz.jobPage.descriptionMaxLen') }]">
                <Textarea v-model:value="editForm.description"
                  :placeholder="$t('page.quartz.jobPage.placeholderDescription')" :rows="3" />
              </Form.Item>
            </Col>
            <Col :xs="24">
              <Form.Item :label="$t('page.quartz.jobPage.isEnabled')" name="isEnabled" valuePropName="checked">
                <Switch v-model:checked="editForm.isEnabled" />
              </Form.Item>
            </Col>
          </Row>
        </Form>

        <template #footer>
          <Space>
            <Button @click="editModalVisible = false">{{ $t('page.quartz.jobPage.cancel') }}</Button>
            <Button type="primary" @click="handleSave">{{ $t('page.quartz.jobPage.save') }}</Button>
          </Space>
        </template>
      </Modal>

      <!-- Cron 表达式生成器 -->
      <CronHelperModal v-model:visible="cronHelperVisible" :current-expression="editForm.cronExpression"
        @cancel="closeCronHelper" @select="selectCronExpression" />
    </template>
  </Page>
</template>

<style scoped>
/* VbenAdmin 风格样式优化 */
.mb-4 {
  margin-bottom: 16px;
}

.text-right {
  text-align: right;
}

.flex {
  display: flex;
}

.w-full {
  width: 100%;
}

.items-center {
  align-items: center;
}

.justify-between {
  justify-content: space-between;
}

/* ====== 表单分区标题 ====== */
.form-section-title {
  font-size: 13px;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin: 4px 0 14px;
  padding-left: 8px;
  border-left: 3px solid hsl(var(--primary));
  line-height: 1;
}

.form-section-title:not(:first-child) {
  margin-top: 8px;
}



/* ====== 调度设置：两列混排，标签列与表单其余分区保持一致 ====== */
.schedule-grid :deep(.ant-form-item) {
  margin-bottom: 12px;
}

.schedule-grid :deep(.ant-form-item-row) {
  flex-wrap: nowrap;
  align-items: center;
}

.schedule-grid :deep(.ant-form-item-label) {
  display: flex;
  flex: 0 0 110px;
  width: 110px;
  align-items: center;
  justify-content: flex-end;
  padding-right: 6px;
}

/* 同行右侧字段：标签宽度随文案伸缩，避免英文长标签溢出到左侧字段 */
.schedule-grid .schedule-col--inline-label :deep(.ant-form-item-label) {
  flex: 0 0 auto;
  /* 覆盖 Form labelCol 注入的内联 width */
  width: auto !important;
  min-width: 110px;
}

.schedule-grid :deep(.ant-form-item-label > label) {
  justify-content: flex-end;
  height: auto;
  line-height: 1.4;
  font-size: 13px;
  white-space: nowrap;
  text-align: right;
}



.schedule-grid :deep(.ant-form-item-control) {
  flex: 1 1 auto;
  min-width: 0;
}

/* ====== 失败重试：无外框组合，与同行下拉框控件基线对齐 ====== */
.retry-group {
  display: inline-flex;
  align-items: center;
  flex-wrap: nowrap;
  max-width: 100%;
  min-height: 32px;
  gap: 8px;
}

/* 双列布局下输入框可收缩，避免撑破同行另一列 */
.retry-group__input {
  width: 88px;
  flex: 0 1 88px;
  min-width: 64px;
}

.retry-group__input--interval {
  width: 96px;
  flex-basis: 96px;
}

.retry-group__unit,
.retry-group__inline-label {
  font-size: 13px;
  line-height: 1;
  white-space: nowrap;
  color: hsl(var(--muted-foreground));
}

.retry-group__inline-label {
  color: hsl(var(--foreground));
}

.retry-group__sep {
  width: 1px;
  height: 16px;
  margin: 0 4px;
  background: hsl(var(--border));
}

/* ====== JSON 字段格式化按钮 ====== */
.json-field {
  position: relative;
}

.json-field .json-format-btn {
  position: absolute;
  right: 4px;
  bottom: 2px;
  padding: 0 4px;
  height: 22px;
  font-size: 12px;
  z-index: 1;
}

:deep(.json-textarea) textarea,
.json-textarea :deep(textarea) {
  font-family: 'Consolas', 'Monaco', 'Menlo', 'Courier New', monospace !important;
  font-size: 13px !important;
  line-height: 1.5 !important;
}

/* ====== API 方法颜色标签 ====== */
.api-method-option {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.api-method-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}

/* ====== API 超时字段 ====== */
.api-timeout-field {
  display: flex;
  align-items: center;
  gap: 8px;
}

.api-timeout-unit {
  font-size: 13px;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

/* ====== API 请求头 Key-Value 编辑器 ====== */
.api-headers-editor {
  width: 100%;
}

.header-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.header-row__key {
  flex: 2;
  min-width: 0;
}

.header-row__value {
  flex: 3;
  min-width: 0;
}

.header-row__remove {
  flex-shrink: 0;
  padding: 0 4px !important;
  height: 24px;
  width: 24px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.header-common-select {
  width: 160px;
}

/* ====== API 请求体编辑器 ====== */
.api-body-editor {
  width: 100%;
}

.api-body-hint {
  margin-bottom: 8px;
}

/* ====== 调度器状态条 ====== */
.toolbar-shell {
  display: flex;
  width: 100%;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.scheduler-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 4px 6px 4px 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
  background: hsl(var(--card));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.04);
}

.scheduler-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}

.scheduler-actions :deep(.scheduler-btn-icon) {
  width: 13px;
  height: 13px;
  margin-right: 4px;
}

.scheduler-tip {
  font-size: 12px;
  line-height: 1.6;
  white-space: nowrap;
}

.toolbar-actions {
  flex-shrink: 0;
}

/* 列表状态图标仅展示，不显示焦点框 */
.rerun-state-icon:focus,
.rerun-state-icon:focus-visible {
  outline: none;
}

.scheduler-status {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  min-width: 84px;
  font-size: 12px;
  font-weight: 500;
  padding: 4px 11px 4px 9px;
  border-radius: 999px;
  line-height: 1;
  letter-spacing: 0.01em;
  cursor: default;
  transition: background 0.2s ease;
}

.scheduler-status__text {
  max-width: 190px;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.scheduler-status__count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 16px;
  height: 15px;
  padding: 0 4px;
  border-radius: 999px;
  background: hsl(var(--background) / 0.85);
  font-size: 10px;
  font-weight: 600;
  line-height: 1;
}

.scheduler-status .status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}

.scheduler-status.is-running {
  color: hsl(var(--success));
  background: hsl(var(--success) / 0.08);
  border: 1px solid hsl(var(--success) / 0.2);
}

.scheduler-status.is-running .status-dot {
  background: hsl(var(--success));
  box-shadow: 0 0 0 3px hsl(var(--success) / 0.15);
  animation: pulse 2s ease-in-out infinite;
}

.scheduler-status.is-stopped {
  color: hsl(var(--destructive));
  background: hsl(var(--destructive) / 0.08);
  border: 1px solid hsl(var(--destructive) / 0.2);
}

.scheduler-status.is-stopped .status-dot {
  background: hsl(var(--destructive));
}

.scheduler-status.is-draining {
  color: hsl(var(--warning));
  background: hsl(var(--warning) / 0.08);
  border: 1px solid hsl(var(--warning) / 0.2);
}

.scheduler-status.is-draining .status-dot {
  background: hsl(var(--warning));
  box-shadow: 0 0 0 3px hsl(var(--warning) / 0.15);
  animation: pulse 1.4s ease-in-out infinite;
}

@keyframes pulse {

  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.5;
  }
}

.scheduler-sep {
  width: 1px;
  height: 16px;
  background: hsl(var(--border));
  display: inline-block;
}
</style>