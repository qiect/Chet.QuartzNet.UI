<script setup lang="ts">
import type {
  FormInstance,
  PaginationProps,
  TableColumnsType,
} from 'ant-design-vue';

import type {
  NotificationQueryDto,
  PushPlusConfigDto,
  QuartzNotificationDto,
} from '../../api/quartz/notification';

import { computed, h, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { formatDateTime } from '@vben/utils';

import {
  Alert,
  Badge,
  Button,
  Card,
  Col,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Row,
  Select,
  Space,
  Switch,
  Table,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import { $t } from '#/locales';

import {
  clearNotifications,
  deleteNotification,
  getNotifications,
  getPushPlusConfig,
  NotificationStatusEnum,
  savePushPlusConfig,
  sendTestNotification,
} from '../../api/quartz/notification';

type SortOrder = 'ascend' | 'descend' | undefined;

// 通知状态映射
const notificationStatusMap = {
  [NotificationStatusEnum.Pending]: {
    text: () => $t('page.quartz.notificationPage.statusPending'),
    status: 'default' as const,
  },
  [NotificationStatusEnum.Sent]: {
    text: () => $t('page.quartz.notificationPage.statusSent'),
    status: 'success' as const,
  },
  [NotificationStatusEnum.Failed]: {
    text: () => $t('page.quartz.notificationPage.statusFailed'),
    status: 'error' as const,
  },
};

// 响应式数据
const loading = ref(false);
const saveLoading = ref(false);
const dataSource = ref<QuartzNotificationDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);

// 统计数据
const stats = computed(() => {
  const items = dataSource.value;
  return {
    total: total.value,
    sent: items.filter((i) => i.status === NotificationStatusEnum.Sent).length,
    failed: items.filter((i) => i.status === NotificationStatusEnum.Failed)
      .length,
    pending: items.filter((i) => i.status === NotificationStatusEnum.Pending)
      .length,
  };
});

// 详情对话框
const detailModalVisible = ref(false);
const currentNotification = ref<null | QuartzNotificationDto>(null);

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
  return ['cp', 'mail', 'webhook', 'wechat'].includes(configForm.channel);
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
const columns = computed<TableColumnsType>(() => [
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
    width: 110,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      const status = notificationStatusMap[record.status];
      return h(Badge, {
        status: status?.status || 'default',
        text:
          status?.text?.() ||
          record.status ||
          $t('page.quartz.notificationPage.unknown'),
      });
    },
  },
  {
    title: $t('page.quartz.notificationPage.sendTime'),
    dataIndex: 'sendTime',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'sendTime' ? sortOrder.value : undefined,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      return record.sendTime ? formatDateTime(record.sendTime) : '-';
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
      return record.createTime ? formatDateTime(record.createTime) : '-';
    },
  },
  {
    title: $t('page.quartz.notificationPage.action'),
    key: 'action',
    width: 100,
    fixed: 'right',
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      return h(Space, { size: 4 }, [
        h(Tooltip, { title: $t('page.quartz.notificationPage.detail') }, () =>
          h(
            Button,
            {
              type: 'link',
              size: 'small',
              onClick: () => handleDetail(record),
            },
            () => $t('page.quartz.notificationPage.detail'),
          ),
        ),
        h(Tooltip, { title: $t('page.quartz.notificationPage.delete') }, () =>
          h(
            Button,
            {
              type: 'link',
              size: 'small',
              danger: true,
              onClick: () => handleDelete(record),
            },
            () => $t('page.quartz.notificationPage.delete'),
          ),
        ),
      ]);
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
    $t('page.quartz.notificationPage.paginationTotal', {
      start: range[0],
      end: range[1],
      total,
    }),
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
    Object.assign(configForm, response);
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
      message.error(
        response.message || $t('page.quartz.notificationPage.saveConfigFailed'),
      );
    }
  } catch (error: any) {
    if (error.errorFields) {
      return;
    }
    const errorMessage =
      error.message || $t('page.quartz.notificationPage.saveConfigFailed');
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
      message.error(
        response.message || $t('page.quartz.notificationPage.testSendFailed'),
      );
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
          message.error(
            response.message || $t('page.quartz.notificationPage.deleteFailed'),
          );
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
          message.error(
            response.message || $t('page.quartz.notificationPage.clearFailed'),
          );
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
                  {{ $t('page.quartz.notificationPage.totalNotifications') }}
                </div>
                <div class="mt-1 text-xl font-bold text-foreground">
                  {{ stats.total }}
                </div>
              </div>
              <div class="stat-mini-icon bg-primary/10 text-primary">
                <span style="font-size: 18px">&#x1F514;</span>
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
                  {{ $t('page.quartz.notificationPage.sentCount') }}
                </div>
                <div class="mt-1 text-xl font-bold text-success">
                  {{ stats.sent }}
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
                  {{ $t('page.quartz.notificationPage.failedCount') }}
                </div>
                <div class="mt-1 text-xl font-bold text-destructive">
                  {{ stats.failed }}
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
                  {{ $t('page.quartz.notificationPage.pendingCount') }}
                </div>
                <div class="mt-1 text-xl font-bold text-warning">
                  {{ stats.pending }}
                </div>
              </div>
              <div class="stat-mini-icon bg-warning/10 text-warning">
                <span style="font-size: 18px">&#x23F3;</span>
              </div>
            </div>
          </Card>
        </Col>
      </Row>

      <!-- 搜索卡片 -->
      <Card class="search-card mb-3" :body-style="{ padding: '16px 20px' }">
        <Form ref="searchFormRef" :model="searchForm" layout="inline">
          <Row :gutter="[16, 12]" class="w-full">
            <Col :xs="24" :sm="12" :md="8" :lg="6" :xl="5">
              <Form.Item
                :label="$t('page.quartz.notificationPage.notificationStatus')"
                name="status"
                class="mb-0"
              >
                <Select
                  v-model:value="searchForm.status"
                  :placeholder="
                    $t('page.quartz.notificationPage.placeholderStatus')
                  "
                  allow-clear
                >
                  <Select.Option :value="NotificationStatusEnum.Pending">
                    {{ $t('page.quartz.notificationPage.statusPending') }}
                  </Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Sent">
                    {{ $t('page.quartz.notificationPage.statusSent') }}
                  </Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Failed">
                    {{ $t('page.quartz.notificationPage.statusFailed') }}
                  </Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="8" :lg="6" :xl="5">
              <Form.Item
                :label="$t('page.quartz.notificationPage.triggeredBy')"
                name="triggeredBy"
                class="mb-0"
              >
                <Input
                  v-model:value="searchForm.triggeredBy"
                  :placeholder="
                    $t('page.quartz.notificationPage.placeholderTriggeredBy')
                  "
                  allow-clear
                />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="8" :lg="12" :xl="14" class="text-right">
              <Form.Item class="mb-0">
                <Space>
                  <Button type="primary" @click="handleSearch">
                    {{ $t('page.quartz.notificationPage.search') }}
                  </Button>
                  <Button @click="handleReset">
                    {{ $t('page.quartz.notificationPage.reset') }}
                  </Button>
                </Space>
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>

      <!-- 通知管理卡片 -->
      <Card :body-style="{ padding: '16px 20px' }">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <Space>
            <Button type="primary" @click="handleOpenConfigModal">
              {{ $t('page.quartz.notificationPage.notificationConfig') }}
            </Button>
            <Button @click="handleSendTest">
              {{ $t('page.quartz.notificationPage.sendTestNotification') }}
            </Button>
          </Space>
          <Button danger @click="handleClearNotifications">
            {{ $t('page.quartz.notificationPage.clearAll') }}
          </Button>
        </div>
        <Table
          :columns="columns"
          :data-source="dataSource"
          :pagination="pagination"
          :loading="loading"
          :row-key="(record) => record.notificationId"
          @change="handleTableChange"
          size="middle"
          :scroll="{ x: 'max-content' }"
        >
          <template #emptyText>
            <Empty
              :description="
                $t('page.quartz.notificationPage.noNotificationData')
              "
            />
          </template>
        </Table>
      </Card>

      <!-- 配置对话框 -->
      <Modal
        v-model:open="configModalVisible"
        :title="$t('page.quartz.notificationPage.notificationConfig')"
        width="720px"
        destroy-on-close
        centered
        @cancel="configModalVisible = false"
      >
        <div class="config-modal-content">
          <Alert
            :message="$t('page.quartz.notificationPage.configPushPlusDesc')"
            type="info"
            show-icon
            class="mb-3"
          />

          <Form
            ref="formRef"
            :model="configForm"
            layout="vertical"
            size="small"
          >
            <!-- 基础配置 -->
            <section class="form-section">
              <div class="section-header">
                <span class="title">{{
                  $t('page.quartz.notificationPage.basicConfig')
                }}</span>
                <div class="header-action">
                  <span class="label">{{
                    $t('page.quartz.notificationPage.serviceEnableStatus')
                  }}</span>
                  <Switch v-model:checked="configForm.enable" size="small" />
                </div>
              </div>

              <Row :gutter="12" align="middle">
                <Col :span="16">
                  <Form.Item
                    label="PushPlus Token"
                    name="token"
                    :rules="[
                      {
                        required: configForm.enable,
                        message: $t(
                          'page.quartz.notificationPage.tokenRequired',
                        ),
                      },
                    ]"
                  >
                    <Input.Password
                      v-model:value="configForm.token"
                      :placeholder="
                        $t('page.quartz.notificationPage.tokenPlaceholder')
                      "
                      autocomplete="off"
                    />
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item
                    :label="$t('page.quartz.notificationPage.topicLabel')"
                    name="topic"
                  >
                    <Input
                      v-model:value="configForm.topic"
                      :placeholder="
                        $t('page.quartz.notificationPage.topicPlaceholder')
                      "
                    />
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item
                    :label="$t('page.quartz.notificationPage.pushChannel')"
                    name="channel"
                  >
                    <Select v-model:value="configForm.channel">
                      <Select.Option value="wechat">
                        {{ $t('page.quartz.notificationPage.channelWechat') }}
                      </Select.Option>
                      <Select.Option value="cp">
                        {{
                          $t('page.quartz.notificationPage.channelWechatWork')
                        }}
                      </Select.Option>
                      <Select.Option value="webhook">
                        {{ $t('page.quartz.notificationPage.channelWebhook') }}
                      </Select.Option>
                      <Select.Option value="mail">
                        {{ $t('page.quartz.notificationPage.channelEmail') }}
                      </Select.Option>
                      <Select.Option value="sms">
                        {{ $t('page.quartz.notificationPage.channelSms') }}
                      </Select.Option>
                      <Select.Option value="voice">
                        {{ $t('page.quartz.notificationPage.channelVoice') }}
                      </Select.Option>
                      <Select.Option value="extension">
                        {{
                          $t('page.quartz.notificationPage.channelExtension')
                        }}
                      </Select.Option>
                      <Select.Option value="app">
                        {{ $t('page.quartz.notificationPage.channelApp') }}
                      </Select.Option>
                    </Select>
                  </Form.Item>
                </Col>
                <Col :span="8">
                  <Form.Item
                    :label="$t('page.quartz.notificationPage.messageTemplate')"
                    name="template"
                  >
                    <Select v-model:value="configForm.template">
                      <Select.Option value="html">
                        {{ $t('page.quartz.notificationPage.templateHtml') }}
                      </Select.Option>
                      <Select.Option value="txt">
                        {{ $t('page.quartz.notificationPage.templateTxt') }}
                      </Select.Option>
                      <Select.Option value="json">
                        {{ $t('page.quartz.notificationPage.templateJson') }}
                      </Select.Option>
                      <Select.Option value="markdown">
                        {{
                          $t('page.quartz.notificationPage.templateMarkdown')
                        }}
                      </Select.Option>
                    </Select>
                  </Form.Item>
                </Col>
                <Col
                  v-if="['webhook', 'cp', 'mail'].includes(configForm.channel)"
                  :span="8"
                >
                  <Form.Item
                    :label="$t('page.quartz.notificationPage.optionLabel')"
                    name="option"
                    :rules="[
                      {
                        required: ['webhook', 'cp'].includes(
                          configForm.channel,
                        ),
                        message: $t(
                          'page.quartz.notificationPage.optionRequired',
                        ),
                      },
                    ]"
                  >
                    <Input
                      v-model:value="configForm.option"
                      :placeholder="optionPlaceholder"
                    />
                  </Form.Item>
                </Col>
                <Col
                  v-if="['wechat', 'cp'].includes(configForm.channel)"
                  :span="8"
                >
                  <Form.Item
                    :label="$t('page.quartz.notificationPage.toLabel')"
                    name="to"
                  >
                    <Input
                      v-model:value="configForm.to"
                      :placeholder="
                        $t('page.quartz.notificationPage.toPlaceholder')
                      "
                    />
                  </Form.Item>
                </Col>
              </Row>

              <Alert
                v-if="showChannelParams"
                :message="channelTipMessage"
                type="warning"
                show-icon
                class="mt-1"
              />
            </section>

            <!-- 通知策略 -->
            <section class="form-section">
              <div class="section-header">
                <span class="title">{{
                  $t('page.quartz.notificationPage.notificationStrategy')
                }}</span>
              </div>

              <div class="strategy-grid">
                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">
                      {{ $t('page.quartz.notificationPage.jobSuccess') }}
                    </div>
                    <div class="desc">
                      {{ $t('page.quartz.notificationPage.jobSuccessDesc') }}
                    </div>
                  </div>
                  <Switch
                    v-model:checked="configForm.strategy.notifyOnJobSuccess"
                  />
                </div>

                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">
                      {{ $t('page.quartz.notificationPage.jobFailure') }}
                    </div>
                    <div class="desc">
                      {{ $t('page.quartz.notificationPage.jobFailureDesc') }}
                    </div>
                  </div>
                  <Switch
                    v-model:checked="configForm.strategy.notifyOnJobFailure"
                  />
                </div>

                <div class="strategy-item">
                  <div class="strategy-info">
                    <div class="name">
                      {{ $t('page.quartz.notificationPage.schedulerError') }}
                    </div>
                    <div class="desc">
                      {{
                        $t('page.quartz.notificationPage.schedulerErrorDesc')
                      }}
                    </div>
                  </div>
                  <Switch
                    v-model:checked="configForm.strategy.notifyOnSchedulerError"
                  />
                </div>
              </div>
            </section>

            <!-- 高级配置 -->
            <div class="advanced-section">
              <div
                class="section-header"
                @click="advancedVisible = !advancedVisible"
              >
                <span class="title">{{
                  $t('page.quartz.notificationPage.advancedConfig')
                }}</span>
                <span class="toggle-icon" :class="{ expanded: advancedVisible }"
                  >&#x203A;</span
                >
              </div>
              <div v-show="advancedVisible" class="advanced-body">
                <Row :gutter="12">
                  <Col :span="16">
                    <Form.Item
                      :label="
                        $t('page.quartz.notificationPage.callbackUrlLabel')
                      "
                      name="callbackUrl"
                    >
                      <Input
                        v-model:value="configForm.callbackUrl"
                        :placeholder="
                          $t(
                            'page.quartz.notificationPage.callbackUrlPlaceholder',
                          )
                        "
                      />
                    </Form.Item>
                  </Col>
                  <Col :span="8">
                    <Form.Item
                      :label="$t('page.quartz.notificationPage.timestampLabel')"
                      name="timestamp"
                    >
                      <InputNumber
                        v-model:value="configForm.timestamp"
                        :placeholder="
                          $t(
                            'page.quartz.notificationPage.timestampPlaceholder',
                          )
                        "
                        :precision="0"
                        :min="0"
                        style="width: 100%"
                      />
                    </Form.Item>
                  </Col>
                </Row>
              </div>
            </div>
          </Form>
        </div>

        <template #footer>
          <div class="flex justify-end gap-3">
            <Button @click="configModalVisible = false">
              {{ $t('page.quartz.notificationPage.cancel') }}
            </Button>
            <Button
              type="primary"
              :loading="saveLoading"
              @click="handleSaveConfig"
            >
              {{ $t('page.quartz.notificationPage.saveConfig') }}
            </Button>
          </div>
        </template>
      </Modal>

      <!-- 详情对话框 -->
      <Modal
        v-model:open="detailModalVisible"
        :title="$t('page.quartz.notificationPage.notificationDetail')"
        width="800px"
        :footer="null"
        :destroy-on-close="true"
        centered
      >
        <div v-if="currentNotification" class="notification-detail">
          <!-- 头部信息 -->
          <div class="detail-header mb-4">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <Typography.Text strong class="text-base">
                {{ currentNotification.title }}
              </Typography.Text>
              <Badge
                :status="
                  notificationStatusMap[currentNotification.status]?.status ||
                  'default'
                "
                :text="
                  notificationStatusMap[currentNotification.status]?.text?.() ||
                  $t('page.quartz.notificationPage.unknown')
                "
              />
            </div>

            <div
              class="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-4"
            >
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.notificationPage.triggerSource')
                }}</span>
                <span class="info-value">{{
                  currentNotification.triggeredBy || '-'
                }}</span>
              </div>
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.notificationPage.sendDateTime')
                }}</span>
                <span class="info-value">{{
                  currentNotification.sendTime
                    ? formatDateTime(currentNotification.sendTime)
                    : '-'
                }}</span>
              </div>
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.notificationPage.sendDuration')
                }}</span>
                <span class="info-value font-mono">{{
                  currentNotification.duration
                    ? `${currentNotification.duration} ms`
                    : '0 ms'
                }}</span>
              </div>
              <div class="info-item">
                <span class="info-label">{{
                  $t('page.quartz.notificationPage.createDateTime')
                }}</span>
                <span class="info-value">{{
                  formatDateTime(currentNotification.createTime)
                }}</span>
              </div>
            </div>
          </div>

          <!-- 内容区域 -->
          <div class="detail-content space-y-4">
            <div class="content-section">
              <Typography.Text strong>
                {{ $t('page.quartz.notificationPage.notificationContent') }}
              </Typography.Text>
              <div class="content-card info-card mt-2">
                <div
                  class="content-html"
                  v-html="currentNotification.content"
                ></div>
              </div>
            </div>

            <div
              v-if="currentNotification.errorMessage"
              class="content-section"
            >
              <Typography.Text strong class="text-destructive">
                {{ $t('page.quartz.notificationPage.errorInfo') }}
              </Typography.Text>
              <div class="content-card error-card mt-2">
                <pre class="code-block">{{
                  currentNotification.errorMessage
                }}</pre>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-5 flex justify-end">
          <Button @click="detailModalVisible = false" type="primary">
            {{ $t('page.quartz.notificationPage.close') }}
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

/* 内容卡片 */
.content-card {
  border-radius: 8px;
  border: 1px solid hsl(var(--border));
  background: hsl(var(--card));
  transition: box-shadow 0.2s;
  overflow: hidden;
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

.info-card {
  background: hsl(var(--primary) / 0.03);
  border-color: hsl(var(--primary) / 0.15);
}

.content-html {
  padding: 12px;
  font-size: 13px;
  line-height: 1.6;
  color: hsl(var(--foreground));
  word-break: break-word;
}

.content-html :deep(img) {
  max-width: 100%;
}

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
    background: hsl(var(--muted) / 0.5);
    border-radius: 8px;
    margin-bottom: 12px;
    border: 1px solid hsl(var(--border));

    .section-header {
      display: flex;
      align-items: center;
      margin-bottom: 10px;
      padding-bottom: 8px;
      border-bottom: 1px solid hsl(var(--border));

      .title {
        font-size: 14px;
        font-weight: 600;
        flex: 1;
        color: hsl(var(--foreground));
      }

      .header-action {
        display: flex;
        align-items: center;
        gap: 8px;

        .label {
          font-size: 12px;
          color: hsl(var(--muted-foreground));
        }
      }
    }
  }

  .advanced-section {
    margin-top: 12px;
    background: hsl(var(--muted) / 0.5);
    border-radius: 8px;
    border: 1px solid hsl(var(--border));
    overflow: hidden;

    .section-header {
      display: flex;
      align-items: center;
      padding: 8px 12px;
      cursor: pointer;
      user-select: none;
      transition: background 0.2s;

      &:hover {
        background: hsl(var(--accent));
      }

      .title {
        font-size: 13px;
        font-weight: 600;
        color: hsl(var(--muted-foreground));
      }

      .toggle-icon {
        margin-left: 6px;
        font-size: 16px;
        color: hsl(var(--muted-foreground));
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
      border-top: 1px solid hsl(var(--border));
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
      background: hsl(var(--card));
      border: 1px solid hsl(var(--border));
      border-radius: 6px;
      transition: all 0.2s ease;

      &:hover {
        border-color: hsl(var(--primary) / 0.3);
      }

      .strategy-info {
        .name {
          font-size: 13px;
          font-weight: 500;
          color: hsl(var(--foreground));
        }

        .desc {
          font-size: 11px;
          color: hsl(var(--muted-foreground));
          margin-top: 1px;
        }
      }
    }
  }
}

@media (max-width: 640px) {
  .strategy-grid {
    grid-template-columns: 1fr !important;
  }
}
</style>
