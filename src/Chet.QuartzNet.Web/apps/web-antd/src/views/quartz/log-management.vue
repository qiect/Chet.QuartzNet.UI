<script setup lang="ts">
import { ref, computed, reactive, h } from 'vue';
import { Page } from '@vben/common-ui';
import { Table, Card, Form } from 'ant-design-vue';
import type { ColumnsType, FormInstance } from 'ant-design-vue';
import {
  Button,
  Input,
  Select,
  Space,
  Modal,
  Tag,
  message,
  DatePicker,
  Typography,
  Alert,
} from 'ant-design-vue';
import {
  SearchOutlined,
  DownloadOutlined,
  DeleteOutlined,
} from '@ant-design/icons-vue';
import type { PaginationProps } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

// 导入日志相关类型和API
import {
  LogStatusEnum,
  getLogList,
  getLogDetail,
  exportLogList,
  clearLogs,
  getLogStatistics,
} from '../../api/quartz/log';
import type { LogQueryParams, LogResponseDto } from '../../api/quartz/log';
import type { ProColumns } from '@vben/common-ui';

// 日志状态映射
const logStatusMap = {
  [LogStatusEnum.SUCCESS]: { text: '成功', status: 'success' },
  [LogStatusEnum.ERROR]: { text: '失败', status: 'error' },
  [LogStatusEnum.RUNNING]: { text: '运行中', status: 'processing' },
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<LogResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(10);

// 搜索条件
// 添加表单实例引用
const searchFormRef = ref<FormInstance>();
// 根据API定义，确保searchForm与LogQueryParams接口匹配
const searchForm = reactive<LogQueryParams>({
  jobId: '',
  jobName: '',
  jobGroup: '',
  status: undefined,
  startTime: undefined,
  endTime: undefined,
});

// 详情对话框
const detailModalVisible = ref(false);
const detailModalTitle = ref('日志详情');
const logDetail = ref<LogResponseDto | null>(null);

// 列配置
const columns: ColumnsType<LogResponseDto>[] = [
  {
    title: '作业ID',
    dataIndex: 'logId',
    width: 150,
    ellipsis: true,
  },
  {
    title: '作业名称',
    dataIndex: 'jobName',
    width: 180,
    ellipsis: true,
  },
  {
    title: '作业分组',
    dataIndex: 'jobGroup',
    width: 150,
    ellipsis: true,
  },
  {
    title: '状态',
    dataIndex: 'status',
    width: 100,
    customRender: ({ record }) => {
      const status = logStatusMap[record.status];
      return h(Tag, { color: status.status }, status.text);
    },
  },
  {
    title: '执行开始时间',
    dataIndex: 'startTime',
    width: 180,
    ellipsis: true,
  },
  {
    title: '执行结束时间',
    dataIndex: 'endTime',
    width: 180,
    ellipsis: true,
  },
  {
    title: '执行时长(ms)',
    dataIndex: 'duration',
    width: 120,
    ellipsis: true,
  },
  {
    title: '操作',
    key: 'action',
    width: 120,
    // 不使用render函数，改用更简单的配置
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

// 加载日志列表
const loadLogList = async () => {
  loading.value = true;
  try {
    const response = await getLogList({
      ...searchForm,
      pageIndex: currentPage.value || 1,
      pageSize: pageSize.value || 10,
    });

    console.log('API响应:', response);
    
    if (response.success) {
      // 根据API定义，响应数据应该包含data字段，其中包含items和totalCount，现在还包含totalPages
      if (response.data && response.data.items && Array.isArray(response.data.items)) {
        dataSource.value = response.data.items;
        total.value = response.data.totalCount || 0;
        // 可以使用totalPages做额外处理
        console.log(`成功加载日志列表: ${dataSource.value.length} 条记录，共 ${total.value} 条，总页数：${response.data.totalPages}`);
      } else {
        console.warn('API响应数据格式不符合预期', response.data);
        dataSource.value = [];
        total.value = 0;
      }
    } else {
      // 处理错误情况，包括可能的errorCode
      const errorMsg = response.errorCode ? 
        `${response.message || '获取日志列表失败'} (错误码: ${response.errorCode})` : 
        response.message || '获取日志列表失败';
      message.error(errorMsg);
      dataSource.value = [];
      total.value = 0;
    }
  } catch (error) {
    console.log('获取日志列表时发生错误:', error);
    message.error(typeof error === 'object' && error !== null && 'message' in error 
      ? String(error.message) 
      : '获取日志列表失败');
    dataSource.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
};

// 加载统计信息
const loadLogStatistics = async () => {
  try {
    // 根据API定义，getLogStatistics只接受开始时间和结束时间参数
    const stats = await getLogStatistics({
      startTime: searchForm.startTime,
      endTime: searchForm.endTime
    });
    statistics.value = stats;
  } catch (error) {
    console.log('获取日志统计信息失败:', error);
  }
};

// 处理分页变化
const handlePageChange = (page: number, pageSizeVal: number) => {
  currentPage.value = page;
  pageSize.value = pageSizeVal;
  loadLogList();
};

// 处理搜索
const handleSearch = () => {
  currentPage.value = 1;
  loadLogList();
  loadLogStatistics();
};

// 处理重置
const handleReset = () => {
  searchForm.jobId = '';
  searchForm.jobName = '';
  searchForm.jobGroup = '';
  searchForm.status = undefined;
  searchForm.startTime = undefined;
  searchForm.endTime = undefined;
  currentPage.value = 1;
  loadLogList();
  loadLogStatistics();
};

// 导出日志 - 暂时禁用
const handleExport = async () => {
  try {
    await exportLogList({...searchForm});
  } catch (error: any) {
    message.error(error.message || '导出功能暂未实现');
  }
};

// 清空日志 - 暂时禁用
const handleClear = () => {
  Modal.confirm({
    title: '确认清空',
    content: '确定要清空日志吗？此操作不可恢复！',
    onOk: async () => {
      try {
        await clearLogs(searchForm);
      } catch (error: any) {
        message.error(error.message || '清空功能暂未实现');
      }
    },
  });
};

// 查看详情
const handleDetail = async (log: LogResponseDto) => {
  try {
    const detail = await getLogDetail(log.logId);
    logDetail.value = detail;
    detailModalVisible.value = true;
  } catch (error) {
    message.error('获取日志详情失败');
    console.log('获取日志详情失败:', error);
  }
};

// 统计信息（模拟数据）
const statistics = ref({
  totalLogs: 0,
  successCount: 0,
  failureCount: 0,
  runningCount: 0,
  cancelledCount: 0,
});

// 初始化
const initData = async () => {
  await loadLogList();
  // 初始化统计信息
  statistics.value = {
    totalLogs: 0,
    successCount: 0,
    failureCount: 0,
    runningCount: 0,
    cancelledCount: 0,
  };
};

// 启动时加载数据
initData();
</script>

<template>
  <Page>
    <Card style="margin-bottom: 20px;">
      <Alert 
        type="info" 
        style="margin-bottom: 16px;"
      >
        <template #message>
          日志统计信息: 共 {{ statistics.totalLogs }} 条，成功 {{ statistics.successCount }} 条，失败 {{ statistics.failureCount }} 条，运行中 {{ statistics.runningCount }} 条
        </template>
      </Alert>
      
      <Form
        ref="searchFormRef"
        :model="searchForm"
        layout="inline"
        :label-col="{ span: 8 }"
        :wrapper-col="{ span: 16 }"
        style="flex-wrap: wrap;"
      >
        <Form.Item label="作业ID" name="jobId" style="margin-bottom: 16px;">
          <Input placeholder="请输入作业ID" style="width: 180px;" />
        </Form.Item>
        <Form.Item label="作业名称" name="jobName" style="margin-bottom: 16px;">
          <Input placeholder="请输入作业名称" style="width: 180px;" />
        </Form.Item>
        <Form.Item label="作业分组" name="jobGroup" style="margin-bottom: 16px;">
          <Input placeholder="请输入作业分组" style="width: 180px;" />
        </Form.Item>
        <Form.Item label="执行状态" name="status" style="margin-bottom: 16px;">
          <Select placeholder="请选择状态" allowClear style="width: 120px;">
            <Select.Option :value="LogStatusEnum.SUCCESS">成功</Select.Option>
            <Select.Option :value="LogStatusEnum.ERROR">失败</Select.Option>
            <Select.Option :value="LogStatusEnum.RUNNING">运行中</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="开始时间" name="startTime" style="margin-bottom: 16px;">
          <DatePicker
            showTime
            placeholder="选择开始时间"
            style="width: 200px;"
          />
        </Form.Item>
        <Form.Item label="结束时间" name="endTime" style="margin-bottom: 16px;">
          <DatePicker
            showTime
            placeholder="选择结束时间"
            style="width: 200px;"
          />
        </Form.Item>
        <Form.Item style="margin-bottom: 16px;">
          <Space>
            <Button type="primary" @click="handleSearch">
              <template #icon><SearchOutlined /></template>
              搜索
            </Button>
            <Button @click="handleReset">重置</Button>
          </Space>
        </Form.Item>
        <Form.Item style="margin-bottom: 16px;">
          <Space>
            <Button type="primary" disabled icon="download" @click="handleExport">
              导出日志（暂未实现）
            </Button>
            <Button danger disabled icon="delete" @click="handleClear">清空日志（暂未实现）</Button>
          </Space>
        </Form.Item>
      </Form>
    </Card>
    
    <Card>
      <!-- 日志列表 -->
      <Table
        :columns="columns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :rowKey="(record) => record.logId"
        :size="small"
        @change="handlePageChange"
        style="width: 100%;"
      >
        <!-- 添加action插槽来渲染详情按钮 -->
        <template #action="{ record }">
          <Space size="middle">
            <Button 
              type="link" 
              @click="handleDetail(record)"
              :disabled="loading"
            >
              详情
            </Button>
          </Space>
        </template>
      </Table>
    </Card>

    <!-- 详情对话框 -->
    <Modal
      v-model:open="detailModalVisible"
      :title="detailModalTitle"
      width="900px"
      :okButtonProps="{ style: { display: 'none' } }"
      :cancelButtonProps="{ style: { display: 'none' } }"
      :footer="[]"
    >
      <div v-if="logDetail" class="log-detail">
        <div class="detail-header">
          <Typography.Title :level="4">
            {{ logDetail.jobName }} - {{ logDetail.jobGroup }}
          </Typography.Title>
          <div style="display: flex; gap: 15px; margin-bottom: 20px">
            <div>
              <strong>执行状态:</strong>
              <Tag :color="logStatusMap[logDetail.status].status">
                {{ logStatusMap[logDetail.status].text }}
              </Tag>
            </div>
            <div><strong>执行时长:</strong> {{ logDetail.duration || 0 }} ms</div>
            <div><strong>执行时间:</strong> {{ logDetail.startTime }}</div>
          </div>
        </div>

        <div class="detail-content">
          <Typography.Title :level="5">执行信息</Typography.Title>
          <pre
            style="
              background: #f5f5f5;
              padding: 10px;
              border-radius: 4px;
              white-space: pre-wrap;
              word-break: break-word;
            "
          >
            {{ logDetail.executionInfo || logDetail.message || '暂无执行信息' }}
          </pre>

          <div v-if="logDetail.errorMessage || logDetail.exception">
            <Typography.Title :level="5">错误信息</Typography.Title>
            <pre
              style="
                background: #fff1f0;
                padding: 10px;
                border-radius: 4px;
                color: #f5222d;
                white-space: pre-wrap;
                word-break: break-word;
              "
            >
              {{ logDetail.errorMessage || logDetail.exception }}
            </pre>
          </div>
          
          <!-- 显示新增的result字段 -->
          <div v-if="logDetail.result">
            <Typography.Title :level="5">执行结果</Typography.Title>
            <pre
              style="
                background: #f0f9ff;
                padding: 10px;
                border-radius: 4px;
                white-space: pre-wrap;
                word-break: break-word;
              "
            >
              {{ typeof logDetail.result === 'string' ? logDetail.result : JSON.stringify(logDetail.result, null, 2) }}
            </pre>
          </div>
          
          <!-- 显示新增的jobData字段 -->
          <div v-if="logDetail.jobData">
            <Typography.Title :level="5">作业数据</Typography.Title>
            <pre
              style="
                background: #f6ffed;
                padding: 10px;
                border-radius: 4px;
                white-space: pre-wrap;
                word-break: break-word;
              "
            >
              {{ typeof logDetail.jobData === 'string' ? logDetail.jobData : JSON.stringify(logDetail.jobData, null, 2) }}
            </pre>
          </div>
        </div>
      </div>
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