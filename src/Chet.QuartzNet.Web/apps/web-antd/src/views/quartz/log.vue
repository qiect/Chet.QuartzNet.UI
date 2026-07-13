<script setup lang="ts">
import type {
  FormInstance,
  PaginationProps,
  TableColumnsType,
} from 'ant-design-vue';

import type { LogQueryParams, LogResponseDto } from '../../api/quartz/log';

import { computed, h, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';

import {
  Badge,
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Form,
  Input,
  message,
  Modal,
  Row,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue';

// 导入i18n
import { $t } from '#/locales';

// 导入日志相关类型和API
import { clearLogs, getLogList, LogStatusEnum } from '../../api/quartz/log';

// 日志状态映射
type BadgeStatus = 'default' | 'error' | 'processing' | 'success' | 'warning';

const logStatusMap: Record<
  LogStatusEnum,
  { color: string; status: BadgeStatus; text: () => string }
> = {
  [LogStatusEnum.SUCCESS]: {
    text: () => $t('page.quartz.logPage.statusSuccess'),
    status: 'success',
    color: 'success',
  },
  [LogStatusEnum.ERROR]: {
    text: () => $t('page.quartz.logPage.statusError'),
    status: 'error',
    color: 'error',
  },
  [LogStatusEnum.RUNNING]: {
    text: () => $t('page.quartz.logPage.statusRunning'),
    status: 'processing',
    color: 'processing',
  },
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<LogResponseDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);

// 统计数据
const stats = computed(() => {
  const items = dataSource.value;
  return {
    total: total.value,
    success: items.filter((i) => i.status === LogStatusEnum.SUCCESS).length,
    error: items.filter((i) => i.status === LogStatusEnum.ERROR).length,
    running: items.filter((i) => i.status === LogStatusEnum.RUNNING).length,
  };
});

// 搜索条件
const searchFormRef = ref<FormInstance>();
const searchForm = reactive<LogQueryParams>({
  jobName: '',
  jobGroup: '',
  status: undefined,
  startTime: undefined,
  endTime: undefined,
});

// 详情对话框
const detailModalVisible = ref(false);
const logDetail = ref<LogResponseDto | null>(null);

// 排序配置
const sortBy = ref<string>('');
const sortOrder = ref<string>('');

// 获取列排序状态（将内部 'asc'/'desc' 转为 ant-design-vue 的 'ascend'/'descend'）
const colSortOrder = (field: string): 'ascend' | 'descend' | undefined => {
  if (sortBy.value !== field || !sortOrder.value) return undefined;
  return sortOrder.value === 'asc' ? 'ascend' : 'descend';
};

// 列配置（使用computed属性，当排序状态变化时自动更新）
const columns = computed<TableColumnsType>(() => [
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
    width: 110,
    customRender: ({ record }: { record: LogResponseDto }) => {
      const status = logStatusMap[record.status];
      return h(Badge, {
        status: status?.status || 'default',
        text: status?.text?.() || $t('page.quartz.logPage.unknown'),
      });
    },
  },
  {
    title: $t('page.quartz.logPage.startTime'),
    dataIndex: 'startTime',
    ellipsis: true,
    sorter: true,
    sortOrder: colSortOrder('startTime'),
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.startTime ? formatDateTime(record.startTime) : '-';
    },
  },
  {
    title: $t('page.quartz.logPage.endTime'),
    dataIndex: 'endTime',
    ellipsis: true,
    sorter: true,
    sortOrder: colSortOrder('endTime'),
    customRender: ({ record }: { record: LogResponseDto }) => {
      return record.endTime ? formatDateTime(record.endTime) : '-';
    },
  },
  {
    title: $t('page.quartz.logPage.duration'),
    dataIndex: 'duration',
    ellipsis: true,
    sorter: true,
    sortOrder: colSortOrder('duration'),
    customRender: ({ record }: { record: LogResponseDto }) => {
      const dur = record.duration || 0;
      const color = dur > 5000 ? 'error' : dur > 1000 ? 'warning' : 'success';
      return h(Tag, { color, bordered: false }, { default: () => `${dur} ms` });
    },
  },
  {
    title: $t('page.quartz.logPage.action'),
    width: 80,
    key: 'action',
    fixed: 'right',
    customRender: ({ record }: { record: LogResponseDto }) => {
      return h(
        Button,
        {
          type: 'link',
          size: 'small',
          onClick: () => handleDetail(record),
          disabled: loading.value,
        },
        {
          default: () => $t('page.quartz.logPage.detail'),
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
  showTotal: (total, range) =>
    $t('page.quartz.logPage.paginationTotal', {
      start: range[0],
      end: range[1],
      total,
    }),
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
      const errorMsg = response.errorCode
        ? `${response.message || $t('page.quartz.logPage.loadListFailed')} (${$t('page.quartz.logPage.errorCode')}: ${response.errorCode})`
        : response.message || $t('page.quartz.logPage.loadListFailed');
      message.error(errorMsg);
      dataSource.value = [];
      total.value = 0;
    }
  } catch (error) {
    console.error($t('page.quartz.logPage.loadListFailed'), error);
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
const handleTableChange = (pagination: any, _filters: any, sorter: any) => {
  if (pagination.current !== undefined) {
    currentPage.value = pagination.current;
  }
  if (pagination.pageSize !== undefined) {
    pageSize.value = pagination.pageSize;
  }

  if (sorter.field !== undefined) {
    sortBy.value = sorter.field;
    sortOrder.value =
      sorter.order === 'ascend'
        ? 'asc'
        : sorter.order === 'descend'
          ? 'desc'
          : '';
  }

  loadLogList();
};

// 处理搜索
const handleSearch = async () => {
  if (searchFormRef.value) {
    await searchFormRef.value.validateFields();
  }
  currentPage.value = 1;
  loadLogList();
};

// 处理重置
const handleReset = () => {
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
    okText: $t('page.quartz.jobPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.jobPage.cancel'),
    onOk: async () => {
      try {
        const response = await clearLogs({
          jobName: '',
          jobGroup: '',
          status: undefined,
          startTime: undefined,
          endTime: undefined,
        });
        if (response.success) {
          message.success($t('page.quartz.logPage.clearSuccess'));
          await loadLogList();
        } else {
          message.error(
            response.message || $t('page.quartz.logPage.clearFailed'),
          );
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
    console.error($t('page.quartz.logPage.showDetailFailed'), error);
  }
};

// 复制文本到剪贴板
const handleCopy = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text);
    message.success($t('page.quartz.logPage.copiedText'));
  } catch {
    message.error($t('page.quartz.logPage.copyText'));
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
      <!-- 统计概览 -->
      <Row :gutter="[12, 12]" class="mb-3">
        <Col :xs="12" :sm="12" :md="6">
          <Card
            class="stat-mini-card"
            :bordered="false"
            :body-style="{ padding: '14px 16px' }"
          >
            <div class="flex items-center justify-between">
              <div>
                <div class="text-xs text-muted-foreground">
                  {{ $t('page.quartz.logPage.totalLogs') }}
                </div>
                <div class="mt-1 text-xl font-bold text-foreground">
                  {{ stats.total }}
                </div>
              </div>
              <div class="stat-mini-icon bg-primary/10 text-primary">
                <span style="font-size: 18px">&#x1F4CB;</span>
              </div>
            </div>
          </Card>
        </Col>
        <Col :xs="12" :sm="12" :md="6">
          <Card
            class="stat-mini-card"
            :bordered="false"
            :body-style="{ padding: '14px 16px' }"
          >
            <div class="flex items-center justify-between">
              <div>
                <div class="text-xs text-muted-foreground">
                  {{ $t('page.quartz.logPage.successLogs') }}
                </div>
                <div class="mt-1 text-xl font-bold text-success">
                  {{ stats.success }}
                </div>
              </div>
              <div class="stat-mini-icon bg-success/10 text-success">
                <span style="font-size: 18px">&#x2713;</span>
              </div>
            </div>
          </Card>
        </Col>
        <Col :xs="12" :sm="12" :md="6">
          <Card
            class="stat-mini-card"
            :bordered="false"
            :body-style="{ padding: '14px 16px' }"
          >
            <div class="flex items-center justify-between">
              <div>
                <div class="text-xs text-muted-foreground">
                  {{ $t('page.quartz.logPage.failedLogs') }}
                </div>
                <div class="mt-1 text-xl font-bold text-destructive">
                  {{ stats.error }}
                </div>
              </div>
              <div class="stat-mini-icon bg-destructive/10 text-destructive">
                <span style="font-size: 18px">&#x2717;</span>
              </div>
            </div>
          </Card>
        </Col>
        <Col :xs="12" :sm="12" :md="6">
          <Card
            class="stat-mini-card"
            :bordered="false"
            :body-style="{ padding: '14px 16px' }"
          >
            <div class="flex items-center justify-between">
              <div>
                <div class="text-xs text-muted-foreground">
                  {{ $t('page.quartz.logPage.runningLogs') }}
                </div>
                <div class="mt-1 text-xl font-bold text-warning">
                  {{ stats.running }}
                </div>
              </div>
              <div class="stat-mini-icon bg-warning/10 text-warning">
                <span style="font-size: 18px">&#x21bb;</span>
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      <!-- 搜索卡片 -->
      <Card class="search-card mb-3" :body-style="{ padding: '16px 20px' }">
        <Form ref="searchFormRef" :model="searchForm" layout="inline">
          <Row :gutter="[16, 12]" class="w-full">
            <Col :xs="24" :sm="12" :md="8" :lg="5" :xl="4">
              <Form.Item
                :label="$t('page.quartz.logPage.jobName')"
                name="jobName"
                class="mb-0"
              >
                <Input
                  v-model:value="searchForm.jobName"
                  :placeholder="$t('page.quartz.logPage.placeholderJobName')"
                  allow-clear
                />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="5" :xl="4">
              <Form.Item
                :label="$t('page.quartz.logPage.jobGroup')"
                name="jobGroup"
                class="mb-0"
              >
                <Input
                  v-model:value="searchForm.jobGroup"
                  :placeholder="$t('page.quartz.logPage.placeholderJobGroup')"
                  allow-clear
                />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="5" :xl="4">
              <Form.Item
                :label="$t('page.quartz.logPage.executionStatus')"
                name="status"
                class="mb-0"
              >
                <Select
                  v-model:value="searchForm.status"
                  :placeholder="$t('page.quartz.logPage.placeholderStatus')"
                  allow-clear
                >
                  <Select.Option :value="LogStatusEnum.SUCCESS">
                    {{ $t('page.quartz.logPage.statusSuccess') }}
                  </Select.Option>
                  <Select.Option :value="LogStatusEnum.ERROR">
                    {{ $t('page.quartz.logPage.statusError') }}
                  </Select.Option>
                  <Select.Option :value="LogStatusEnum.RUNNING">
                    {{ $t('page.quartz.logPage.statusRunning') }}
                  </Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="5" :xl="5">
              <Form.Item
                :label="$t('page.quartz.logPage.startTime')"
                name="startTime"
                class="mb-0"
              >
                <DatePicker
                  v-model:value="searchForm.startTime"
                  show-time
                  :placeholder="$t('page.quartz.logPage.selectStartTime')"
                  style="width: 100%"
                />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="4" :xl="7" class="text-right">
              <Form.Item class="mb-0">
                <Space>
                  <Button type="primary" @click="handleSearch">
                    {{ $t('page.quartz.logPage.search') }}
                  </Button>
                  <Button @click="handleReset">
                    {{ $t('page.quartz.logPage.reset') }}
                  </Button>
                </Space>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      <!-- 日志列表卡片 -->
      <Card :body-style="{ padding: '16px 20px' }">
        <div class="mb-4 flex items-center justify-end">
          <Button danger @click="handleClear">
            {{ $t('page.quartz.logPage.clearLogs') }}
          </Button>
        </div>
        <Table
          :columns="columns"
          :data-source="dataSource"
          :pagination="pagination"
          :loading="loading"
          :row-key="(record) => record.logId"
          size="middle"
          @change="handleTableChange"
          :scroll="{ x: 'max-content' }"
        >
          <template #emptyText>
            <Empty :description="$t('page.quartz.logPage.noLogData')" />
          </template>
        </Table>
      </Card>

      <!-- 详情对话框 -->
      <Modal
        v-model:open="detailModalVisible"
        :title="$t('page.quartz.logPage.logDetail')"
        width="900px"
        :footer="null"
        :destroy-on-close="true"
        centered
      >
        <div v-if="logDetail" class="log-detail">
          <!-- 头部信息 -->
          <div class="detail-header mb-4">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <Typography.Text strong class="text-base">
                  {{ logDetail.jobName }}
                </Typography.Text>
                <Typography.Text type="secondary" class="text-sm">
                  {{ logDetail.jobGroup }}
                </Typography.Text>
              </div>
              <Badge
                :status="logStatusMap[logDetail.status]?.status || 'default'"
                :text="
                  logStatusMap[logDetail.status]?.text?.() ||
                  $t('page.quartz.logPage.unknown')
                "
              />
            </div>

            <!-- 基本信息行 -->
            <div class="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.logPage.executionDuration')
                }}</span>
                <span class="info-value font-mono"
                  >{{ logDetail.duration || 0 }} ms</span
                >
              </div>
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.logPage.startDateTime')
                }}</span>
                <span class="info-value">{{
                  logDetail.startTime
                    ? formatDateTime(logDetail.startTime)
                    : '-'
                }}</span>
              </div>
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.logPage.endDateTime')
                }}</span>
                <span class="info-value">{{
                  logDetail.endTime ? formatDateTime(logDetail.endTime) : '-'
                }}</span>
              </div>
            </div>
          </div>

          <!-- 内容区域 -->
          <div class="detail-content space-y-4">
            <!-- 执行信息 -->
            <div class="content-section">
              <div class="section-header">
                <Typography.Text strong>
                  {{ $t('page.quartz.logPage.executionInfo') }}
                </Typography.Text>
                <Button
                  v-if="logDetail.message"
                  type="text"
                  size="small"
                  @click="handleCopy(logDetail.message)"
                >
                  {{ $t('page.quartz.logPage.copyText') }}
                </Button>
              </div>
              <div class="content-card info-card">
                <pre class="code-block">{{
                  logDetail.message || $t('page.quartz.logPage.noExecutionInfo')
                }}</pre>
              </div>
            </div>

            <!-- 错误信息 -->
            <div v-if="logDetail.errorMessage" class="content-section">
              <div class="section-header">
                <Typography.Text strong class="text-destructive">
                  {{ $t('page.quartz.logPage.errorInfo') }}
                </Typography.Text>
                <Button
                  type="text"
                  size="small"
                  @click="handleCopy(logDetail.errorMessage)"
                >
                  {{ $t('page.quartz.logPage.copyText') }}
                </Button>
              </div>
              <div class="content-card error-card">
                <pre class="code-block">{{ logDetail.errorMessage }}</pre>
              </div>
            </div>

            <!-- 异常信息 -->
            <div v-if="logDetail.exception" class="content-section">
              <div class="section-header">
                <Typography.Text strong class="text-destructive">
                  {{ $t('page.quartz.logPage.exceptionInfo') }}
                </Typography.Text>
                <Button
                  type="text"
                  size="small"
                  @click="handleCopy(logDetail.exception)"
                >
                  {{ $t('page.quartz.logPage.copyText') }}
                </Button>
              </div>
              <div class="content-card error-card">
                <pre class="code-block">{{ logDetail.exception }}</pre>
              </div>
            </div>

            <!-- 执行结果 -->
            <div v-if="logDetail.result" class="content-section">
              <div class="section-header">
                <Typography.Text strong class="text-success">
                  {{ $t('page.quartz.logPage.executionResult') }}
                </Typography.Text>
                <Button
                  type="text"
                  size="small"
                  @click="
                    handleCopy(
                      typeof logDetail.result === 'string'
                        ? logDetail.result
                        : JSON.stringify(logDetail.result, null, 2),
                    )
                  "
                >
                  {{ $t('page.quartz.logPage.copyText') }}
                </Button>
              </div>
              <div class="content-card success-card">
                <pre class="code-block">{{
                  typeof logDetail.result === 'string'
                    ? logDetail.result
                    : JSON.stringify(logDetail.result, null, 2)
                }}</pre>
              </div>
            </div>

            <!-- 作业数据 -->
            <div v-if="logDetail.jobData" class="content-section">
              <div class="section-header">
                <Typography.Text strong>
                  {{ $t('page.quartz.logPage.jobData') }}
                </Typography.Text>
                <Button
                  type="text"
                  size="small"
                  @click="
                    handleCopy(
                      typeof logDetail.jobData === 'string'
                        ? logDetail.jobData
                        : JSON.stringify(logDetail.jobData, null, 2),
                    )
                  "
                >
                  {{ $t('page.quartz.logPage.copyText') }}
                </Button>
              </div>
              <div class="content-card info-card">
                <pre class="code-block">{{
                  typeof logDetail.jobData === 'string'
                    ? logDetail.jobData
                    : JSON.stringify(logDetail.jobData, null, 2)
                }}</pre>
              </div>
            </div>
          </div>
        </div>

        <!-- 底部按钮 -->
        <div class="mt-5 flex justify-end">
          <Button @click="detailModalVisible = false" type="primary">
            {{ $t('page.quartz.logPage.close') }}
          </Button>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
/* 统计迷你卡片 */
.stat-mini-card {
  background: hsl(var(--card)) !important;
  border: 1px solid hsl(var(--border)) !important;
  border-radius: 10px;
  transition: border-color 0.2s;
}

.stat-mini-card:hover {
  border-color: hsl(var(--primary) / 0.3) !important;
}

.stat-mini-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

/* 搜索卡片 */
.search-card {
  background: hsl(var(--card)) !important;
  border: 1px solid hsl(var(--border)) !important;
  border-radius: 10px;
}

/* 详情头部 */
.detail-header {
  padding: 16px;
  background: hsl(var(--muted) / 0.5);
  border-radius: 10px;
  border: 1px solid hsl(var(--border));
}

/* 信息项 */
.info-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.info-label {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.info-value {
  font-size: 13px;
  font-weight: 500;
  color: hsl(var(--foreground));
}

/* 内容区域 */
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.content-card {
  border-radius: 8px;
  border: 1px solid hsl(var(--border));
  background: hsl(var(--card));
  transition: box-shadow 0.2s;
}

.content-card:hover {
  box-shadow: 0 2px 8px hsl(var(--foreground) / 0.06);
}

.error-card {
  background: hsl(var(--destructive) / 0.04);
  border-color: hsl(var(--destructive) / 0.2);
}

.error-card .code-block {
  color: hsl(var(--destructive));
}

.success-card {
  background: hsl(var(--success) / 0.04);
  border-color: hsl(var(--success) / 0.2);
}

.success-card .code-block {
  color: hsl(var(--success));
}

.info-card {
  background: hsl(var(--primary) / 0.03);
  border-color: hsl(var(--primary) / 0.15);
}

/* 代码块 */
.code-block {
  margin: 0;
  padding: 12px;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-x: auto;
  max-height: 360px;
  overflow-y: auto;
  color: hsl(var(--foreground));
}

/* 间距工具类 */
.mb-3 {
  margin-bottom: 12px;
}

.text-right {
  text-align: right;
}
</style>
