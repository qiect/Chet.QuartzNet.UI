<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import { Page } from '@vben/common-ui';
import {
  Button,
  Input,
  Select,
  Space,
  Modal,
  Form,
  Switch,
  message,
  notification,
  Tag,
  Table,
  Card,
  Row,
  Col,
  Dropdown,
  Menu,
  Tooltip,
} from 'ant-design-vue';
import type { FormInstance, PaginationProps } from 'ant-design-vue';
// 导入Cron帮助组件
import CronHelperModal from './components/cron-helper.vue';

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
const dataSource = ref<QuartzJobResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);
// 批量删除相关
const selectedRowKeys = ref<string[]>([]);
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

// 搜索条件
// 添加表单实例引用
const searchFormRef = ref<FormInstance>();
const searchForm = ref<Partial<QuartzJobQueryDto>>({
  jobName: '',
  jobGroup: '',
  status: undefined,
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
  skipSslValidation: false,
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

// 排序配置
const sortBy = ref<string>('');
const sortOrder = ref<string>('');

// 计算编辑模态框标题
const editModalDisplayTitle = computed(() => {
  if (editModalTitle.value === 'edit') return $t('page.quartz.jobPage.editJob');
  if (editModalTitle.value === 'copy') return $t('page.quartz.jobPage.copyJob');
  return $t('page.quartz.jobPage.addJob');
});

// 判断是否为编辑模式
const isEditMode = computed(() => editModalTitle.value === 'edit');

// 列配置（使用computed属性，当排序状态变化时自动更新）
const columns = computed(() => [
  {
    title: $t('page.quartz.jobPage.jobName'),
    dataIndex: 'jobName',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'jobName' ? sortOrder.value : undefined,
  },
  {
    title: $t('page.quartz.jobPage.jobGroup'),
    dataIndex: 'jobGroup',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'jobGroup' ? sortOrder.value : undefined,
  },
  {
    title: $t('page.quartz.jobPage.jobType'),
    dataIndex: 'jobType',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      const type = jobTypeMap[record.jobType];
      return h(Tag, { color: type?.color || 'default' }, {
        default: () => type?.text || $t('page.quartz.jobPage.unknown')
      });
    },
  },
  {
    title: $t('page.quartz.jobPage.jobClassOrApi'),
    dataIndex: 'jobClassOrApi',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.jobPage.cronExpression'),
    dataIndex: 'cronExpression',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.jobPage.previousRun'),
    dataIndex: 'previousRunTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'previousRunTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.previousRunTime
        ? formatDateTime(record.previousRunTime)
        : '-';
    },
  },
  {
    title: $t('page.quartz.jobPage.nextRun'),
    dataIndex: 'nextRunTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'nextRunTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.nextRunTime ? formatDateTime(record.nextRunTime) : '-';
    },
  },
  {
    title: $t('page.quartz.jobPage.status'),
    dataIndex: 'status',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      const status = jobStatusMap[record.status];
      return h(
        Tag,
        { color: status?.status || 'default' },
        {
          default: () => status?.text?.() || record.status || $t('page.quartz.jobPage.unknown')
        }
      );
    },
  },
  {
    title: $t('page.quartz.jobPage.isEnabled'),
    dataIndex: 'isEnabled',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) =>
      h(Switch, { checked: record.isEnabled, disabled: true }),
  },
  {
    title: $t('page.quartz.jobPage.createTime'),
    dataIndex: 'createTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'createTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.createTime ? formatDateTime(record.createTime) : '-';
    },
  },
  {
    title: $t('page.quartz.jobPage.action'),
    key: 'action',
    width: 80,
    fixed: 'right',
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      // 创建下拉菜单
      const menu = h(Menu, {}, [
        h(
          Menu.Item,
          {
            onClick: () => handleEdit(record),
          },
          {
            default: () => $t('page.quartz.jobPage.edit')
          },
        ),
        h(
          Menu.Item,
          {
            onClick: () => handleCopyJob(record),
          },
          {
            default: () => $t('page.quartz.jobPage.copy')
          },
        ),
        h(
          Menu.Item,
          {
            onClick: () => handleDelete(record),
            danger: true,
          },
          {
            default: () => $t('page.quartz.jobPage.delete')
          },
        ),
        h(
          Menu.Item,
          {
            onClick: () =>
              record.status === JobStatusEnum.Normal
                ? handleStop(record)
                : handleResume(record),
            style: {
              color: record.status === JobStatusEnum.Normal ? '#faad14' : '#52c41a',
            },
          },
          {
            default: () => (record.status === JobStatusEnum.Normal ? $t('page.quartz.jobPage.stop') : $t('page.quartz.jobPage.resume'))
          },
        ),
        h(
          Menu.Item,
          {
            onClick: () => handleExecute(record),
            style: {
              color: '#1890ff',
            },
          },
          {
            default: () => $t('page.quartz.jobPage.executeNow')
          },
        ),
      ]);

      return h(
        Dropdown,
        {
          trigger: ['click'],
          overlay: menu,
        },
        {
          default: () =>
            h(
              Button,
              {
                type: 'primary',
                disabled: loading.value,
              },
              {
                default: () => $t('page.quartz.jobPage.action')
              },
            ),
        },
      );
    },
  },
]);

// 分页配置
const pagination = computed<PaginationProps>(() => ({
  current: currentPage.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  showQuickJumper: true,
  showTotal: (total, range) => $t('page.quartz.jobPage.paginationTotal', { start: range[0], end: range[1], total }),
  pageSizeOptions: ['10', '20', '50', '100'],
}));

// 处理表格变化事件（分页、排序）
const handleTableChange = (pagination: any, filters: any, sorter: any) => {
  // 处理分页变化
  if (pagination.current !== undefined) {
    currentPage.value = pagination.current;
  }
  if (pagination.pageSize !== undefined) {
    pageSize.value = pagination.pageSize;
  }

  // 处理排序变化
  if (sorter.field !== undefined) {
    sortBy.value = sorter.field;
    sortOrder.value =
      sorter.order === 'ascend'
        ? 'ascend'
        : sorter.order === 'descend'
          ? 'descend'
          : undefined;
  }

  // 重新加载数据
  loadJobList();
};

// 加载作业列表
const loadJobList = async () => {
  loading.value = true;
  try {
    const response = await getJobs({
      pageIndex: currentPage.value,
      pageSize: pageSize.value,
      jobName: searchForm.value.jobName,
      jobGroup: searchForm.value.jobGroup,
      status: searchForm.value.status,
      sortBy: sortBy.value,
      sortOrder: sortOrder.value,
    });

    dataSource.value = response.data?.items || [];
    total.value = response.data?.totalCount || 0;
  } catch (error) {
    message.error($t('page.quartz.jobPage.jobListFailed'));
    console.error($t('page.quartz.jobPage.jobListFailed'), error);
  } finally {
    loading.value = false;
  }
};



// 处理搜索
const handleSearch = async () => {
  if (searchFormRef.value) {
    // 触发表单验证（如果需要）
    await searchFormRef.value.validateFields();
  }
  currentPage.value = 1;
  loadJobList();
};

// 处理重置
const handleReset = () => {
  searchForm.value = {
    jobName: '',
    jobGroup: '',
    status: undefined,
  };
  currentPage.value = 1;
  loadJobList();
};

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
      skipSslValidation: response.data?.skipSslValidation || false,
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
      skipSslValidation: response.data?.skipSslValidation || false,
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
      skipSslValidation: editForm.skipSslValidation,
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
      loadJobList();
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
        loadJobList();
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
    loadJobList();
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
    loadJobList();
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

// 批量删除作业
const handleBatchDelete = () => {
  Modal.confirm({
    title: $t('page.quartz.jobPage.confirmBatchDelete'),
    content: $t('page.quartz.jobPage.confirmBatchDeleteContent', { count: selectedRowKeys.value.length }),
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    async onOk() {
      try {
        // 检查是否有选中的作业
        if (selectedRowKeys.value.length === 0) {
          message.warning($t('page.quartz.jobPage.selectDeleteFirst'));
          return;
        }

        // 准备删除参数 - 使用与后端匹配的格式
        const jobList = selectedRows.value.map(job => {
          return {
            JobName: job.jobName,
            JobGroup: job.jobGroup
          };
        });

        const result = await batchDeleteJob(jobList);

        if (result.success) {
          message.success($t('page.quartz.jobPage.batchDeleteSuccess'));
          // 清空选择
          selectedRowKeys.value = [];
          selectedRows.value = [];
          // 重新加载作业列表
          loadJobList();
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
      loadJobList();
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
          loadJobList();
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
      const parsed = JSON.parse(value);
      editForm[property] = JSON.stringify(parsed, null, 2);
      message.success($t('page.quartz.jobPage.jsonFormatSuccess'));
    }
  } catch (error) {
    message.error($t('page.quartz.jobPage.invalidJson'));
  }
};

// 生命周期
onMounted(async () => {
  await getSchedulerStatusInfo();
  loadJobList();
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <Card class="mb-4">
        <Form ref="searchFormRef" :model="searchForm" layout="horizontal" :label-align="'right'">
          <Row :gutter="16">
            <Col :xs="24" :sm="12" :md="8" :lg="8" :xl="6">
              <Form.Item :label="$t('page.quartz.jobPage.jobName')" name="jobName">
                <Input v-model:value="searchForm.jobName" :placeholder="$t('page.quartz.jobPage.placeholderJobName')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="8" :xl="6">
              <Form.Item :label="$t('page.quartz.jobPage.jobGroup')" name="jobGroup">
                <Input v-model:value="searchForm.jobGroup" :placeholder="$t('page.quartz.jobPage.placeholderJobGroup')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="8" :xl="6">
              <Form.Item :label="$t('page.quartz.jobPage.status')" name="status">
                <Select v-model:value="searchForm.status" :placeholder="$t('page.quartz.jobPage.placeholderStatus')" allowClear>
                  <Select.Option :value="JobStatusEnum.Normal">{{ $t('page.quartz.jobPage.statusNormal') }}</Select.Option>
                  <Select.Option :value="JobStatusEnum.Paused">{{ $t('page.quartz.jobPage.statusPaused') }}</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" :lg="24" :xl="6" class="text-right">
              <Space>
                <Button type="primary" @click="handleSearch"> {{ $t('page.quartz.jobPage.search') }} </Button>
                <Button @click="handleReset"> {{ $t('page.quartz.jobPage.reset') }} </Button>
              </Space>
            </Col>
          </Row>
        </Form>
      </Card>

      <!-- 作业管理卡片 -->
      <Card>
        <div class="mb-4 flex items-center justify-between">
          <Space>
            <Button type="primary" :disabled="schedulerStatus.isStarted" @click="handleStartScheduler">
              {{ $t('page.quartz.jobPage.startScheduler') }}
            </Button>
            <Button danger :disabled="!schedulerStatus.isStarted || schedulerStatus.isShutdown"
              @click="handleStopScheduler">
              {{ $t('page.quartz.jobPage.stopScheduler') }}
            </Button>
            <Tag :color="schedulerStatus.isStarted ? 'success' : 'error'">
              {{ schedulerStatus.status }}
            </Tag>
          </Space>
          <Space>
            <Button type="primary" @click="handleAdd"> {{ $t('page.quartz.jobPage.addJob') }} </Button>
            <Button danger :disabled="selectedRowKeys.length === 0" @click="handleBatchDelete"> {{ $t('page.quartz.jobPage.batchDelete') }} </Button>
          </Space>
        </div>
        <!-- 作业列表 -->
        <Table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading"
          :rowKey="(record) => `${record.jobName}-${record.jobGroup}`" @change="handleTableChange" size="middle"
          :scroll="{ x: 'max-content' }" :row-selection="{
            selectedRowKeys: selectedRowKeys,
            onChange: (keys: string[], rows: QuartzJobResponseDto[]) => {
              selectedRowKeys = keys;
              selectedRows = rows;
            }
          }" />
      </Card>

      <!-- 新增编辑对话框 -->
      <Modal v-model:open="editModalVisible" :title="editModalDisplayTitle" width="800px" :body-style="{ padding: '24px' }"
        destroyOnClose @cancel="editModalVisible = false">
        <Form ref="formRef" :model="editForm" layout="horizontal" :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }"
          :label-align="'right'">
          <Row :gutter="16">
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobName')" name="jobName"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobNameRequired') }, { max: 100, message: $t('page.quartz.jobPage.jobNameMaxLen') }]">
                <Input v-model:value="editForm.jobName" :placeholder="$t('page.quartz.jobPage.placeholderJobName')" :disabled="isEditMode" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobGroup')" name="jobGroup"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobGroupRequired') }, { max: 100, message: $t('page.quartz.jobPage.jobGroupMaxLen') }]">
                <Input v-model:value="editForm.jobGroup" :placeholder="$t('page.quartz.jobPage.placeholderJobGroup')" :disabled="isEditMode" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.cronExpression')" name="cronExpression"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.cronRequired') }, { max: 200, message: $t('page.quartz.jobPage.cronMaxLen') }]">
                <Space.Compact style="width: 100%">
                  <Input v-model:value="editForm.cronExpression" :placeholder="$t('page.quartz.jobPage.cronPlaceholder')" style="flex: 1" />
                  <Tooltip :title="$t('page.quartz.jobPage.cronHelper')">
                    <Button type="default" @click="openCronHelper"> 🤔 </Button>
                  </Tooltip>
                </Space.Compact>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobType')" name="jobType" :rules="[{ required: true, message: $t('page.quartz.jobPage.jobTypeRequired') }]">
                <Select v-model:value="editForm.jobType" @change="handleJobTypeChange">
                  <Select.Option :value="JobTypeEnum.DLL">DLL</Select.Option>
                  <Select.Option :value="JobTypeEnum.API">API</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.jobClassOrApi')" name="jobClassOrApi"
                :rules="[{ required: true, message: $t('page.quartz.jobPage.jobClassOrApiRequired') }, { max: 500, message: $t('page.quartz.jobPage.jobClassOrApiMaxLen') }]">
                <Select v-model:value="editForm.jobClassOrApi" :placeholder="$t('page.quartz.jobPage.selectJobClassOrApi')" showSearch allowClear
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
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.DLL">
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
                <div class="relative">
                  <Tooltip :title="$t('page.quartz.jobPage.jobDataTooltip')">
                    <Input.TextArea v-model:value="editForm.jobData" :placeholder="$t('page.quartz.jobPage.placeholderJobData')" :rows="4" />
                  </Tooltip>
                  <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                    <Button type="link" size="small" style="position: absolute; right: 8px; bottom: 8px;"
                      @click="formatJson('jobData')">
                      😄
                    </Button>
                  </Tooltip>
                </div>
              </Form.Item>
            </Col>
            <!-- API相关配置 -->
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.API">
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
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.API">
              <Form.Item :label="$t('page.quartz.jobPage.skipSsl')" name="skipSslValidation" valuePropName="checked">
                <Switch v-model:checked="editForm.skipSslValidation" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.API">
              <Form.Item :label="$t('page.quartz.jobPage.apiTimeout')" name="apiTimeout" :rules="[
                {
                  required: true,
                  message: $t('page.quartz.jobPage.apiTimeoutRequired'),
                  type: 'number',
                },
                { type: 'number', min: 1, max: 99999, message: $t('page.quartz.jobPage.apiTimeoutRange') },
              ]">
                <Input type="number" v-model:value.number="editForm.apiTimeout" :placeholder="$t('page.quartz.jobPage.placeholderApiTimeout')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.API">
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
                <div class="relative">
                  <Input.TextArea v-model:value="editForm.apiHeaders"
                    :placeholder="$t('page.quartz.jobPage.placeholderApiHeaders')" :rows="3" />
                  <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                    <Button type="link" size="small" style="position: absolute; right: 8px; bottom: 8px;"
                      @click="formatJson('apiHeaders')">
                      😄
                    </Button>
                  </Tooltip>
                </div>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" v-if="editForm.jobType === JobTypeEnum.API">
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
                <div class="relative">
                  <Input.TextArea v-model:value="editForm.apiBody" :placeholder="$t('page.quartz.jobPage.placeholderApiBody')" :rows="4" />
                  <Tooltip :title="$t('page.quartz.jobPage.jsonFormat')">
                    <Button type="link" size="small" style="position: absolute; right: 8px; bottom: 8px;"
                      @click="formatJson('apiBody')">
                      😄
                    </Button>
                  </Tooltip>
                </div>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item :label="$t('page.quartz.jobPage.description')" name="description" :rules="[{ max: 500, message: $t('page.quartz.jobPage.descriptionMaxLen') }]">
                <Input.TextArea v-model:value="editForm.description" :placeholder="$t('page.quartz.jobPage.placeholderDescription')" :rows="3" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
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

      <!-- Cron帮助模态框 -->
      <CronHelperModal v-model:visible="cronHelperVisible" @cancel="closeCronHelper" @select="selectCronExpression" />
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

.text-sm {
  font-size: 14px;
}

.font-medium {
  font-weight: 500;
}

.text-gray-700 {
  color: rgba(0, 0, 0, 0.65);
}

.pl-6 {
  padding-left: 24px;
}

.pr-6 {
  padding-right: 24px;
}
</style>
