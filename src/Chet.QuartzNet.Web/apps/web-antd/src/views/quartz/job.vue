<script setup lang="ts">
import { ref, computed, onMounted, reactive, nextTick } from 'vue';
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
} from 'ant-design-vue';
import type { FormInstance } from 'ant-design-vue';
// 导入Cron帮助组件
import CronHelperModal from './components/cron-helper.vue';
// 导入可拖动 Modal 组合式函数
import { useDraggableModal } from './composables/use-draggable-modal';

// 导入i18n
import { $t } from '#/locales';

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
  triggerJob,
  pauseJob,
  resumeJob,
  getSchedulerStatus,
  startScheduler,
  stopScheduler,
  getJobClasses,
} from '../../api/quartz/job';
import type {
  QuartzJobDto,
  QuartzJobResponseDto,
  QuartzJobQueryDto,
} from '../../api/quartz/job';

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
// 批量删除相关
const selectedRows = ref<QuartzJobResponseDto[]>([]);

// 调度器状态
const schedulerStatus = ref({
  schedulerName: '',
  schedulerInstanceId: '',
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
});

// 编辑对话框
const editModalVisible = ref(false);
const editModalTitle = ref('add');
const editForm = reactive<QuartzJobDto>({
  jobName: '',
  jobGroup: '',
  jobType: JobTypeEnum.DLL,
  jobClassOrApi: '',
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
  startTime: undefined,
  endTime: undefined,
  isEnabled: true,
});

const formRef = ref<FormInstance>();

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

// 列配置
const columns = [
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
    title: $t('page.quartz.jobPage.action'),
    width: 70,
    align: 'center' as const,
    fixed: 'right',
    slots: { default: 'action' },
  },
];

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
  columns: columns as any,
  height: 'auto',
  showOverflow: true,
  rowConfig: { keyField: '_rowKey', isHover: true },
  sortConfig: {
    trigger: 'cell',
    remote: true,
    defaultSort: savedSort,
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
          } catch { }
        }
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

// 编辑对话框支持拖动
useDraggableModal(editModalVisible, 'quartz-job-edit-modal');

// 搜索/重置由 VbenForm 内置提交按钮触发，无需手动处理

// 打开新增对话框
const handleAdd = async () => {
  editModalTitle.value = 'add';
  Object.assign(editForm, {
    jobName: '',
    jobGroup: '',
    jobType: JobTypeEnum.DLL,
    jobClassOrApi: '',
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
    startTime: undefined,
    endTime: undefined,
    isEnabled: true,
  });
  // 默认作业类型是DLL，加载作业类列表
  await loadJobClasses();
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
      startTime: response.data?.startTime || undefined,
      endTime: response.data?.endTime || undefined,
      isEnabled: response.data?.isEnabled !== false,
    };
    Object.assign(editForm, jobDetail);

    // 如果作业类型是DLL，加载作业类列表
    if (editForm.jobType === JobTypeEnum.DLL) {
      await loadJobClasses();
    }

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
      startTime: response.data?.startTime || undefined,
      endTime: response.data?.endTime || undefined,
      isEnabled: response.data?.isEnabled !== false,
    };
    Object.assign(editForm, jobDetail);

    // 如果作业类型是DLL，加载作业类列表
    if (editForm.jobType === JobTypeEnum.DLL) {
      await loadJobClasses();
    }

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
    await formRef.value.validate();

    loading.value = true;

    // 准备提交数据，确保字段名称与后端一致
    const submitData = {
      jobName: editForm.jobName,
      jobGroup: editForm.jobGroup,
      jobType: editForm.jobType,
      jobClassOrApi: editForm.jobClassOrApi,
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

// 获取调度器状态
const getSchedulerStatusInfo = async () => {
  try {
    const response = await getSchedulerStatus();
    if (response.success && response.data) {
      // 直接使用后端返回的数据，不进行命名转换
      schedulerStatus.value = response.data;
    }
  } catch (error) {
    console.error($t('page.quartz.jobPage.schedulerStatusFailed'), error);
    message.error($t('page.quartz.jobPage.schedulerStatusFailed'));
  }
};

// 启动调度器
const handleStartScheduler = async () => {
  try {
    const response = await startScheduler();
    if (response.success) {
      message.success($t('page.quartz.jobPage.schedulerStartedSuccess'));
      await getSchedulerStatusInfo();
      gridApi.query();
    }
  } catch (error) {
    console.error($t('page.quartz.jobPage.schedulerStartFailed'), error);
    message.error($t('page.quartz.jobPage.schedulerStartFailed'));
  }
};

// 停止调度器
const handleStopScheduler = () => {
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmStopScheduler'),
    content: $t('page.quartz.jobPage.confirmStopSchedulerContent'),
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      try {
        const response = await stopScheduler();
        if (response.success) {
          message.success($t('page.quartz.jobPage.schedulerStoppedSuccess'));
          await getSchedulerStatusInfo();
          gridApi.query();
        }
      } catch (error) {
        console.error($t('page.quartz.jobPage.schedulerStopFailed'), error);
        message.error($t('page.quartz.jobPage.schedulerStopFailed'));
      }
    },
  });
};

// JSON 格式化函数
const formatJson = (property: keyof QuartzJobDto) => {
  try {
    const value = editForm[property];
    if (value) {
      const parsed = JSON.parse(value as string);
      (editForm as any)[property] = JSON.stringify(parsed, null, 2);
      message.success($t('page.quartz.jobPage.jsonFormatSuccess'));
    }
  } catch (error) {
    message.error($t('page.quartz.jobPage.invalidJson'));
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
  // 数据加载后恢复排序视觉状态
  await nextTick();
  try {
    const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
    if (saved) {
      gridApi.grid?.setSort({ field: saved.field, order: saved.order });
    }
  } catch { }
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <!-- 作业管理 -->
      <Grid>
        <!-- 工具栏：左侧调度器控制 + 右侧作业操作 -->
        <template #toolbar-actions>
          <div class="flex w-full items-center justify-between">
            <div class="scheduler-bar">
              <span class="scheduler-status" :class="schedulerStatus.isStarted ? 'is-running' : 'is-stopped'">
                <i class="status-dot"></i>
                {{ schedulerStatus.status }}
              </span>
              <span class="scheduler-sep"></span>
              <Button type="primary" :disabled="schedulerStatus.isStarted" @click="handleStartScheduler">
                {{ $t('page.quartz.jobPage.startScheduler') }}
              </Button>
              <Button danger :disabled="!schedulerStatus.isStarted || schedulerStatus.isShutdown"
                @click="handleStopScheduler">
                {{ $t('page.quartz.jobPage.stopScheduler') }}
              </Button>
            </div>
            <Space>
              <Button type="primary" @click="handleAdd"> {{ $t('page.quartz.jobPage.addJob') }} </Button>
              <Button danger :disabled="selectedRows.length === 0" @click="handleBatchDelete">
                {{ $t('page.quartz.jobPage.batchDelete') }}
              </Button>
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
      <Modal v-model:open="editModalVisible" :title="editModalDisplayTitle" width="760px"
        :body-style="{ padding: '24px' }" wrapClassName="quartz-job-edit-modal" destroyOnClose
        @cancel="editModalVisible = false">
        <Form ref="formRef" :model="editForm" layout="horizontal" :label-col="{ style: { width: '110px' } }"
          :wrapper-col="{ flex: 1 }">
          <!-- 基本信息 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionBasic') }}</div>
          <Row :gutter="16">
            <Col :xs="24" :md="12">
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
          <Row :gutter="16">
            <Col :xs="24">
              <Form.Item :label="$t('page.quartz.jobPage.cronExpression')" name="cronExpression"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.cronRequired') }, { max: 200, message: $t('page.quartz.jobPage.cronMaxLen') }]">
                <Space.Compact style="width: 100%">
                  <Input v-model:value="editForm.cronExpression"
                    :placeholder="$t('page.quartz.jobPage.cronPlaceholder')" style="flex: 1" />
                  <Button @click="openCronHelper">{{ $t('page.quartz.jobPage.cronGenerator') }}</Button>

                </Space.Compact>
              </Form.Item>
            </Col>
            <!-- 失败重试 + 禁止并发：同一行布局 -->
            <Col :xs="24">
              <Form.Item name="retryCount" :rules="[
                { type: 'number', min: 0, max: 10, message: $t('page.quartz.jobPage.retryCountRange') },
              ]">
                <template #label>
                  <Tooltip :title="$t('page.quartz.jobPage.retryCountHint')">
                    <i class="vxe-icon-question-circle-fill retry-hint-icon"></i>
                    {{ $t('page.quartz.jobPage.retryLabel') }}
                  </Tooltip>
                </template>
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
                  <span class="retry-group__divider" aria-hidden="true"></span>
                  <Tooltip :title="$t('page.quartz.jobPage.disallowConcurrentHint')">
                    <span class="retry-group__inline-label" style="cursor: help;">
                      <i class="vxe-icon-question-circle-fill retry-hint-icon"></i>
                      {{ $t('page.quartz.jobPage.disallowConcurrent') }}
                    </span>
                  </Tooltip>
                  <Switch v-model:checked="editForm.disallowConcurrentExecution" />
                </div>
              </Form.Item>
            </Col>
          </Row>

          <!-- 作业配置 -->
          <div class="form-section-title">{{ $t('page.quartz.jobPage.sectionConfig') }}</div>
          <Row :gutter="16">
            <Col :xs="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobClassOrApi')" name="jobClassOrApi"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobClassOrApiRequired') }, { max: 500, message: $t('page.quartz.jobPage.jobClassOrApiMaxLen') }]">
                <Select v-model:value="editForm.jobClassOrApi"
                  :placeholder="$t('page.quartz.jobPage.selectJobClassOrApi')" showSearch allowClear
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
                  <Input.TextArea v-model:value="editForm.jobData"
                    :placeholder="$t('page.quartz.jobPage.placeholderJobData')" :rows="4" />
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
                    <Select.Option value="GET">GET</Select.Option>
                    <Select.Option value="POST">POST</Select.Option>
                    <Select.Option value="PUT">PUT</Select.Option>
                    <Select.Option value="DELETE">DELETE</Select.Option>
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
                  <Input type="number" v-model:value.number="editForm.apiTimeout"
                    :placeholder="$t('page.quartz.jobPage.placeholderApiTimeout')" />
                </Form.Item>
              </Col>
              <Col :xs="24">
                <Form.Item :label="$t('page.quartz.jobPage.skipSsl')" name="skipSslValidation" valuePropName="checked">
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
                  <div class="json-field">
                    <Input.TextArea v-model:value="editForm.apiHeaders"
                      :placeholder="$t('page.quartz.jobPage.placeholderApiHeaders')" :rows="3" />
                    <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                      <Button type="link" size="small" class="json-format-btn" @click="formatJson('apiHeaders')">
                        {{ $t('page.quartz.jobPage.jsonFormat') }}
                      </Button>
                    </Tooltip>
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
                  <div class="json-field">
                    <Input.TextArea v-model:value="editForm.apiBody"
                      :placeholder="$t('page.quartz.jobPage.placeholderApiBody')" :rows="4" />
                    <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                      <Button type="link" size="small" class="json-format-btn" @click="formatJson('apiBody')">
                        {{ $t('page.quartz.jobPage.jsonFormat') }}
                      </Button>
                    </Tooltip>
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
                <Input.TextArea v-model:value="editForm.description"
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



/* ====== 失败重试紧凑配置组 ====== */
.retry-group {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.retry-group__input {
  width: 96px;
}

.retry-group__input--interval {
  width: 104px;
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

/* ====== 表单字段辅助提示 ====== */
.retry-hint-icon {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  cursor: pointer;
  transition: color 0.2s;
}

.retry-hint-icon:hover {
  color: hsl(var(--primary));
}

.retry-group__sep {
  width: 1px;
  height: 16px;
  margin: 0 4px;
  background: hsl(var(--border));
}

.retry-group__divider {
  width: 1px;
  height: 20px;
  margin: 0 8px;
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

/* ====== 调度器状态条 ====== */
.scheduler-bar {
  display: flex;
  align-items: center;
  gap: 10px;
}

.scheduler-status {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  font-size: 12px;
  font-weight: 500;
  padding: 4px 11px 4px 9px;
  border-radius: 999px;
  line-height: 1;
  letter-spacing: 0.01em;
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