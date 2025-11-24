<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Input, Select, Space, Modal, Form, Switch, message, notification, Divider, Tag, Table, Card, Alert } from 'ant-design-vue';
import { SearchOutlined, EditOutlined, DeleteOutlined, PlusOutlined, StopOutlined, PlayCircleOutlined } from '@ant-design/icons-vue';
import type { ColumnsType, FormInstance, PaginationProps } from 'ant-design-vue';

// 导入作业API服务
import { 
  JobTypeEnum, 
  JobStatusEnum, 
  getJobList, 
  getJobDetail, 
  createJob, 
  updateJob, 
  deleteJob, 
  triggerJob, 
  pauseJob, 
  resumeJob 
} from '../../api/quartz/job';
import type { JobRequestDto, JobResponseDto } from '../../api/quartz/job';

// 作业类型和状态映射
const jobTypeMap = {
  [JobTypeEnum.CLASS]: { text: 'CLASS', color: 'blue' },
  [JobTypeEnum.HTTP]: { text: 'HTTP', color: 'green' },
  [JobTypeEnum.SCRIPT]: { text: 'SCRIPT', color: 'orange' }
};

const jobStatusMap = {
  [JobStatusEnum.STOPPED]: { text: '已停止', status: 'error' },
  [JobStatusEnum.RUNNING]: { text: '运行中', status: 'processing' }
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<JobResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(10);

// 搜索条件
// 添加表单实例引用
const searchFormRef = ref<FormInstance>();
const searchForm = ref({
  jobName: '',
  jobGroup: '',
  jobStatus: undefined,
  jobType: undefined
});

// 编辑对话框
const editModalVisible = ref(false);
const editModalTitle = ref('新增作业');
const editForm = reactive<JobRequestDto>({
  jobName: '',
  jobGroup: '',
  jobType: JobTypeEnum.CLASS,
  cronExpression: '',
  description: '',
  requestUrl: '',
  requestMethod: 'GET',
  requestHeaders: '',
  requestBody: '',
  assemblyName: '',
  className: '',
  methodName: '',
  paramJson: '',
  shellCommand: '',
  timeout: 30000,
  isActive: true,
  retryCount: 0,
  retryInterval: 0
});

const formRef = ref<FormInstance>();

// 定义可展开行配置
const expandableConfig = {
  expandedRowRender: (record: JobResponseDto) => {
    return h('div', { style: { padding: '16px', background: '#fafafa' } }, [
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '描述:'),
        ' ',
        record.description || '-'
      ]),
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '创建时间:'),
        ' ',
        record.createTime || '-'
      ]),
      h('div', { style: { marginBottom: '8px' } }, [
        h('strong', null, '修改时间:'),
        ' ',
        record.updateTime || '-'
      ]),
      h('div', { style: { display: 'flex', gap: '10px', marginTop: '10px' } }, [
        h(Button, {
          type: 'link',
          onClick: () => handleExecute(record)
        }, '立即执行'),
        h(Button, {
          type: 'link',
          onClick: () => handlePause(record)
        }, '暂停')
      ])
    ]);
  }
};

// 列配置
const columns: ColumnsType<JobResponseDto>[] = [
  {
    title: '作业名称',
    dataIndex: 'jobName',
    width: 180,
    ellipsis: true
  },
  {
    title: '作业分组',
    dataIndex: 'jobGroup',
    width: 150,
    ellipsis: true
  },
  {
    title: '作业类型',
    dataIndex: 'jobType',
    width: 100,
    customRender: ({ record }) => {
      const type = jobTypeMap[record.jobType];
      return h(Tag, { color: type.color }, type.text);
    }
  },
  {
    title: 'cron表达式',
    dataIndex: 'cronExpression',
    width: 200,
    ellipsis: true
  },
  {
    title: '状态',
    dataIndex: 'jobStatus',
    width: 100,
    customRender: ({ record }) => {
      const status = jobStatusMap[record.jobStatus];
      return h(Tag, { color: status.status }, status.text);
    }
  },
  {
    title: '是否启用',
    dataIndex: 'isActive',
    width: 100,
    customRender: ({ record }) => h(Switch, { checked: record.isActive, disabled: true })
  },
  {
    title: '上次执行',
    dataIndex: 'lastExecutionTime',
    width: 180,
    ellipsis: true
  },
  {
    title: '下次执行',
    dataIndex: 'nextExecutionTime',
    width: 180,
    ellipsis: true
  },
  {
    title: '操作',
    valueType: 'option',
    width: 200,
    render: ({ record }) => {
      const buttons = [
        h(Button, {
          type: 'link',
          icon: h(EditOutlined),
          onClick: () => handleEdit(record),
          disabled: loading.value
        }, '编辑'),
        h(Button, {
          type: 'link',
          danger: true,
          icon: h(DeleteOutlined),
          onClick: () => handleDelete(record),
          disabled: loading.value
        }, '删除')
      ];
      
      // 根据状态添加不同按钮，避免条件渲染导致的问题
      if (record.jobStatus === JobStatusEnum.RUNNING) {
        buttons.push(h(Button, {
          type: 'link',
          icon: h(StopOutlined),
          onClick: () => handleStop(record),
          disabled: loading.value
        }, '停止'));
      } else {
        buttons.push(h(Button, {
          type: 'link',
          icon: h(PlayCircleOutlined),
          onClick: () => handleResume(record),
          disabled: loading.value
        }, '恢复'));
      }
      
      return h(Space, { size: 'middle' }, buttons);
    }
  }
];

// 分页配置
const pagination = computed<PaginationProps>(() => ({
  current: currentPage.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true,
  showQuickJumper: true,
  showTotal: (total, range) => `${range[0]}-${range[1]} 共 ${total} 条`,
  pageSizeOptions: ['10', '20', '50', '100']
}));

// 加载作业列表
const loadJobList = async () => {
  loading.value = true;
  try {
    const response = await getJobList({
      pageNum: currentPage.value,
      pageSize: pageSize.value,
      jobName: searchForm.value.jobName,
      jobGroup: searchForm.value.jobGroup,
      jobStatus: searchForm.value.jobStatus,
      jobType: searchForm.value.jobType
    });
    
    dataSource.value = response.data || [];
    total.value = response.total || 0;
  } catch (error) {
    message.error('获取作业列表失败');
    console.error('获取作业列表失败:', error);
  } finally {
    loading.value = false;
  }
};

// 处理分页变化
const handlePageChange = (page: number, pageSizeVal: number) => {
  currentPage.value = page;
  pageSize.value = pageSizeVal;
  loadJobList();
};

// 处理搜索
const handleSearch = () => {
  currentPage.value = 1;
  loadJobList();
};

// 处理重置
const handleReset = () => {
  searchForm.value = {
    jobName: '',
    jobGroup: '',
    jobStatus: undefined,
    jobType: undefined
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
    requestBody: '',
    assemblyName: '',
    className: '',
    methodName: '',
    paramJson: '',
    shellCommand: '',
    timeout: 30000,
    isActive: true,
    retryCount: 0,
    retryInterval: 0
  });
  editModalVisible.value = true;
};

// 打开编辑对话框
const handleEdit = async (job: JobResponseDto) => {
  loading.value = true;
  try {
    const jobDetail = await getJobDetail(job.jobName, job.jobGroup);
    editModalTitle.value = '编辑作业';
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
    
    if (editForm.jobName && editForm.jobGroup && editModalTitle.value === '编辑作业') {
      // 更新作业
      await updateJob(editForm.jobName, editForm.jobGroup, editForm);
      message.success('作业更新成功');
    } else {
      // 新增作业
      await createJob(editForm);
      message.success('作业创建成功');
    }
    
    editModalVisible.value = false;
    loadJobList();
  } catch (error: any) {
    if (error.errorFields) {
      return; // 表单验证错误已显示
    }
    message.error(editModalTitle.value === '编辑作业' ? '作业更新失败' : '作业创建失败');
    console.error('保存作业失败:', error);
  } finally {
    loading.value = false;
  }
};

// 删除作业
const handleDelete = async (job: JobResponseDto) => {
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
const handleStop = async (job: JobResponseDto) => {
  try {
    await pauseJob(job.jobName, job.jobGroup);
    message.success('作业停止成功');
    loadJobList();
  } catch (error) {
    message.error('作业停止失败');
    console.error('停止作业失败:', error);
  }
};

// 暂停作业
const handlePause = async (job: JobResponseDto) => {
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
const handleResume = async (job: JobResponseDto) => {
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
const handleExecute = async (job: JobResponseDto) => {
  try {
    await triggerJob(job.jobName, job.jobGroup);
    message.success('作业立即执行成功');
    notification.success({
      message: '作业执行通知',
      description: `作业 ${job.jobName} 已开始执行，请稍后在日志中查看执行结果`
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
          required: true
        },
        {
          type: 'input',
          label: '类名',
          name: 'className',
          placeholder: '请输入类名（包含命名空间）',
          required: true
        },
        {
          type: 'input',
          label: '方法名',
          name: 'methodName',
          placeholder: '请输入方法名',
          required: true
        },
        {
          type: 'textarea',
          label: '参数JSON',
          name: 'paramJson',
          placeholder: 'JSON格式的参数',
          rows: 4
        }
      ];
    case JobTypeEnum.HTTP:
      return [
        {
          type: 'input',
          label: '请求URL',
          name: 'requestUrl',
          placeholder: '请输入HTTP请求URL',
          required: true
        },
        {
          type: 'select',
          label: '请求方法',
          name: 'requestMethod',
          options: [
            { label: 'GET', value: 'GET' },
            { label: 'POST', value: 'POST' },
            { label: 'PUT', value: 'PUT' },
            { label: 'DELETE', value: 'DELETE' }
          ]
        },
        {
          type: 'textarea',
          label: '请求头',
          name: 'requestHeaders',
          placeholder: 'JSON格式的请求头，例如: {"Content-Type": "application/json"}',
          rows: 3
        },
        {
          type: 'textarea',
          label: '请求体',
          name: 'requestBody',
          placeholder: '请求体内容',
          rows: 4
        }
      ];
    case JobTypeEnum.SCRIPT:
      return [
        {
          type: 'textarea',
          label: '脚本命令',
          name: 'shellCommand',
          placeholder: '请输入要执行的脚本命令',
          rows: 4,
          required: true
        }
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
<Card style="margin-bottom: 20px;">
  <Form
    ref="searchFormRef"
    :model="searchForm"
    layout="inline"
    :label-col="{ span: 8 }"
    :wrapper-col="{ span: 16 }"
    style="flex-wrap: wrap;"
  >
    <Form.Item label="作业名称" name="jobName" style="margin-bottom: 16px;">
      <Input placeholder="请输入作业名称" style="width: 180px;" />
    </Form.Item>
    <Form.Item label="作业分组" name="jobGroup" style="margin-bottom: 16px;">
      <Input placeholder="请输入作业分组" style="width: 180px;" />
    </Form.Item>
    <Form.Item label="作业状态" name="jobStatus" style="margin-bottom: 16px;">
      <Select placeholder="请选择状态" allowClear style="width: 120px;">
        <Select.Option :value="JobStatusEnum.STOPPED">已停止</Select.Option>
        <Select.Option :value="JobStatusEnum.RUNNING">运行中</Select.Option>
      </Select>
    </Form.Item>
    <Form.Item label="作业类型" name="jobType" style="margin-bottom: 16px;">
      <Select placeholder="请选择类型" allowClear style="width: 120px;">
        <Select.Option :value="JobTypeEnum.CLASS">CLASS</Select.Option>
        <Select.Option :value="JobTypeEnum.HTTP">HTTP</Select.Option>
        <Select.Option :value="JobTypeEnum.SCRIPT">SCRIPT</Select.Option>
      </Select>
    </Form.Item>
    <Form.Item style="margin-bottom: 16px;">
      <Space>
        <Button type="primary" @click="handleSearch">
          <template #icon><SearchOutlined /></template>
          搜索
        </Button>
        <Button @click="handleReset">
          重置
        </Button>
        <Button type="primary" @click="handleAdd">
          <template #icon><PlusOutlined /></template>
          新增作业
        </Button>
      </Space>
    </Form.Item>
  </Form>
</Card>

<Card>
  <!-- 作业列表 -->
  <Table
    :columns="columns"
    :data-source="dataSource"
    :pagination="pagination"
    :loading="loading"
    :rowKey="(record) => `${record.jobName}-${record.jobGroup}`"
    @change="handlePageChange"
    :expandable="expandableConfig"
    style="width: 100%;"
  />
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
        <Select.Option :value="JobTypeEnum.CLASS">CLASS</Select.Option>
        <Select.Option :value="JobTypeEnum.HTTP">HTTP</Select.Option>
        <Select.Option :value="JobTypeEnum.SCRIPT">SCRIPT</Select.Option>
      </Select>
    </Form.Item>
    
    <Form.Item
      label="Cron表达式"
      name="cronExpression"
      :rules="[{ required: true, message: '请输入Cron表达式' }]"
    >
      <Input placeholder="例如: 0 0/1 * * * ? (每分钟执行一次)" />
    </Form.Item>
    
    <Form.Item
      label="描述"
      name="description"
    >
      <Input.TextArea placeholder="请输入作业描述" :rows="3" />
    </Form.Item>
    
    <Form.Item
      label="超时时间(ms)"
      name="timeout"
      :rules="[{ required: true, message: '请输入超时时间' }]"
    >
      <Input type="number" placeholder="请输入超时时间，单位毫秒" />
    </Form.Item>
    
    <Form.Item
      label="重试次数"
      name="retryCount"
    >
      <Input type="number" placeholder="请输入失败重试次数" />
    </Form.Item>
    
    <Form.Item
      label="重试间隔(ms)"
      name="retryInterval"
    >
      <Input type="number" placeholder="请输入重试间隔时间" />
    </Form.Item>
    
    <Form.Item
      label="是否启用"
      name="isActive"
      valuePropName="checked"
    >
      <Switch />
    </Form.Item>
    
    <Divider>作业配置详情</Divider>
    
    <template v-for="(item, index) in getJobTypeFormItems()" :key="`${item.name}-${index}`">
      <Form.Item
        :label="item.label"
        :name="item.name"
        :rules="item.required ? [{ required: true, message: `请输入${item.label}` }] : []"
      >
        <template v-if="item.type === 'input'">
          <Input :placeholder="item.placeholder" />
        </template>
        <template v-else-if="item.type === 'select'">
          <Select v-model:value="editForm[item.name]" v-if="item.options">
            <template v-for="option in item.options" :key="option.value">
              <Select.Option :value="option.value">{{ option.label }}</Select.Option>
            </template>
          </Select>
        </template>
        <template v-else-if="item.type === 'textarea'">
          <Input.TextArea :placeholder="item.placeholder" :rows="item.rows || 3" />
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