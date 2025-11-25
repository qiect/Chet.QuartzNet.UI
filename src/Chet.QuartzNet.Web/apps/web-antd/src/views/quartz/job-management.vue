<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue';
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
  Divider,
  Tag,
  Table,
  Card,
  Alert,
} from 'ant-design-vue';
import {
  SearchOutlined,
  EditOutlined,
  DeleteOutlined,
  PlusOutlined,
  StopOutlined,
  PlayCircleOutlined,
} from '@ant-design/icons-vue';
import type {
  ColumnsType,
  FormInstance,
  PaginationProps,
} from 'ant-design-vue';

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
} from '../../api/quartz/job';
import type {
  QuartzJobDto,
  QuartzJobResponseDto,
  QuartzJobQueryDto,
} from '../../api/quartz/job';

// 作业类型和状态映射
const jobTypeMap = {
  [JobTypeEnum.DLL]: { text: 'DLL', color: 'blue' },
  [JobTypeEnum.API]: { text: 'API', color: 'green' },
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

// 搜索条件
// 添加表单实例引用
const searchFormRef = ref<FormInstance>();
const searchForm = ref<Partial<QuartzJobQueryDto>>({
  jobName: '',
  jobGroup: '',
  status: undefined,
  jobType: undefined,
});

// 编辑对话框
const editModalVisible = ref(false);
const editModalTitle = ref('新增作业');
const editForm = reactive<QuartzJobDto>({
  jobName: '',
  jobGroup: '',
  jobType: JobTypeEnum.CLASS,
  cronExpression: '',
  description: '',
  requestUrl: '',
  requestMethod: 'GET',
  requestHeaders: '',
  apiBody: '',
  assemblyName: '',
  className: '',
  methodName: '',
  paramJson: '',
  shellCommand: '',
  apiTimeout: 30000,
  isActive: true,
  retryCount: 0,
  retryInterval: 0,
});

const formRef = ref<FormInstance>();

// 定义可展开行配置
const expandableConfig = {
  expandedRowRender: (record: QuartzJobResponseDto) => {
    return h('div', { style: { padding: '16px', background: '#fafafa' } }, [
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '描述:'),
        ' ',
        record.description || '-',
      ]),
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '创建时间:'),
        ' ',
        record.createTime || '-',
      ]),
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '修改时间:'),
        ' ',
        record.updateTime || '-',
      ]),
      h('div', { style: { display: 'flex', gap: '10px', marginTop: '10px' } }, [
        h(
          Button,
          {
            type: 'link',
            onClick: () => handleExecute(record),
          },
          '立即执行',
        ),
        h(
          Button,
          {
            type: 'link',
            onClick: () => handlePause(record),
          },
          '暂停',
        ),
      ]),
    ]);
  },
};

// 列配置 - 移除固定宽度设置，实现列宽自适应
const columns: ColumnsType<QuartzJobResponseDto>[] = [
  {
    title: '作业名称',
    dataIndex: 'jobName',
    ellipsis: true,
  },
  {
    title: '作业分组',
    dataIndex: 'jobGroup',
    ellipsis: true,
  },
  {
    title: '作业类型',
    dataIndex: 'jobType',
    ellipsis: true,
    customRender: ({ record }) => {
      const type = jobTypeMap[record.jobType];
      return h(
        Tag,
        { color: type?.color || 'default' },
        type?.text || record.jobType || '未知',
      );
    },
  },
  {
    title: 'cron表达式',
    dataIndex: 'cronExpression',
    ellipsis: true,
  },
  {
    title: '状态',
    dataIndex: 'status',
    ellipsis: true,
    customRender: ({ record }) => {
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
    dataIndex: 'isActive',
    ellipsis: true,
    customRender: ({ record }) =>
      h(Switch, { checked: record.isActive, disabled: true }),
  },
  {
    title: '上次执行',
    dataIndex: 'lastRunTime',
    ellipsis: true,
  },
  {
    title: '下次执行',
    dataIndex: 'nextRunTime',
    ellipsis: true,
  },
  {
    title: '操作',
    key: 'action',
    width: 200,
    slots: {
      customRender: 'action',
    },
  },
];

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
      jobType: searchForm.value.jobType,
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

// 处理分页变化
const handlePageChange = (pageObj) => {
  currentPage.value = pageObj.current;
  pageSize.value = pageObj.pageSize;
  loadJobList();
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
    jobStatus: undefined,
    jobType: undefined,
  };
  currentPage.value = 1;
  loadJobList();
};

// 打开新增对话框
const handleAdd = () => {
  editModalTitle.value = '新增作业';
  Object.assign(editForm, {
    jobName: '',
    jobGroup: '',
    jobType: JobTypeEnum.CLASS,
    cronExpression: '0 0/1 * * * ?',
    description: '',
    requestUrl: '',
    requestMethod: 'GET',
    requestHeaders: '',
    apiBody: '',
    assemblyName: '',
    className: '',
    methodName: '',
    paramJson: '',
    shellCommand: '',
    apiTimeout: 30000,
    isActive: true,
    retryCount: 0,
    retryInterval: 0,
  });
  editModalVisible.value = true;
};

// 打开编辑对话框
const handleEdit = async (job: QuartzJobResponseDto) => {
  loading.value = true;
  try {
    const response = await getJob(job.jobName, job.jobGroup);
    editModalTitle.value = '编辑作业';
    // 转换响应数据到表单格式
    const jobDetail = {
      jobName: response.result?.jobName || '',
      jobGroup: response.result?.jobGroup || '',
      jobType: response.result?.jobType || JobTypeEnum.CLASS,
      cronExpression: response.result?.cronExpression || '',
      description: response.result?.description || '',
      requestUrl: response.result?.requestUrl || '',
      requestMethod: response.result?.requestMethod || 'GET',
      requestHeaders: response.result?.requestHeaders || '',
      apiBody: response.result?.apiBody || '',
      assemblyName: response.result?.assemblyName || '',
      className: response.result?.className || '',
      methodName: response.result?.methodName || '',
      paramJson: response.result?.paramJson || '',
      shellCommand: response.result?.shellCommand || '',
      apiTimeout: response.result?.apiTimeout || 30000,
      isActive: response.result?.isActive !== false,
      retryCount: response.result?.retryCount || 0,
      retryInterval: response.result?.retryInterval || 0,
    };
    Object.assign(editForm, jobDetail);
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
    const submitData = {
      jobName: editForm.jobName,
      jobGroup: editForm.jobGroup,
      jobType: editForm.jobType,
      cronExpression: editForm.cronExpression,
      description: editForm.description,
      requestUrl: editForm.requestUrl,
      requestMethod: editForm.requestMethod,
      requestHeaders: editForm.requestHeaders,
      apiBody: editForm.apiBody,
      assemblyName: editForm.assemblyName,
      className: editForm.className,
      methodName: editForm.methodName,
      paramJson: editForm.paramJson,
      shellCommand: editForm.shellCommand,
      apiTimeout: editForm.apiTimeout,
      isActive: editForm.isActive,
      retryCount: editForm.retryCount,
      retryInterval: editForm.retryInterval,
    };

    if (
      editForm.jobName &&
      editForm.jobGroup &&
      editModalTitle.value === '编辑作业'
    ) {
      // 更新作业
      await updateJob(editForm.jobName, editForm.jobGroup, submitData);
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
const handleDelete = async (job: QuartzJobResponseDto) => {
  try {
    await deleteJob(job.jobName, job.jobGroup);
    message.success('作业删除成功');
    loadJobList();
  } catch (error) {
    message.error('作业删除失败');
    console.error('删除作业失败:', error);
  }
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

// 暂停作业
const handlePause = async (job: QuartzJobResponseDto) => {
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

// 获取当前作业类型对应的表单部分
const getJobTypeFormItems = () => {
  switch (editForm.jobType) {
    case JobTypeEnum.CLASS:
      return [
        {
          type: 'input',
          label: '程序集名称',
          name: 'assemblyName',
          placeholder: '请输入程序集名称',
          required: true,
        },
        {
          type: 'input',
          label: '类名',
          name: 'className',
          placeholder: '请输入类名（包含命名空间）',
          required: true,
        },
        {
          type: 'input',
          label: '方法名',
          name: 'methodName',
          placeholder: '请输入方法名',
          required: true,
        },
        {
          type: 'textarea',
          label: '参数JSON',
          name: 'paramJson',
          placeholder: 'JSON格式的参数',
          rows: 4,
        },
      ];
    case JobTypeEnum.HTTP:
      return [
        {
          type: 'input',
          label: '请求URL',
          name: 'requestUrl',
          placeholder: '请输入HTTP请求URL',
          required: true,
        },
        {
          type: 'select',
          label: '请求方法',
          name: 'requestMethod',
          options: [
            { label: 'GET', value: 'GET' },
            { label: 'POST', value: 'POST' },
            { label: 'PUT', value: 'PUT' },
            { label: 'DELETE', value: 'DELETE' },
          ],
        },
        {
          type: 'textarea',
          label: '请求头',
          name: 'requestHeaders',
          placeholder:
            'JSON格式的请求头，例如: {"Content-Type": "application/json"}',
          rows: 3,
        },
        {
          type: 'textarea',
          label: 'API请求体',
          name: 'apiBody',
          placeholder: '请求体内容',
          rows: 4,
        },
      ];
    case JobTypeEnum.SCRIPT:
      return [
        {
          type: 'textarea',
          label: '脚本命令',
          name: 'shellCommand',
          placeholder: '请输入要执行的脚本命令',
          rows: 4,
          required: true,
        },
      ];
    default:
      return [];
  }
};

// 生命周期
onMounted(() => {
  loadJobList();
});
</script>

<template>
  <Page>
    <Card style="margin-bottom: 20px">
      <Form
        ref="searchFormRef"
        :model="searchForm"
        layout="inline"
        :label-col="{ span: 8 }"
        :wrapper-col="{ span: 16 }"
        style="flex-wrap: wrap"
      >
        <Form.Item label="作业名称" name="jobName" style="margin-bottom: 16px">
          <Input placeholder="请输入作业名称" style="width: 180px" />
        </Form.Item>
        <Form.Item label="作业分组" name="jobGroup" style="margin-bottom: 16px">
          <Input placeholder="请输入作业分组" style="width: 180px" />
        </Form.Item>
        <Form.Item label="作业状态" name="status" style="margin-bottom: 16px">
          <Select placeholder="请选择状态" allowClear style="width: 120px">
            <Select.Option :value="JobStatusEnum.Normal">正常</Select.Option>
            <Select.Option :value="JobStatusEnum.Paused">已暂停</Select.Option>
            <Select.Option :value="JobStatusEnum.Completed"
              >已完成</Select.Option
            >
            <Select.Option :value="JobStatusEnum.Error">错误</Select.Option>
            <Select.Option :value="JobStatusEnum.Blocked">阻塞</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="作业类型" name="jobType" style="margin-bottom: 16px">
          <Select placeholder="请选择类型" allowClear style="width: 120px">
            <Select.Option :value="JobTypeEnum.DLL">DLL</Select.Option>
            <Select.Option :value="JobTypeEnum.API">API</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item style="margin-bottom: 16px">
          <Space>
            <Button type="primary" @click="handleSearch">
              <template #icon><SearchOutlined /></template>
              搜索
            </Button>
            <Button @click="handleReset"> 重置 </Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>

    <Card>
      <Form.Item>
        <Space wrap>
          <Button type="primary" @click="handleAdd">
            <template #icon><PlusOutlined /></template>
            新增作业
          </Button>
        </Space>
      </Form.Item>
      <!-- 作业列表 -->
      <Table
        :columns="columns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :rowKey="(record) => `${record.jobName}-${record.jobGroup}`"
        @change="handlePageChange"
        :expandable="expandableConfig"
        size="middle"
        width="100%"
        tableLayout="auto"
      >
        <template #action="{ record }">
          <Space>
            <Button
              type="primary"
              :disabled="loading"
              @click="handleEdit(record)"
              >编辑</Button
            >
            <Button danger :disabled="loading" @click="handleDelete(record)"
              >删除</Button
            >
            <Button
              v-if="record.status === JobStatusEnum.Normal"
              :disabled="loading"
              @click="handleStop(record)"
              >停止</Button
            >
            <Button v-else :disabled="loading" @click="handleResume(record)"
              >恢复</Button
            >
          </Space>
        </template>
      </Table>
    </Card>

    <!-- 编辑对话框 -->
    <Modal
      v-model:visible="editModalVisible"
      :title="editModalTitle"
      width="700px"
      @cancel="editModalVisible = false"
    >
      <Form
        ref="formRef"
        :model="editForm"
        layout="vertical"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 18 }"
      >
        <Form.Item
          label="作业名称"
          name="jobName"
          :rules="[{ required: true, message: '请输入作业名称' }]"
        >
          <Input placeholder="请输入作业名称" />
        </Form.Item>

        <Form.Item
          label="作业分组"
          name="jobGroup"
          :rules="[{ required: true, message: '请输入作业分组' }]"
        >
          <Input placeholder="请输入作业分组" />
        </Form.Item>

        <Form.Item
          label="作业类型"
          name="jobType"
          :rules="[{ required: true, message: '请选择作业类型' }]"
        >
          <Select v-model:value="editForm.jobType" @change="() => {}">
            <Select.Option :value="JobTypeEnum.DLL">DLL</Select.Option>
            <Select.Option :value="JobTypeEnum.API">API</Select.Option>
          </Select>
        </Form.Item>

        <Form.Item
          label="Cron表达式"
          name="cronExpression"
          :rules="[{ required: true, message: '请输入Cron表达式' }]"
        >
          <Input placeholder="例如: 0 0/1 * * * ? (每分钟执行一次)" />
        </Form.Item>

        <Form.Item label="描述" name="description">
          <Input.TextArea placeholder="请输入作业描述" :rows="3" />
        </Form.Item>

        <Form.Item
          label="API超时(ms)"
          name="apiTimeout"
          :rules="[{ required: true, message: '请输入API超时时间' }]"
        >
          <Input type="number" placeholder="请输入API超时时间，单位毫秒" />
        </Form.Item>

        <Form.Item label="重试次数" name="retryCount">
          <Input type="number" placeholder="请输入失败重试次数" />
        </Form.Item>

        <Form.Item label="重试间隔(ms)" name="retryInterval">
          <Input type="number" placeholder="请输入重试间隔时间" />
        </Form.Item>

        <Form.Item label="是否启用" name="isActive" valuePropName="checked">
          <Switch />
        </Form.Item>

        <Divider>作业配置详情</Divider>

        <template
          v-for="(item, index) in getJobTypeFormItems()"
          :key="`${item.name}-${index}`"
        >
          <Form.Item
            :label="item.label"
            :name="item.name"
            :rules="
              item.required
                ? [{ required: true, message: `请输入${item.label}` }]
                : []
            "
          >
            <template v-if="item.type === 'input'">
              <Input :placeholder="item.placeholder" />
            </template>
            <template v-else-if="item.type === 'select'">
              <Select v-model:value="editForm[item.name]" v-if="item.options">
                <template v-for="option in item.options" :key="option.value">
                  <Select.Option :value="option.value">{{
                    option.label
                  }}</Select.Option>
                </template>
              </Select>
            </template>
            <template v-else-if="item.type === 'textarea'">
              <Input.TextArea
                :placeholder="item.placeholder"
                :rows="item.rows || 3"
              />
            </template>
          </Form.Item>
        </template>
      </Form>

      <template #footer>
        <Space>
          <Button @click="editModalVisible = false">取消</Button>
          <Button type="primary" @click="handleSave">保存</Button>
        </Space>
      </template>
    </Modal>
  </Page>
</template>

<style scoped>
/* 响应式布局调整 */
@media (max-width: 768px) {
  .ant-form-inline .ant-form-item {
    margin-right: 0;
    margin-bottom: 16px;
    width: 100%;
  }

  .ant-form-inline .ant-form-item-label {
    text-align: left;
  }

  .ant-card {
    margin-bottom: 16px !important;
  }
}
</style>
