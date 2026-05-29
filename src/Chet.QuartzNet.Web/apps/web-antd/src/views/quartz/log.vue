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

// 导入i18n
import { $t } from '#/locales';

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
  [LogStatusEnum.SUCCESS]: { text: () => $t('page.quartz.logPage.statusSuccess'), status: 'success' },
  [LogStatusEnum.ERROR]: { text: () => $t('page.quartz.logPage.statusError'), status: 'error' },
  [LogStatusEnum.RUNNING]: { text: () => $t('page.quartz.logPage.statusRunning'), status: 'processing' },
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<LogResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);

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
const detailModalTitle = ref('logDetail');
const logDetail = ref<LogResponseDto | null>(null);

// 排序配置
const sortBy = ref<string>('');
const sortOrder = ref<string>('');

// 列配置（使用computed属性，当排序状态变化时自动更新）
const columns = computed<ColumnsType<LogResponseDto>[]>(() => [
  {
    title: $t('page.quartz.logPage.jobName'),
    dataIndex: 'jobName',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.logPage.jobGroup'),
    dataIndex: 'jobGroup',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.logPage.status'),
    dataIndex: 'status',
    customRender: ({ record }) => {
      const status = logStatusMap[record.status];
      return h(Tag, { color: status.status }, {
        default: () => status?.text?.() || $t('page.quartz.logPage.unknown')
      });
    },
  },
  {
    title: $t('page.quartz.logPage.startTime'),
    dataIndex: 'startTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'startTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.startTime ? formatDateTime(record.startTime) : '-';
    },
  },
  {
    title: $t('page.quartz.logPage.endTime'),
    dataIndex: 'endTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'endTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.endTime ? formatDateTime(record.endTime) : '-';
    },
  },
  {
    title: $t('page.quartz.logPage.duration'),
    dataIndex: 'duration',
    ellipsis: true,
    sorter: true,
    sortOrder:
      sortBy.value === 'duration'
        ? sortOrder.value === 'asc'
          ? 'ascend'
          : sortOrder.value === 'desc'
            ? 'descend'
            : undefined
        : undefined,
  },
  {
    title: $t('page.quartz.logPage.action'),
    width: 80,
    key: 'action',
    fixed: 'right',
    customRender: ({ record }) => {
      return h(Space, { size: 'middle' }, {
        default: () => [
          h(Button, {
            type: 'primary',
            onClick: () => handleDetail(record),
            disabled: loading.value,
          }, {
            default: () => $t('page.quartz.logPage.detail'),
          }),
        ],
      });
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
  showTotal: (total, range) => $t('page.quartz.logPage.paginationTotal', { start: range[0], end: range[1], total }),
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


    if (response.success) {
      // 根据API定义，响应数据应该包含data字段，其中包含items和totalCount，现在还包含totalPages
      if (
        response.data &&
        response.data.items &&
        Array.isArray(response.data.items)
      ) {
        dataSource.value = response.data.items;
        total.value = response.data.totalCount || 0;
      } else {
        dataSource.value = [];
        total.value = 0;
      }
    } else {
      // 处理错误情况，包括可能的errorCode
      const errorMsg = response.errorCode
        ? `${response.message || $t('page.quartz.logPage.loadListFailed')} (${$t('page.quartz.logPage.errorCode')}: ${response.errorCode})`
        : response.message || $t('page.quartz.logPage.loadListFailed');
      message.error(errorMsg);
      dataSource.value = [];
      total.value = 0;
    }
  } catch (error) {
    console.log($t('page.quartz.logPage.loadListFailed'), error);
    message.error(
      typeof error === 'object' && error !== null && 'message' in error
        ? String(error.message)
        : $t('page.quartz.logPage.loadListFailed'),
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
    sortOrder.value =
      sorter.order === 'ascend'
        ? 'asc'
        : sorter.order === 'descend'
          ? 'desc'
          : undefined;
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
          await loadLogList();
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

// 初始化
const initData = async () => {
  await loadLogList();
};

// 启动时加载数据
initData();
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <Card class="mb-4">
        <Form ref="searchFormRef" :model="searchForm" layout="horizontal" :label-align="'right'">
          <Row :gutter="16">
            <!-- 默认显示的3个搜索条件 -->
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4">
              <Form.Item :label="$t('page.quartz.logPage.jobName')" name="jobName">
                <Input v-model:value="searchForm.jobName" :placeholder="$t('page.quartz.logPage.placeholderJobName')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4">
              <Form.Item :label="$t('page.quartz.logPage.jobGroup')" name="jobGroup">
                <Input v-model:value="searchForm.jobGroup" :placeholder="$t('page.quartz.logPage.placeholderJobGroup')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4">
              <Form.Item :label="$t('page.quartz.logPage.executionStatus')" name="status">
                <Select v-model:value="searchForm.status" :placeholder="$t('page.quartz.logPage.placeholderStatus')" allowClear>
                  <Select.Option :value="LogStatusEnum.SUCCESS">{{ $t('page.quartz.logPage.statusSuccess') }}</Select.Option>
                  <Select.Option :value="LogStatusEnum.ERROR">{{ $t('page.quartz.logPage.statusError') }}</Select.Option>
                  <Select.Option :value="LogStatusEnum.RUNNING">{{ $t('page.quartz.logPage.statusRunning') }}</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4">
              <Form.Item :label="$t('page.quartz.logPage.startTime')" name="startTime">
                <DatePicker v-model:value="searchForm.startTime" showTime :placeholder="$t('page.quartz.logPage.selectStartTime')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4">
              <Form.Item :label="$t('page.quartz.logPage.endTime')" name="endTime">
                <DatePicker v-model:value="searchForm.endTime" showTime :placeholder="$t('page.quartz.logPage.selectEndTime')" />
              </Form.Item>
            </Col>
            <!-- 展开显示的搜索条件 -->
            <template v-if="isSearchExpanded">

            </template>

            <!-- 搜索按钮和展开/收起按钮 -->
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="4" class="text-right">
              <Space>
                <Button type="primary" @click="handleSearch"> {{ $t('page.quartz.logPage.search') }} </Button>
                <Button @click="handleReset"> {{ $t('page.quartz.logPage.reset') }} </Button>
              </Space>
            </Col>
          </Row>
        </Form>
      </Card>

      <Card>
        <div class="mb-4 flex items-center justify-end">
          <Space>
            <Button danger @click="handleClear">{{ $t('page.quartz.logPage.clearLogs') }}</Button>
          </Space>
        </div>
        <!-- 日志列表 -->
        <Table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading"
          :rowKey="(record) => record.logId" size="middle" @change="handleTableChange" :scroll="{ x: 'max-content' }">
        </Table>
      </Card>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="$t('page.quartz.logPage.logDetail')" width="80%" :max-width="1200" :footer="null"
        :destroyOnClose="true">
        <div v-if="logDetail" class="log-detail">
          <!-- 头部信息 -->
          <div class="detail-header mb-4 rounded-lg p-5">
            <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
              <Typography.Title :level="4" class="m-0 text-ellipsis max-w-[70%]">
                {{ logDetail.jobName }} - {{ logDetail.jobGroup }}
              </Typography.Title>
              <Tag :color="logStatusMap[logDetail.status].status" class="text-lg px-4 py-1 text-base">
                {{ logStatusMap[logDetail.status].text() }}
              </Tag>
            </div>

            <!-- 基本信息行 -->
            <div class="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.logPage.executionDuration') }}</span>
                <span class="text-sm font-medium">{{ logDetail.duration || 0 }} ms</span>
              </div>
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.logPage.startDateTime') }}</span>
                <span class="text-sm">{{ formatDateTime(logDetail.startTime) }}</span>
              </div>
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.logPage.endDateTime') }}</span>
                <span class="text-sm">{{
                  logDetail.endTime ? formatDateTime(logDetail.endTime) : '-'
                  }}</span>
              </div>
            </div>
          </div>

          <!-- 内容区域 -->
          <div class="detail-content space-y-6">
            <!-- 执行信息 -->
            <div class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.logPage.executionInfo') }}</Typography.Title>
              <div class="content-card exec-info-card rounded-lg p-4">
                <pre class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{ logDetail.message || $t('page.quartz.logPage.noExecutionInfo')
                }}</pre>
              </div>
            </div>

            <!-- 错误信息 -->
            <div v-if="logDetail.errorMessage" class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.logPage.errorInfo') }}</Typography.Title>
              <div class="content-card error-card rounded-lg p-4">
                <pre class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{ logDetail.errorMessage }}
          </pre>
              </div>
            </div>

            <!-- 异常信息 -->
            <div v-if="logDetail.exception" class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.logPage.exceptionInfo') }}</Typography.Title>
              <div class="content-card error-card rounded-lg p-4">
                <pre
                  class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{ logDetail.exception }}</pre>
              </div>
            </div>

            <!-- 执行结果 -->
            <div v-if="logDetail.result" class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.logPage.executionResult') }}</Typography.Title>
              <div class="content-card success-card rounded-lg p-4">
                <pre class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{ typeof logDetail.result ===
                  'string' ?
                  logDetail.result : JSON.stringify(logDetail.result, null, 2) }}</pre>
              </div>
            </div>

            <!-- 作业数据 -->
            <div v-if="logDetail.jobData" class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.logPage.jobData') }}</Typography.Title>
              <div class="content-card info-card rounded-lg p-4">
                <pre class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{ typeof logDetail.jobData ===
                  'string' ?
                  logDetail.jobData : JSON.stringify(logDetail.jobData, null, 2) }}</pre>
              </div>
            </div>
          </div>
        </div>

        <!-- 底部按钮 -->
        <div class="mt-6 flex justify-end">
          <Button @click="detailModalVisible = false" type="primary" size="large" class="px-6">
            {{ $t('page.quartz.logPage.close') }}
          </Button>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
/* 暗色主题兼容样式 */
.detail-header {
  background: var(--color-bg-container) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.info-item {
  background: rgba(var(--color-text-secondary-rgb), 0.05);
  border-radius: 4px;
  transition: all 0.3s ease;
}

.info-item:hover {
  background: rgba(var(--color-text-secondary-rgb), 0.1);
}

.detail-content {
  :deep(.ant-typography) {
    color: var(--color-text) !important;
  }
}

/* 内容区域样式 */
.content-section {
  margin-bottom: 1.5rem;
}

.content-card {
  background: var(--color-bg-container) !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 8px;
  transition: all 0.3s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.content-card:hover {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
}

/* 错误信息和异常区域 */
.error-card {
  background: rgba(var(--color-error-rgb), 0.1) !important;
  border: 1px solid var(--color-error-light) !important;
}

/* 错误信息和异常的代码块样式 */
.error-card :deep(.code-block) {
  color: #ff4d4f !important;
}

/* 执行结果区域 */
.success-card {
  background: rgba(var(--color-primary-rgb), 0.1) !important;
  border: 1px solid var(--color-primary-light) !important;
}

/* 作业数据区域 */
.info-card {
  background: rgba(var(--color-success-rgb), 0.1) !important;
  border: 1px solid var(--color-success-light) !important;
}

/* 执行信息区域 */
.exec-info-card {
  background: rgba(var(--color-info-rgb), 0.1) !important;
  border: 1px solid var(--color-info-light) !important;
}

/* 代码块样式 */
.code-block {
  color: var(--color-text) !important;
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
  line-height: 1.6;
  padding: 0.75rem;
  border-radius: 4px;
  background: rgba(var(--color-text-rgb), 0.03) !important;
  overflow-x: auto;
  max-height: 400px;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .detail-header {
    padding: 1rem;
  }

  .content-card {
    padding: 1rem;
  }

  .code-block {
    font-size: 0.85rem;
  }
}
</style>
