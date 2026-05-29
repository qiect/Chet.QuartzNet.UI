<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue';
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
  Tag,
  Table,
  Card,
  Row,
  Col,
  Tooltip,
  InputNumber,
  Typography,
  Alert,
} from 'ant-design-vue';
import type { FormInstance, PaginationProps } from 'ant-design-vue';

type SortOrder = 'ascend' | 'descend' | undefined;

import { $t } from '#/locales';

import {
  NotificationStatusEnum,
  getPushPlusConfig,
  savePushPlusConfig,
  sendTestNotification,
  getNotifications,
  deleteNotification,
  clearNotifications,
} from '../../api/quartz/notification';
import type {
  PushPlusConfigDto,
  QuartzNotificationDto,
  NotificationQueryDto,
} from '../../api/quartz/notification';

// 通知状态映射
const notificationStatusMap = {
  [NotificationStatusEnum.Pending]: { text: () => $t('page.quartz.notificationPage.statusPending'), status: 'default' },
  [NotificationStatusEnum.Sent]: { text: () => $t('page.quartz.notificationPage.statusSent'), status: 'success' },
  [NotificationStatusEnum.Failed]: { text: () => $t('page.quartz.notificationPage.statusFailed'), status: 'error' },
};

// 响应式数据
const loading = ref(false);
const saveLoading = ref(false);
const dataSource = ref<QuartzNotificationDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);

// 详情对话框
const detailModalVisible = ref(false);
const currentNotification = ref<QuartzNotificationDto | null>(null);

// 搜索条件
const searchFormRef = ref<FormInstance>();
const searchForm = ref<Partial<NotificationQueryDto>>({
  status: undefined,
  triggeredBy: '',
});

// 排序配置
const sortBy = ref<string>('');
const sortOrder = ref<SortOrder>(undefined);

// 编辑对话框
const configModalVisible = ref(false);
const configForm = reactive<PushPlusConfigDto>({
  token: '',
  channel: 'wechat',
  template: 'html',
  topic: '',
  option: '',
  to: '',
  callbackUrl: '',
  timestamp: undefined,
  enable: false,
  strategy: {
    notifyOnJobSuccess: false,
    notifyOnJobFailure: true,
    notifyOnSchedulerError: true,
  },
});

const formRef = ref<FormInstance>();
const advancedVisible = ref(false);

// Option 动态占位符（根据渠道变化）
const optionPlaceholder = computed(() => {
  const placeholders: Record<string, string> = {
    webhook: $t('page.quartz.notificationPage.optionPlaceholderWebhook'),
    cp: $t('page.quartz.notificationPage.optionPlaceholderCp'),
    mail: $t('page.quartz.notificationPage.optionPlaceholderMail'),
  };
  return placeholders[configForm.channel] || '';
});

// 是否显示渠道参数区域
const showChannelParams = computed(() => {
  return ['webhook', 'cp', 'mail', 'wechat'].includes(configForm.channel);
});

// 渠道提示信息
const channelTipMessage = computed(() => {
  const tips: Record<string, string> = {
    webhook: $t('page.quartz.notificationPage.channelTipWebhook'),
    cp: $t('page.quartz.notificationPage.channelTipCp'),
    mail: $t('page.quartz.notificationPage.channelTipMail'),
    wechat: $t('page.quartz.notificationPage.channelTipWechat'),
  };
  return tips[configForm.channel] || '';
});

// 列配置
const columns = computed(() => [
  {
    title: $t('page.quartz.notificationPage.title'),
    dataIndex: 'title',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.notificationPage.triggeredBy'),
    dataIndex: 'triggeredBy',
    ellipsis: true,
  },
  {
    title: $t('page.quartz.notificationPage.status'),
    dataIndex: 'status',
    ellipsis: true,
    width: 100,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      const status = notificationStatusMap[record.status];
      return {
        children: h(
          Tag,
          { color: status?.status || 'default' },
          status?.text?.() || record.status || $t('page.quartz.notificationPage.unknown'),
        ),
      };
    },
  },
  {
    title: $t('page.quartz.notificationPage.sendTime'),
    dataIndex: 'sendTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'sendTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      return {
        children: record.sendTime ? formatDateTime(record.sendTime) : '-',
      };
    },
  },
  {
    title: $t('page.quartz.notificationPage.duration'),
    dataIndex: 'duration',
    ellipsis: true,
    width: 100,
    sorter: true,
    sortOrder: sortBy.value === 'duration' ? sortOrder.value : undefined,
  },
  {
    title: $t('page.quartz.notificationPage.createTime'),
    dataIndex: 'createTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'createTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      return {
        children: record.createTime ? formatDateTime(record.createTime) : '-',
      };
    },
  },
  {
    title: $t('page.quartz.notificationPage.action'),
    key: 'action',
    width: 100,
    fixed: 'right',
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      return {
        children: h(Space, { size: 4 }, [
          h(
            Tooltip,
            { title: $t('page.quartz.notificationPage.detail') },
            () => h(Button, { type: 'link', size: 'small', onClick: () => handleDetail(record) }, () => $t('page.quartz.notificationPage.detail')),
          ),
          h(
            Tooltip,
            { title: $t('page.quartz.notificationPage.delete') },
            () => h(Button, { type: 'link', size: 'small', danger: true, onClick: () => handleDelete(record) }, () => $t('page.quartz.notificationPage.delete')),
          ),
        ]),
      };
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
  showTotal: (total, range) => $t('page.quartz.notificationPage.paginationTotal', { start: range[0], end: range[1], total }),
  pageSizeOptions: ['10', '20', '50', '100'],
}));

// 表格变化事件处理
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
        ? 'ascend'
        : sorter.order === 'descend'
          ? 'descend'
          : undefined;
  }

  loadNotificationList();
};

// 加载通知列表
const loadNotificationList = async () => {
  loading.value = true;
  try {
    const response = await getNotifications({
      pageIndex: currentPage.value,
      pageSize: pageSize.value,
      status: searchForm.value.status,
      triggeredBy: searchForm.value.triggeredBy,
      sortBy: sortBy.value,
      sortOrder: sortOrder.value,
    });

    dataSource.value = response.data?.items || [];
    total.value = response.data?.totalCount || 0;
  } catch (error) {
    message.error($t('page.quartz.notificationPage.loadListFailed'));
    console.error($t('page.quartz.notificationPage.loadListFailed'), error);
  } finally {
    loading.value = false;
  }
};

// 处理搜索
const handleSearch = async () => {
  if (searchFormRef.value) {
    await searchFormRef.value.validateFields();
  }
  currentPage.value = 1;
  loadNotificationList();
};

// 处理重置
const handleReset = () => {
  searchForm.value = {
    status: undefined,
    triggeredBy: '',
  };
  currentPage.value = 1;
  loadNotificationList();
};

// 打开配置对话框
const handleOpenConfigModal = async () => {
  try {
    const response = await getPushPlusConfig();
    Object.assign(configForm, response.data);
    configModalVisible.value = true;
  } catch (error) {
    message.error($t('page.quartz.notificationPage.getConfigFailed'));
    console.error($t('page.quartz.notificationPage.getConfigFailed'), error);
  }
};

// 保存配置
const handleSaveConfig = async () => {
  if (!formRef.value) return;

  try {
    await formRef.value.validateFields();
    saveLoading.value = true;

    const response = await savePushPlusConfig(configForm);
    if (response.success) {
      message.success($t('page.quartz.notificationPage.saveConfigSuccess'));
      configModalVisible.value = false;
    } else {
      message.error(response.message || $t('page.quartz.notificationPage.saveConfigFailed'));
    }
  } catch (error: any) {
    if (error.errorFields) {
      return;
    }
    const errorMessage = error.message || $t('page.quartz.notificationPage.saveConfigFailed');
    message.error(errorMessage);
    console.error($t('page.quartz.notificationPage.saveConfigFailed'), error);
  } finally {
    saveLoading.value = false;
  }
};

// 发送测试通知
const handleSendTest = async () => {
  try {
    loading.value = true;
    const response = await sendTestNotification();
    if (response.success) {
      message.success($t('page.quartz.notificationPage.testSendSuccess'));
      loadNotificationList();
    } else {
      message.error(response.message || $t('page.quartz.notificationPage.testSendFailed'));
    }
  } catch (error) {
    message.error($t('page.quartz.notificationPage.testSendFailed'));
    console.error($t('page.quartz.notificationPage.testSendFailed'), error);
  } finally {
    loading.value = false;
  }
};

// 查看详情
const handleDetail = (notification: QuartzNotificationDto) => {
  currentNotification.value = notification;
  detailModalVisible.value = true;
};

// 删除通知
const handleDelete = (notification: QuartzNotificationDto) => {
  Modal.confirm({
    title: $t('page.quartz.notificationPage.confirmDelete'),
    content: $t('page.quartz.notificationPage.confirmDeleteContent'),
    okText: $t('page.quartz.notificationPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.notificationPage.cancel'),
    async onOk() {
      try {
        const response = await deleteNotification(notification.notificationId);
        if (response.success) {
          message.success($t('page.quartz.notificationPage.deleteSuccess'));
          loadNotificationList();
        } else {
          message.error(response.message || $t('page.quartz.notificationPage.deleteFailed'));
        }
      } catch (error) {
        message.error($t('page.quartz.notificationPage.deleteFailed'));
        console.error($t('page.quartz.notificationPage.deleteFailed'), error);
      }
    },
  });
};

// 清空通知
const handleClearNotifications = () => {
  Modal.confirm({
    title: $t('page.quartz.notificationPage.confirmClear'),
    content: $t('page.quartz.notificationPage.confirmClearContent'),
    okText: $t('page.quartz.notificationPage.ok'),
    okType: 'danger',
    cancelText: $t('page.quartz.notificationPage.cancel'),
    async onOk() {
      try {
        const response = await clearNotifications({
          pageIndex: 1,
          pageSize: 1,
          status: searchForm.value.status,
          triggeredBy: searchForm.value.triggeredBy,
        });
        if (response.success) {
          message.success($t('page.quartz.notificationPage.clearSuccess'));
          loadNotificationList();
        } else {
          message.error(response.message || $t('page.quartz.notificationPage.clearFailed'));
        }
      } catch (error) {
        message.error($t('page.quartz.notificationPage.clearFailed'));
        console.error($t('page.quartz.notificationPage.clearFailed'), error);
      }
    },
  });
};

// 生命周期
onMounted(() => {
  loadNotificationList();
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <Card class="mb-4">
        <Form ref="searchFormRef" :model="searchForm" layout="horizontal" :label-align="'right'">
          <Row :gutter="16">
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="6" :xxl="4">
              <Form.Item :label="$t('page.quartz.notificationPage.notificationStatus')" name="status">
                <Select v-model:value="searchForm.status" :placeholder="$t('page.quartz.notificationPage.placeholderStatus')" allowClear>
                  <Select.Option :value="NotificationStatusEnum.Pending">{{ $t('page.quartz.notificationPage.statusPending') }}</Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Sent">{{ $t('page.quartz.notificationPage.statusSent') }}</Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Failed">{{ $t('page.quartz.notificationPage.statusFailed') }}</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="6" :xxl="6">
              <Form.Item :label="$t('page.quartz.notificationPage.triggeredBy')" name="triggeredBy">
                <Input v-model:value="searchForm.triggeredBy" :placeholder="$t('page.quartz.notificationPage.placeholderTriggeredBy')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" :lg="8" :xl="12" :xxl="14" class="text-right">
              <Space>
                <Button type="primary" @click="handleSearch">{{ $t('page.quartz.notificationPage.search') }}</Button>
                <Button @click="handleReset">{{ $t('page.quartz.notificationPage.reset') }}</Button>
              </Space>
            </Col>
          </Row>
        </Form>
      </Card>

      <!-- 通知管理卡片 -->
      <Card>
        <div class="mb-4 flex items-center justify-between">
          <Space>
            <Button type="primary" @click="handleOpenConfigModal">{{ $t('page.quartz.notificationPage.notificationConfig') }}</Button>
            <Button type="default" @click="handleSendTest">{{ $t('page.quartz.notificationPage.sendTestNotification') }}</Button>
          </Space>
          <Space>
            <Button danger @click="handleClearNotifications">{{ $t('page.quartz.notificationPage.clearAll') }}</Button>
          </Space>
        </div>
        <Table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading"
          :rowKey="(record) => record.notificationId" @change="handleTableChange" size="middle"
          :scroll="{ x: 'max-content' }" />
      </Card>

      <!-- 配置对话框 -->
      <Modal v-model:open="configModalVisible" :title="$t('page.quartz.notificationPage.notificationConfig')" width="720px" destroyOnClose
        @cancel="configModalVisible = false" centered>
        <div class="config-modal-content">
          <div class="config-tip">
            <span class="tip-icon">ⓘ</span>
            <span>{{ $t('page.quartz.notificationPage.configPushPlusDesc') }}</span>
          </div>

          <Form ref="formRef" :model="configForm" layout="vertical" class="custom-form" size="small">
            <!-- 基础配置 -->
            <section class="form-section">
              <div class="section-header">
                <span class="title">{{ $t('page.quartz.notificationPage.basicConfig') }}</span>
                <div class="header-action">
                  <span class="label">{{ $t('page.quartz.notificationPage.serviceEnableStatus') }}</span>
                  <Switch v-model:checked="configForm.enable" size="small" />
                </div>
              </div>

              <Row :gutter="12" align="middle">
                <Col :span="16">
                  <Form.Item label="PushPlus Token" name="token"
                    :rules="[{ required: configForm.enable, message: $t('page.quartz.notificationPage.tokenRequired') }]">
                    <Input.Password v-model:value="configForm.token" :placeholder="$t('page.quartz.notificationPage.tokenPlaceholder')" autocomplete="off" />
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item :label="$t('page.quartz.notificationPage.topicLabel')" name="topic">
                    <Input v-model:value="configForm.topic" :placeholder="$t('page.quartz.notificationPage.topicPlaceholder')" />
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item :label="$t('page.quartz.notificationPage.pushChannel')" name="channel">
                    <Select v-model:value="configForm.channel">
                      <Select.Option value="wechat">{{ $t('page.quartz.notificationPage.channelWechat') }}</Select.Option>
                      <Select.Option value="cp">{{ $t('page.quartz.notificationPage.channelWechatWork') }}</Select.Option>
                      <Select.Option value="webhook">{{ $t('page.quartz.notificationPage.channelWebhook') }}</Select.Option>
                      <Select.Option value="mail">{{ $t('page.quartz.notificationPage.channelEmail') }}</Select.Option>
                      <Select.Option value="sms">{{ $t('page.quartz.notificationPage.channelSms') }}</Select.Option>
                      <Select.Option value="voice">{{ $t('page.quartz.notificationPage.channelVoice') }}</Select.Option>
                      <Select.Option value="extension">{{ $t('page.quartz.notificationPage.channelExtension') }}</Select.Option>
                      <Select.Option value="app">{{ $t('page.quartz.notificationPage.channelApp') }}</Select.Option>
                    </Select>
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item :label="$t('page.quartz.notificationPage.messageTemplate')" name="template">
                    <Select v-model:value="configForm.template">
                      <Select.Option value="html">{{ $t('page.quartz.notificationPage.templateHtml') }}</Select.Option>
                      <Select.Option value="txt">{{ $t('page.quartz.notificationPage.templateTxt') }}</Select.Option>
                      <Select.Option value="json">{{ $t('page.quartz.notificationPage.templateJson') }}</Select.Option>
                      <Select.Option value="markdown">{{ $t('page.quartz.notificationPage.templateMarkdown') }}</Select.Option>
                    </Select>
                  </Form.Item>
                </Col>
                <Col v-if="['webhook', 'cp', 'mail'].includes(configForm.channel)" :span="8">
                  <Form.Item :label="$t('page.quartz.notificationPage.optionLabel')" name="option"
                    :rules="[{ required: ['webhook', 'cp'].includes(configForm.channel), message: $t('page.quartz.notificationPage.optionRequired') }]">
                    <Input v-model:value="configForm.option" :placeholder="optionPlaceholder" />
                  </Form.Item>
                </Col>
                <Col v-if="['wechat', 'cp'].includes(configForm.channel)" :span="8">
                  <Form.Item :label="$t('page.quartz.notificationPage.toLabel')" name="to">
                    <Input v-model:value="configForm.to" :placeholder="$t('page.quartz.notificationPage.toPlaceholder')" />
                  </Form.Item>
                </Col>
              </Row>

              <Alert v-if="showChannelParams" :message="channelTipMessage" type="warning" show-icon class="channel-tip" />
            </section>

            <!-- 通知策略 -->
            <section class="form-section last">
              <div class="section-header">
                <span class="title">{{ $t('page.quartz.notificationPage.notificationStrategy') }}</span>
              </div>

              <div class="strategy-grid">
                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">{{ $t('page.quartz.notificationPage.jobSuccess') }}</div>
                    <div class="desc">{{ $t('page.quartz.notificationPage.jobSuccessDesc') }}</div>
                  </div>
                  <Switch v-model:checked="configForm.strategy.notifyOnJobSuccess" />
                </div>

                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">{{ $t('page.quartz.notificationPage.jobFailure') }}</div>
                    <div class="desc">{{ $t('page.quartz.notificationPage.jobFailureDesc') }}</div>
                  </div>
                  <Switch v-model:checked="configForm.strategy.notifyOnJobFailure" />
                </div>

                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">{{ $t('page.quartz.notificationPage.schedulerError') }}</div>
                    <div class="desc">{{ $t('page.quartz.notificationPage.schedulerErrorDesc') }}</div>
                  </div>
                  <Switch v-model:checked="configForm.strategy.notifyOnSchedulerError" />
                </div>
              </div>
            </section>

            <!-- 高级配置 -->
            <div class="advanced-section">
              <div class="section-header" @click="advancedVisible = !advancedVisible">
                <span class="title">{{ $t('page.quartz.notificationPage.advancedConfig') }}</span>
                <span class="toggle-icon" :class="{ expanded: advancedVisible }">›</span>
              </div>
              <div v-show="advancedVisible" class="advanced-body">
                <Row :gutter="12">
                  <Col :span="16">
                    <Form.Item :label="$t('page.quartz.notificationPage.callbackUrlLabel')" name="callbackUrl">
                      <Input v-model:value="configForm.callbackUrl" :placeholder="$t('page.quartz.notificationPage.callbackUrlPlaceholder')" />
                    </Form.Item>
                  </Col>
                  <Col :span="8">
                    <Form.Item :label="$t('page.quartz.notificationPage.timestampLabel')" name="timestamp">
                      <InputNumber v-model:value="configForm.timestamp" :placeholder="$t('page.quartz.notificationPage.timestampPlaceholder')"
                        :precision="0" :min="0" style="width: 100%" />
                    </Form.Item>
                  </Col>
                </Row>
              </div>
            </div>
          </Form>
        </div>

        <template #footer>
          <div class="modal-footer">
            <Button @click="configModalVisible = false">{{ $t('page.quartz.notificationPage.cancel') }}</Button>
            <Button type="primary" :loading="saveLoading" @click="handleSaveConfig">{{ $t('page.quartz.notificationPage.saveConfig') }}</Button>
          </div>
        </template>
      </Modal>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="$t('page.quartz.notificationPage.notificationDetail')" width="80%" :max-width="1200" :footer="null"
        :destroyOnClose="true">
        <div v-if="currentNotification" class="notification-detail">
          <!-- 头部信息 -->
          <div class="detail-header mb-4 rounded-lg p-5">
            <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
              <Typography.Title :level="4" class="m-0 text-ellipsis max-w-[70%]">
                {{ currentNotification.title }}
              </Typography.Title>
              <Tag :color="notificationStatusMap[currentNotification.status].status"
                class="text-lg px-4 py-1 text-base">
                {{ notificationStatusMap[currentNotification.status].text() }}
              </Tag>
            </div>

            <div class="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-2 lg:grid-cols-2 xl:grid-cols-4">
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.notificationPage.triggerSource') }}</span>
                <span class="text-sm">{{ currentNotification.triggeredBy || '-' }}</span>
              </div>
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.notificationPage.sendDateTime') }}</span>
                <span class="text-sm">{{
                  currentNotification.sendTime
                    ? formatDateTime(currentNotification.sendTime)
                    : '-' }}
                </span>
              </div>
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.notificationPage.sendDuration') }}</span>
                <span class="text-sm">{{
                  currentNotification.duration
                    ? `${currentNotification.duration} ms`
                    : '0 ms' }}
                </span>
              </div>
              <div class="info-item flex items-center gap-2 p-2 rounded">
                <span class="font-semibold text-sm opacity-80">{{ $t('page.quartz.notificationPage.createDateTime') }}</span>
                <span class="text-sm">{{ formatDateTime(currentNotification.createTime) }}</span>
              </div>
            </div>
          </div>

          <!-- 内容区域 -->
          <div class="detail-content space-y-6">
            <div class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.notificationPage.notificationContent') }}</Typography.Title>
              <div class="content-card info-card rounded-lg p-4">
                <div class="word-break-break-word text-sm" v-html="currentNotification.content"></div>
              </div>
            </div>

            <div v-if="currentNotification.errorMessage" class="content-section">
              <Typography.Title :level="5" class="mb-3">{{ $t('page.quartz.notificationPage.errorInfo') }}</Typography.Title>
              <div class="content-card error-card rounded-lg p-4">
                <pre class="code-block word-break-break-word m-0 whitespace-pre-wrap text-sm">{{
                  currentNotification.errorMessage }}</pre>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-6 flex justify-end">
          <Button @click="detailModalVisible = false" type="primary" size="large" class="px-6">
            {{ $t('page.quartz.notificationPage.close') }}
          </Button>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
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

.error-card {
  background: rgba(var(--color-error-rgb), 0.1) !important;
  border: 1px solid var(--color-error-light) !important;
}

.info-card {
  background: rgba(var(--color-success-rgb), 0.1) !important;
  border: 1px solid var(--color-success-light) !important;
}

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

.error-card :deep(.code-block) {
  color: #ff4d4f !important;
}
</style>

<style scoped lang="less">
.config-modal-content {
  margin-top: -8px;

  :deep(.ant-form-item) {
    margin-bottom: 12px;
  }

  :deep(.ant-form-item-label) {
    padding-bottom: 2px;
  }

  :deep(.ant-form-item-label > label) {
    font-size: 13px;
  }

  .form-section {
    padding: 12px;
    background: var(--ant-color-fill-quaternary);
    border-radius: 8px;
    margin-bottom: 12px;
    border: 1px solid var(--ant-color-border-secondary);

    .section-header {
      display: flex;
      align-items: center;
      margin-bottom: 10px;
      padding-bottom: 8px;
      border-bottom: 1px solid var(--ant-color-border-split);

      .title {
        font-size: 14px;
        font-weight: 600;
        flex: 1;
        color: var(--ant-color-text);
      }

      .header-action {
        display: flex;
        align-items: center;
        gap: 8px;

        .label {
          font-size: 12px;
          color: var(--ant-color-text-description);
        }
      }
    }

    &.last {
      margin-bottom: 0;
    }
  }

  .channel-tip {
    border-radius: 6px;
    margin-top: 4px;
  }

  .config-tip {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 10px;
    background: var(--ant-color-info-bg);
    border: 1px solid var(--ant-color-info-border);
    border-radius: 6px;
    margin-bottom: 12px;
    font-size: 12px;
    color: var(--ant-color-text-description);

    .tip-icon {
      flex-shrink: 0;
      font-size: 14px;
      color: var(--ant-color-primary);
    }
  }

  .advanced-section {
    margin-top: 12px;
    background: var(--ant-color-fill-quaternary);
    border-radius: 8px;
    border: 1px solid var(--ant-color-border-secondary);
    overflow: hidden;

    .section-header {
      display: flex;
      align-items: center;
      padding: 8px 12px;
      cursor: pointer;
      user-select: none;
      transition: background 0.2s;

      &:hover {
        background: var(--ant-color-fill-tertiary);
      }

      .title {
        font-size: 13px;
        font-weight: 600;
        color: var(--ant-color-text-description);
      }

      .toggle-icon {
        margin-left: 6px;
        font-size: 16px;
        color: var(--ant-color-text-description);
        transition: transform 0.2s ease;
        display: inline-block;
        line-height: 1;

        &.expanded {
          transform: rotate(90deg);
        }
      }
    }

    .advanced-body {
      padding: 0 12px 12px;
      border-top: 1px solid var(--ant-color-border-secondary);
    }
  }

  .strategy-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 8px;

    .strategy-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 10px;
      background: var(--ant-component-background);
      border: 1px solid var(--ant-color-border-secondary);
      border-radius: 6px;
      transition: all 0.2s ease;

      &:hover {
        border-color: var(--ant-color-primary-border);
      }

      .strategy-info {
        .name {
          font-size: 13px;
          font-weight: 500;
          color: var(--ant-color-text);
        }

        .desc {
          font-size: 11px;
          color: var(--ant-color-text-description);
          margin-top: 1px;
        }
      }
    }
  }
}

.modal-footer {
  padding: 10px 0;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

::where(.dark) {
  .config-modal-content {
    .form-section {
      background: rgba(255, 255, 255, 0.04);
      border-color: #303030;
    }

    .advanced-section {
      background: rgba(255, 255, 255, 0.04);
      border-color: #303030;
    }

    .strategy-item {
      background: #141414 !important;
      border-color: #303030 !important;

      &:hover {
        border-color: var(--ant-color-primary) !important;
      }
    }
  }
}

.mb-3 {
  margin-bottom: 12px;
}
</style>
