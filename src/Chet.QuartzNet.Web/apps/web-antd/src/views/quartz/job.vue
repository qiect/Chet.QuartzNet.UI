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
} from 'ant-design-vue';
import type { FormInstance, PaginationProps } from 'ant-design-vue';
// 导入Cron帮助组件
import CronHelperModal from './components/cron-helper.vue';

// 导入作业API服务
import {
  JobTypeEnum,
  JobStatusEnum,
  getJobs,
  getJob,
  addJob,
  updateJob,
  deleteJob,
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
  [JobStatusEnum.Normal]: { text: '正常', status: 'success' },
  [JobStatusEnum.Paused]: { text: '已暂停', status: 'error' },
  [JobStatusEnum.Completed]: { text: '已完成', status: 'default' },
  [JobStatusEnum.Error]: { text: '错误', status: 'error' },
  [JobStatusEnum.Blocked]: { text: '阻塞', status: 'warning' },
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<QuartzJobResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(10);

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
const editModalTitle = ref('新增作业');
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
    console.error('获取作业类列表失败:', error);
    message.error('获取作业类列表失败');
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

// 列配置（使用computed属性，当排序状态变化时自动更新）
const columns = computed(() => [
  {
    title: '作业名称',
    dataIndex: 'jobName',
    ellipsis: true,
    sorter: true,
    fixed: 'left',
    width: 300,
    sortOrder: sortBy.value === 'jobName' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
  },
  {
    title: '作业分组',
    dataIndex: 'jobGroup',
    ellipsis: true,
    sorter: true,
    fixed: 'left',
    width: 300,
    sortOrder: sortBy.value === 'jobGroup' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
  },
  {
    title: '作业类型',
    dataIndex: 'jobType',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      const type = jobTypeMap[record.jobType];
      return h(Tag, { color: type?.color || 'default' }, type?.text || '未知');
    },
  },
  {
    title: '作业类名/API',
    dataIndex: 'jobClassOrApi',
    ellipsis: true,
  },
  {
    title: 'cron表达式',
    dataIndex: 'cronExpression',
    ellipsis: true,
  },
  {
    title: '上次执行',
    dataIndex: 'previousRunTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'previousRunTime' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.previousRunTime
        ? formatDateTime(record.previousRunTime)
        : '-';
    },
  },
  {
    title: '下次执行',
    dataIndex: 'nextRunTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'nextRunTime' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.nextRunTime ? formatDateTime(record.nextRunTime) : '-';
    },
  },
  {
    title: '状态',
    dataIndex: 'status',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      const status = jobStatusMap[record.status];
      return h(
        Tag,
        { color: status?.status || 'default' },
        status?.text || record.status || '未知',
      );
    },
  },
  {
    title: '是否启用',
    dataIndex: 'isEnabled',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzJobResponseDto }) =>
      h(Switch, { checked: record.isEnabled, disabled: true }),
  },
  {
    title: '创建时间',
    dataIndex: 'createTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'createTime' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      return record.createTime ? formatDateTime(record.createTime) : '-';
    },
  },
  {
    title: '操作',
    key: 'action',
    width: 80,
    customRender: ({ record }: { record: QuartzJobResponseDto }) => {
      // 创建下拉菜单
      const menu = h(Menu, {}, [
        h(
          Menu.Item,
          {
            onClick: () => handleEdit(record),
          },
          '编辑',
        ),
        h(
          Menu.Item,
          {
            onClick: () => handleDelete(record),
            danger: true,
          },
          '删除',
        ),
        h(
          Menu.Item,
          {
            onClick: () =>
              record.status === JobStatusEnum.Normal
                ? handleStop(record)
                : handleResume(record),
          },
          record.status === JobStatusEnum.Normal ? '停止' : '恢复',
        ),
        h(
          Menu.Item,
          {
            onClick: () => handleExecute(record),
          },
          '立即执行',
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
              '操作',
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
  showTotal: (total, range) => `${range[0]}-${range[1]} 共 ${total} 条`,
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
    // 根据表格组件返回的排序状态直接设置，表格组件会自动处理切换逻辑（升序→降序→取消）
    sortOrder.value = sorter.order === 'ascend' ? 'asc' : sorter.order === 'descend' ? 'desc' : '';
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
    message.error('获取作业列表失败');
    console.error('获取作业列表失败:', error);
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
  editModalTitle.value = '新增作业';
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

// 打开编辑对话框
const handleEdit = async (job: QuartzJobResponseDto) => {
  loading.value = true;
  try {
    const response = await getJob(job.jobName, job.jobGroup);
    editModalTitle.value = '编辑作业';
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
    message.error('获取作业详情失败');
    console.error('获取作业详情失败:', error);
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
    // 将apiTimeout从秒转换为毫秒（前端输入的是秒，后端期望的是毫秒）
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

    if (
      editForm.jobName &&
      editForm.jobGroup &&
      editModalTitle.value === '编辑作业'
    ) {
      // 更新作业
      await updateJob(submitData);
      message.success('作业更新成功');
    } else {
      // 新增作业
      await addJob(submitData);
      message.success('作业创建成功');
    }

    editModalVisible.value = false;
    loadJobList();
  } catch (error: any) {
    if (error.errorFields) {
      return; // 表单验证错误已显示
    }
    // 尝试从错误响应中提取更详细的信息
    const errorMessage =
      error.response?.data?.message ||
      error.message ||
      (editModalTitle.value === '编辑作业' ? '作业更新失败' : '作业创建失败');
    message.error(errorMessage);
    console.error('保存作业失败:', error);
  } finally {
    loading.value = false;
  }
};

// 删除作业
const handleDelete = (job: QuartzJobResponseDto) => {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除作业 "${job.jobName}" 吗？此操作不可恢复，相关的作业日志和配置也将被删除。`,
    okText: '确定',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        await deleteJob(job.jobName, job.jobGroup);
        message.success('作业删除成功');
        loadJobList();
      } catch (error) {
        message.error('作业删除失败');
        console.error('删除作业失败:', error);
      }
    },
  });
};

// 停止作业
const handleStop = async (job: QuartzJobResponseDto) => {
  try {
    await pauseJob(job.jobName, job.jobGroup);
    message.success('作业暂停成功');
    loadJobList();
  } catch (error) {
    message.error('作业暂停失败');
    console.error('暂停作业失败:', error);
  }
};

// 恢复作业
const handleResume = async (job: QuartzJobResponseDto) => {
  try {
    await resumeJob(job.jobName, job.jobGroup);
    message.success('作业恢复成功');
    loadJobList();
  } catch (error) {
    message.error('作业恢复失败');
    console.error('恢复作业失败:', error);
  }
};

// 立即执行作业
const handleExecute = async (job: QuartzJobResponseDto) => {
  try {
    await triggerJob(job.jobName, job.jobGroup);
    message.success('作业立即执行成功');
    notification.success({
      message: '作业执行通知',
      description: `作业 ${job.jobName} 已开始执行，请稍后在日志中查看执行结果`,
    });
  } catch (error) {
    message.error('作业执行失败');
    console.error('执行作业失败:', error);
  }
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
    console.error('获取调度器状态失败:', error);
    message.error('获取调度器状态失败');
  }
};

// 启动调度器
const handleStartScheduler = async () => {
  try {
    const response = await startScheduler();
    if (response.success) {
      message.success('调度器启动成功');
      await getSchedulerStatusInfo();
      loadJobList();
    }
  } catch (error) {
    console.error('启动调度器失败:', error);
    message.error('启动调度器失败');
  }
};

// 停止调度器
const handleStopScheduler = () => {
  Modal.confirm({
    title: '确认停止调度器',
    content: '确定要停止调度器吗？停止后所有作业将不再执行，直到重新启动调度器。',
    okText: '确定',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        const response = await stopScheduler();
        if (response.success) {
          message.success('调度器停止成功');
          await getSchedulerStatusInfo();
          loadJobList();
        }
      } catch (error) {
        console.error('停止调度器失败:', error);
        message.error('停止调度器失败');
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
      message.success('JSON 格式化成功');
    }
  } catch (error) {
    message.error('JSON 格式化失败');
  }
};

// 生命周期
onMounted(async () => {
  await getSchedulerStatusInfo();
  loadJobList();
});
</script>

<template>
  <Page>
    <Card class="mb-4">
      <Form
        ref="searchFormRef"
        :model="searchForm"
        layout="horizontal"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 18 }"
        :label-align="'right'"
      >
        <Row :gutter="16">
          <Col :xs="24" :sm="12" :md="8" :lg="8">
            <Form.Item label="作业名称" name="jobName">
              <Input
                v-model:value="searchForm.jobName"
                placeholder="请输入作业名称"
              />
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="12" :md="8" :lg="8">
            <Form.Item label="作业分组" name="jobGroup">
              <Input
                v-model:value="searchForm.jobGroup"
                placeholder="请输入作业分组"
              />
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="12" :md="8" :lg="8">
            <Form.Item label="作业状态" name="status">
              <Select
                v-model:value="searchForm.status"
                placeholder="请选择状态"
                allowClear
              >
                <Select.Option :value="JobStatusEnum.Normal"
                  >正常</Select.Option
                >
                <Select.Option :value="JobStatusEnum.Paused"
                  >已暂停</Select.Option
                >
                <Select.Option :value="JobStatusEnum.Completed"
                  >已完成</Select.Option
                >
                <Select.Option :value="JobStatusEnum.Error">错误</Select.Option>
                <Select.Option :value="JobStatusEnum.Blocked"
                  >阻塞</Select.Option
                >
              </Select>
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24" :lg="24" class="text-right">
            <Space>
              <Button type="primary" @click="handleSearch"> 搜索 </Button>
              <Button @click="handleReset"> 重置 </Button>
            </Space>
          </Col>
        </Row>
      </Form>
    </Card>

    <!-- 作业管理卡片 -->
    <Card>
      <div class="mb-4 flex items-center justify-between">
        <Space>
          <Button
            type="primary"
            :disabled="schedulerStatus.isStarted"
            @click="handleStartScheduler"
          >
            启动调度器
          </Button>
          <Button
            danger
            :disabled="!schedulerStatus.isStarted || schedulerStatus.isShutdown"
            @click="handleStopScheduler"
          >
            停止调度器
          </Button>
          <Tag :color="schedulerStatus.isStarted ? 'success' : 'error'">
            {{ schedulerStatus.status }}
          </Tag>
        </Space>
        <Button type="primary" @click="handleAdd"> 新增作业 </Button>
      </div>
      <!-- 作业列表 -->
      <Table
        :columns="columns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :rowKey="(record) => `${record.jobName}-${record.jobGroup}`"
        @change="handleTableChange"
        size="middle"
        :scroll="{ x: 'max-content' }"
      />
    </Card>

    <!-- 新增编辑对话框 -->
    <Modal
      v-model:visible="editModalVisible"
      :title="editModalTitle"
      width="800px"
      :body-style="{ padding: '24px' }"
      destroyOnClose
      @cancel="editModalVisible = false"
    >
      <Form
        ref="formRef"
        :model="editForm"
        layout="horizontal"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 18 }"
        :label-align="'right'"
      >
        <Row :gutter="16">
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="作业名称"
              name="jobName"
              :rules="[{ required: true, message: '请输入作业名称' }]"
            >
              <Input
                v-model:value="editForm.jobName"
                placeholder="请输入作业名称"
                :disabled="editModalTitle === '编辑作业'"
              />
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="作业分组"
              name="jobGroup"
              :rules="[{ required: true, message: '请输入作业分组' }]"
            >
              <Input
                v-model:value="editForm.jobGroup"
                placeholder="请输入作业分组"
                :disabled="editModalTitle === '编辑作业'"
              />
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="Cron表达式"
              name="cronExpression"
              :rules="[{ required: true, message: '请输入Cron表达式' }]"
            >
              <Space.Compact style="width: 100%">
                <Input
                  v-model:value="editForm.cronExpression"
                  placeholder="例如: 0 0/1 * * * ?"
                  style="flex: 1"
                />
                <Button type="default" @click="openCronHelper"> 🤔 </Button>
              </Space.Compact>
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="作业类型"
              name="jobType"
              :rules="[{ required: true, message: '请选择作业类型' }]"
            >
              <Select
                v-model:value="editForm.jobType"
                @change="handleJobTypeChange"
              >
                <Select.Option :value="JobTypeEnum.DLL">DLL</Select.Option>
                <Select.Option :value="JobTypeEnum.API">API</Select.Option>
              </Select>
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="作业类名/API"
              name="jobClassOrApi"
              :rules="[{ required: true, message: '请输入作业类名或API URL' }]"
            >
              <Select
                v-model:value="editForm.jobClassOrApi"
                placeholder="请选择或输入作业类名"
                showSearch
                allowClear
                mode="SECRET_COMBOBOX_MODE_DO_NOT_USE"
                :filter-option="
                  (input, option) => {
                    return (option?.label || '')
                      .toLowerCase()
                      .includes(input.toLowerCase());
                  }
                "
              >
                <Select.Option
                  v-for="jobClass in jobClasses"
                  :key="jobClass"
                  :value="jobClass"
                  :label="jobClass"
                >
                  {{ jobClass }}
                </Select.Option>
              </Select>
            </Form.Item>
          </Col>

          <!-- API相关配置 -->
          <Col
            :xs="24"
            :sm="24"
            :md="24"
            v-if="editForm.jobType === JobTypeEnum.API"
          >
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item
                label="API请求方法"
                name="apiMethod"
                :rules="[{ required: true, message: '请选择API请求方法' }]"
              >
                <Select v-model:value="editForm.apiMethod">
                  <Select.Option value="GET">GET</Select.Option>
                  <Select.Option value="POST">POST</Select.Option>
                  <Select.Option value="PUT">PUT</Select.Option>
                  <Select.Option value="DELETE">DELETE</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item
                label="跳过SSL验证"
                name="skipSslValidation"
                valuePropName="checked"
              >
                <Switch v-model:checked="editForm.skipSslValidation" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item
                label="API超时(秒)"
                name="apiTimeout"
                :rules="[
                  {
                    required: true,
                    message: '请输入API超时时间',
                    type: 'number',
                  },
                  { type: 'number', min: 1, message: 'API超时时间必须大于0' },
                ]"
              >
                <Input
                  type="number"
                  v-model:value.number="editForm.apiTimeout"
                  placeholder="请输入API超时时间，单位秒"
                />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item
              label="API请求头"
              name="apiHeaders"
              :rules="[
                {
                  validator: (rule, value, callback) => {
                    if (!value) return callback();
                    try {
                      JSON.parse(value);
                      callback();
                    } catch (e) {
                      callback(new Error('请输入有效的JSON格式'));
                    }
                  },
                },
              ]"
            >
              <div class="relative">
                <Input.TextArea
                  v-model:value="editForm.apiHeaders"
                  placeholder="JSON格式的请求头，例如: {'Content-Type': 'application/json'}"
                  :rows="3"
                />
                <Button
                  type="link"
                  size="small"
                  style="position: absolute; right: 8px; bottom: 8px;"
                  @click="formatJson('apiHeaders')"
                >
                  😄
                </Button>
              </div>
            </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item
              label="API请求体"
              name="apiBody"
              :rules="[
                {
                  validator: (rule, value, callback) => {
                    if (!value) return callback();
                    try {
                      JSON.parse(value);
                      callback();
                    } catch (e) {
                      callback(new Error('请输入有效的JSON格式'));
                    }
                  },
                },
              ]"
            >
              <div class="relative">
                <Input.TextArea
                  v-model:value="editForm.apiBody"
                  placeholder="JSON格式的请求体"
                  :rows="4"
                />
                <Button
                  type="link"
                  size="small"
                  style="position: absolute; right: 8px; bottom: 8px;"
                  @click="formatJson('apiBody')"
                >
                  😄
                </Button>
              </div>
            </Form.Item>
            </Col>
          </Col>

          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="作业数据"
              name="jobData"
              :rules="[
                {
                  validator: (rule, value, callback) => {
                    if (!value) return callback();
                    try {
                      JSON.parse(value);
                      callback();
                    } catch (e) {
                      callback(new Error('请输入有效的JSON格式'));
                    }
                  },
                },
              ]"
            >
              <div class="relative">
                <Input.TextArea
                  v-model:value="editForm.jobData"
                  placeholder="JSON格式的作业数据"
                  :rows="4"
                />
                <Button
                  type="link"
                  size="small"
                  style="position: absolute; right: 8px; bottom: 8px;"
                  @click="formatJson('jobData')"
                >
                  😄
                </Button>
              </div>
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item label="描述" name="description">
              <Input.TextArea
                v-model:value="editForm.description"
                placeholder="请输入作业描述"
                :rows="3"
              />
            </Form.Item>
          </Col>
          <Col :xs="24" :sm="24" :md="24">
            <Form.Item
              label="是否启用"
              name="isEnabled"
              valuePropName="checked"
            >
              <Switch v-model:checked="editForm.isEnabled" />
            </Form.Item>
          </Col>
        </Row>
      </Form>

      <template #footer>
        <Space>
          <Button @click="editModalVisible = false">取消</Button>
          <Button type="primary" @click="handleSave">保存</Button>
        </Space>
      </template>
    </Modal>

    <!-- Cron帮助模态框 -->
    <CronHelperModal
      v-model:visible="cronHelperVisible"
      @cancel="closeCronHelper"
      @select="selectCronExpression"
    />
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
