<script setup lang="ts">
import { ref, computed, reactive, h } from 'vue';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import { Page } from '@vben/common-ui';
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
  Table,
  Card,
  Form,
  Row,
  Col,
} from 'ant-design-vue';
import type {
  ColumnsType,
  FormInstance,
  PaginationProps,
} from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

// 导入日志相关类型和API
import {
  LogStatusEnum,
  getLogList,
  getLogDetail,
  clearLogs,
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
  jobName: '',
  jobGroup: '',
  status: undefined,
  startTime: undefined,
  endTime: undefined,
});

// 搜索栏展开状态
const isSearchExpanded = ref(false);

// 详情对话框
const detailModalVisible = ref(false);
const detailModalTitle = ref('日志详情');
const logDetail = ref<LogResponseDto | null>(null);

// 排序配置
const sortBy = ref<string>('');
const sortOrder = ref<string>('');

// 列配置（使用computed属性，当排序状态变化时自动更新）
const columns = computed<ColumnsType<LogResponseDto>[]>(() => [
  {
    title: '作业ID',
    dataIndex: 'logId',
    ellipsis: true,
  },
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
    title: '状态',
    dataIndex: 'status',
    customRender: ({ record }) => {
      const status = logStatusMap[record.status];
      return h(Tag, { color: status.status }, status.text);
    },
  },
  {
    title: '开始时间',
    dataIndex: 'startTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'startTime' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.startTime ? formatDateTime(record.startTime) : '-';
    },
  },
  {
    title: '结束时间',
    dataIndex: 'endTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'endTime' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.endTime ? formatDateTime(record.endTime) : '-';
    },
  },
  {
    title: '执行时长(ms)',
    dataIndex: 'duration',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'duration' ? (sortOrder.value === 'asc' ? 'ascend' : sortOrder.value === 'desc' ? 'descend' : undefined) : undefined,
  },
  {
    title: '操作',
    key: 'action',
    slots: {
      customRender: 'action',
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

// 加载日志列表
const loadLogList = async () => {
  loading.value = true;
  try {
    const response = await getLogList({
      ...searchForm,
      pageIndex: currentPage.value || 1,
      pageSize: pageSize.value || 10,
      sortBy: sortBy.value,
      sortOrder: sortOrder.value,
    });

    console.log('API响应:', response);

    if (response.success) {
      // 根据API定义，响应数据应该包含data字段，其中包含items和totalCount，现在还包含totalPages
      if (
        response.data &&
        response.data.items &&
        Array.isArray(response.data.items)
      ) {
        dataSource.value = response.data.items;
        total.value = response.data.totalCount || 0;
        // 可以使用totalPages做额外处理
        console.log(
          `成功加载日志列表: ${dataSource.value.length} 条记录，共 ${total.value} 条，总页数：${response.data.totalPages}`,
        );
      } else {
        console.warn('API响应数据格式不符合预期', response.data);
        dataSource.value = [];
        total.value = 0;
      }
    } else {
      // 处理错误情况，包括可能的errorCode
      const errorMsg = response.errorCode
        ? `${response.message || '获取日志列表失败'} (错误码: ${response.errorCode})`
        : response.message || '获取日志列表失败';
      message.error(errorMsg);
      dataSource.value = [];
      total.value = 0;
    }
  } catch (error) {
    console.log('获取日志列表时发生错误:', error);
    message.error(
      typeof error === 'object' && error !== null && 'message' in error
        ? String(error.message)
        : '获取日志列表失败',
    );
    dataSource.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
};



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
  loadLogList();
};

// 处理搜索
const handleSearch = async () => {
  if (searchFormRef.value) {
    // 触发表单验证（如果需要）
    await searchFormRef.value.validateFields();
  }
  currentPage.value = 1;
  loadLogList();
};

// 处理重置
const handleReset = () => {
  // 使用表单的重置方法
  if (searchFormRef.value) {
    searchFormRef.value.resetFields();
  }
  currentPage.value = 1;
  loadLogList();
};



// 清空日志
const handleClear = () => {
  Modal.confirm({
    title: '确认清空',
    content: '确定要清空日志吗？此操作不可恢复！',
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
          message.success('日志清空成功');
          // 清空后重新加载日志列表
          await loadLogList();
        } else {
          message.error(response.message || '日志清空失败');
        }
      } catch (error: any) {
        console.error('清空日志失败:', error);
        message.error(error.message || '日志清空失败');
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
    message.error('显示详情失败');
    console.log('显示详情失败:', error);
  }
};

// 初始化
const initData = async () => {
  await loadLogList();
};

// 启动时加载数据
initData();
</script>

<template>
  <Page>
    <Card class="mb-4">

      <Form ref="searchFormRef" :model="searchForm" layout="horizontal" :label-col="{ span: 6 }"
        :wrapper-col="{ span: 18 }" :label-align="'right'">
        <Row :gutter="16">
          <!-- 默认显示的3个搜索条件 -->
          <Col :xs="24" :sm="12" :md="8" :lg="8">
          <Form.Item label="作业名称" name="jobName">
            <Input v-model:value="searchForm.jobName" placeholder="请输入作业名称" />
          </Form.Item>
          </Col>
          <Col :xs="24" :sm="12" :md="8" :lg="8">
          <Form.Item label="作业分组" name="jobGroup">
            <Input v-model:value="searchForm.jobGroup" placeholder="请输入作业分组" />
          </Form.Item>
          </Col>
          <Col :xs="24" :sm="12" :md="8" :lg="8">
          <Form.Item label="执行状态" name="status">
            <Select v-model:value="searchForm.status" placeholder="请选择状态" allowClear>
              <Select.Option :value="LogStatusEnum.SUCCESS">成功</Select.Option>
              <Select.Option :value="LogStatusEnum.ERROR">失败</Select.Option>
              <Select.Option :value="LogStatusEnum.RUNNING">运行中</Select.Option>
            </Select>
          </Form.Item>
          </Col>
          
          <!-- 展开显示的搜索条件 -->
          <template v-if="isSearchExpanded">
            <Col :xs="24" :sm="12" :md="8" :lg="8">
            <Form.Item label="开始时间" name="startTime">
              <DatePicker v-model:value="searchForm.startTime" showTime placeholder="选择开始时间" />
            </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="8">
            <Form.Item label="结束时间" name="endTime">
              <DatePicker v-model:value="searchForm.endTime" showTime placeholder="选择结束时间" />
            </Form.Item>
            </Col>
          </template>
          
          <!-- 搜索按钮和展开/收起按钮 -->
          <Col :xs="24" :sm="24" :md="24" :lg="24" class="text-right">
          <Space>
            <Button type="primary" @click="handleSearch"> 搜索 </Button>
            <Button @click="handleReset"> 重置 </Button>
            <Button type="link" @click="isSearchExpanded = !isSearchExpanded">
              {{ isSearchExpanded ? '收起' : '展开' }}
            </Button>
          </Space>
          </Col>
        </Row>
      </Form>
    </Card>

    <Card>
      <div class="mb-4 flex items-center justify-end">
        <Space>
          <Button danger @click="handleClear">清空日志</Button>
        </Space>
      </div>
      <!-- 日志列表 -->
      <Table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading"
        :rowKey="(record) => record.logId" size="middle" @change="handleTableChange" :scroll="{ x: 'max-content' }")>}">
        <template #action="{ record }">
          <Space size="middle">
            <Button type="primary" @click="handleDetail(record)" :disabled="loading">
              详情
            </Button>
          </Space>
        </template>
      </Table>
    </Card>

    <!-- 详情对话框 -->
    <Modal v-model:open="detailModalVisible" :title="detailModalTitle" width="1000px"
      :footer="null" :destroyOnClose="true">
      <div v-if="logDetail" class="log-detail">
        <!-- 头部信息 -->
        <div class="detail-header mb-4 p-4 bg-gray-50 rounded-lg">
          <div class="flex justify-between items-center mb-3">
            <Typography.Title :level="4" class="m-0">
              {{ logDetail.jobName }} - {{ logDetail.jobGroup }}
            </Typography.Title>
            <Tag :color="logStatusMap[logDetail.status].status" class="text-lg">
              {{ logStatusMap[logDetail.status].text }}
            </Tag>
          </div>
          
          <!-- 基本信息行 -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mt-2">
            <div class="flex items-center">
              <span class="font-bold mr-2">执行时长:</span>
              <span>{{ logDetail.duration || 0 }} ms</span>
            </div>
            <div class="flex items-center">
              <span class="font-bold mr-2">开始时间:</span>
              <span>{{ formatDateTime(logDetail.startTime) }}</span>
            </div>
            <div class="flex items-center">
              <span class="font-bold mr-2">结束时间:</span>
              <span>{{ logDetail.endTime ? formatDateTime(logDetail.endTime) : '-' }}</span>
            </div>
          </div>
        </div>

        <!-- 内容区域 -->
        <div class="detail-content">
          <!-- 执行信息 -->
          <div class="mb-6">
            <Typography.Title :level="5" class="mb-2">执行信息</Typography.Title>
            <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
              <pre class="m-0 whitespace-pre-wrap word-break-break-word text-sm">
                {{ logDetail.executionInfo || logDetail.message || '暂无执行信息' }}
              </pre>
            </div>
          </div>

          <!-- 错误信息 -->
          <div v-if="logDetail.errorMessage || logDetail.exception" class="mb-6">
            <Typography.Title :level="5" class="mb-2">错误信息</Typography.Title>
            <div class="bg-red-50 p-4 rounded-lg border border-red-200">
              <pre class="m-0 whitespace-pre-wrap word-break-break-word text-sm text-red-800">
                {{ logDetail.errorMessage || logDetail.exception }}
              </pre>
            </div>
          </div>

          <!-- 执行结果 -->
          <div v-if="logDetail.result" class="mb-6">
            <Typography.Title :level="5" class="mb-2">执行结果</Typography.Title>
            <div class="bg-blue-50 p-4 rounded-lg border border-blue-200">
              <pre class="m-0 whitespace-pre-wrap word-break-break-word text-sm">
                {{ 
                  typeof logDetail.result === 'string'
                    ? logDetail.result
                    : JSON.stringify(logDetail.result, null, 2)
                }}
              </pre>
            </div>
          </div>

          <!-- 作业数据 -->
          <div v-if="logDetail.jobData" class="mb-6">
            <Typography.Title :level="5" class="mb-2">作业数据</Typography.Title>
            <div class="bg-green-50 p-4 rounded-lg border border-green-200">
              <pre class="m-0 whitespace-pre-wrap word-break-break-word text-sm">
                {{ 
                  typeof logDetail.jobData === 'string'
                    ? logDetail.jobData
                    : JSON.stringify(logDetail.jobData, null, 2)
                }}
              </pre>
            </div>
          </div>
        </div>
      </div>
      
      <!-- 底部按钮 -->
      <div class="flex justify-end mt-4">
        <Button @click="detailModalVisible = false" type="primary">关闭</Button>
      </div>
    </Modal>
  </Page>
</template>
