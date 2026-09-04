<script setup lang="ts">
import { ref, computed, reactive, onMounted, nextTick, watch } from 'vue';
import { formatDateTime } from '@vben/utils';
import { Page } from '@vben/common-ui';
// 导入 vbenadmin 的 Vxe Table 适配器
import { useVbenVxeGrid } from '@vben/plugins/vxe-table';
import type { VxeTableGridOptions } from '@vben/plugins/vxe-table';
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
  Row,
  Col,
  Tooltip,
  InputNumber,
  Alert,
  Descriptions,
  DescriptionsItem,
} from 'ant-design-vue';
import type { FormInstance } from 'ant-design-vue';

// 导入i18n
import { $t } from '#/locales';
import { useI18n } from '@vben/locales';

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
// 导入可拖动 Modal 组合式函数
import { useDraggableModal } from './composables/use-draggable-modal';

const { locale } = useI18n();

// 通知状态映射
const notificationStatusMap = {
  [NotificationStatusEnum.Pending]: { text: () => $t('page.quartz.notificationPage.statusPending'), status: 'default' },
  [NotificationStatusEnum.Sent]: { text: () => $t('page.quartz.notificationPage.statusSent'), status: 'success' },
  [NotificationStatusEnum.Failed]: { text: () => $t('page.quartz.notificationPage.statusFailed'), status: 'error' },
};

// 响应式数据
const loading = ref(false);
const saveLoading = ref(false);

// 详情对话框
const detailModalVisible = ref(false);
const currentNotification = ref<QuartzNotificationDto | null>(null);

// 发送耗时格式化：根据毫秒数自动选择合适单位（ms/s/min/h）
const formatDuration = (ms?: number | null) => {
  if (ms == null) return '—';
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${parseFloat((ms / 1000).toFixed(2))} s`;
  if (ms < 3_600_000) return `${parseFloat((ms / 60_000).toFixed(2))} min`;
  return `${parseFloat((ms / 3_600_000).toFixed(2))} h`;
};

// 搜索条件由 VbenForm 自动注入到 query 的 formValues

// 详情顶部状态条颜色：已发送绿 / 失败红 / 待发送琥珀
const notificationStatusColor = computed(() => {
  const status = currentNotification.value?.status;
  if (status === NotificationStatusEnum.Sent) return '#52c41a';
  if (status === NotificationStatusEnum.Failed) return '#ff4d4f';
  return '#faad14';
});

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
  { type: 'seq', width: 60, title: '#', fixed: 'left' },
  {
    field: 'title',
    title: $t('page.quartz.notificationPage.title'),
    minWidth: 200,
    showOverflow: true,
  },
  {
    field: 'triggeredBy',
    title: $t('page.quartz.notificationPage.triggeredBy'),
    minWidth: 120,
    showOverflow: true,
  },
  {
    field: 'status',
    title: $t('page.quartz.notificationPage.status'),
    width: 100,
    align: 'center' as const,
    slots: { default: 'status' },
  },
  {
    field: 'sendTime',
    title: $t('page.quartz.notificationPage.sendTime'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'duration',
    title: $t('page.quartz.notificationPage.duration'),
    width: 110,
    align: 'right' as const,
    sortable: true,
    slots: { default: 'duration' },
  },
  {
    field: 'createTime',
    title: $t('page.quartz.notificationPage.createTime'),
    width: 170,
    align: 'center' as const,
    sortable: true,
    slots: { default: 'datetime' },
  },
  {
    field: 'action',
    title: $t('page.quartz.notificationPage.action'),
    width: 90,
    align: 'center' as const,
    fixed: 'right',
    slots: { default: 'action' },
  },
]);

// 排序持久化：读取上次排序列
const SORT_KEY = 'quartz-notification-sort';
// 搜索条件持久化 key（仅保存表单输入值，不含分页/排序）
const SEARCH_KEY = 'quartz-notification-search';
const savedSort = (() => {
  try {
    const raw = localStorage.getItem(SORT_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();
const savedSearch = (() => {
  try {
    const raw = localStorage.getItem(SEARCH_KEY);
    return raw ? JSON.parse(raw) : undefined;
  } catch {
    return undefined;
  }
})();

// 构造 Vxe Grid 配置
const gridOptions: VxeTableGridOptions<QuartzNotificationDto> = {
  id: 'quartz-notification-grid',
  columns: columns.value as any,
  height: 'auto',
  showOverflow: true,
  rowConfig: { keyField: 'notificationId', isHover: true },
  sortConfig: {
    trigger: 'cell',
    remote: true,
    defaultSort: savedSort,
  },
  customConfig: { storage: true },
  columnConfig: { resizable: true },
  pagerConfig: { enabled: true },
  proxyConfig: {
    enabled: true,
    autoLoad: false,
    ajax: {
      query: async ({ page, sort }: any, formValues: any) => {
        // autoLoad 首次 query 时 defaultSort 可能未注入，从 localStorage 兜底
        let sortField = sort?.field;
        let sortOrderRaw = sort?.order;
        if (!sortField) {
          try {
            const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
            if (saved) {
              sortField = saved.field;
              sortOrderRaw = saved.order;
            }
          } catch {}
        }
        // 保持原有行为：sortOrder 使用 ascend/descend 形式
        const sortOrder =
          sortOrderRaw === 'asc' ? 'ascend' : sortOrderRaw === 'desc' ? 'descend' : undefined;
        // 主动从 formApi 获取表单值（避开 vxe-table reload 路径下 wrapper 注入 formValues 为空的问题）
        let currentValues: any = formValues || {};
        try {
          const formApiValues = await gridApi.formApi.getValues();
          if (formApiValues && Object.keys(formApiValues).length > 0) {
            currentValues = formApiValues;
          }
        } catch {}
        const params: NotificationQueryDto = {
          status: currentValues?.status,
          triggeredBy: currentValues?.triggeredBy,
          pageIndex: page.currentPage || 1,
          pageSize: page.pageSize || 20,
          sortBy: sortField ?? '',
          sortOrder,
        };
        // 持久化搜索条件（仅保存非空值，避免 localStorage 膨胀）
        try {
          const persisted: Record<string, any> = {};
          for (const k of ['status', 'triggeredBy']) {
            if (currentValues[k] != null && currentValues[k] !== '') {
              persisted[k] = currentValues[k];
            }
          }
          localStorage.setItem(SEARCH_KEY, JSON.stringify(persisted));
        } catch {}

        try {
          const response = await getNotifications(params);
          if (response.success) {
            if (
              response.data &&
              response.data.items &&
              Array.isArray(response.data.items)
            ) {
              return {
                result: response.data.items,
                page: {
                  total: response.data.totalCount || 0,
                },
              };
            }
            return { result: [], page: { total: 0 } };
          }
          message.error(response.message || $t('page.quartz.notificationPage.loadListFailed'));
          return { result: [], page: { total: 0 } };
        } catch (error) {
          console.error($t('page.quartz.notificationPage.loadListFailed'), error);
          message.error(
            typeof error === 'object' && error !== null && 'message' in error
              ? String((error as any).message)
              : $t('page.quartz.notificationPage.loadListFailed'),
          );
          return { result: [], page: { total: 0 } };
        }
      },
    },
    sort: true,
  },
  toolbarConfig: {
    custom: true,
    refresh: true,
    zoom: true,
  },
};

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions,
  formOptions: {
    schema: [
      {
        component: 'Select',
        componentProps: {
          allowClear: true,
          placeholder: $t('page.quartz.notificationPage.placeholderStatus'),
          options: [
            { label: $t('page.quartz.notificationPage.statusPending'), value: NotificationStatusEnum.Pending },
            { label: $t('page.quartz.notificationPage.statusSent'), value: NotificationStatusEnum.Sent },
            { label: $t('page.quartz.notificationPage.statusFailed'), value: NotificationStatusEnum.Failed },
          ],
        },
        fieldName: 'status',
        label: $t('page.quartz.notificationPage.notificationStatus'),
      },
      {
        component: 'Input',
        componentProps: { placeholder: $t('page.quartz.notificationPage.placeholderTriggeredBy') },
        fieldName: 'triggeredBy',
        label: $t('page.quartz.notificationPage.triggeredBy'),
      },
    ],
    showCollapseButton: false,
    submitOnChange: false,
    submitOnEnter: true,
  },
  gridEvents: {
    sortChange: ({ property, field, order }: any) => {
      const sortField = property || field;
      if (sortField && order) {
        localStorage.setItem(
          SORT_KEY,
          JSON.stringify({ field: sortField, order }),
        );
      } else {
        localStorage.removeItem(SORT_KEY);
      }
    },
  },
});

// 对话框支持拖动
useDraggableModal(configModalVisible, 'quartz-notification-config-modal');
useDraggableModal(detailModalVisible, 'quartz-notification-detail-modal');

// 监听语言切换，更新表格列头和搜索表单
watch(locale, () => {
  gridApi.setGridOptions({ columns: columns.value as any });
  gridApi.formApi.updateSchema([
    {
      fieldName: 'status',
      label: $t('page.quartz.notificationPage.notificationStatus'),
      componentProps: {
        allowClear: true,
        placeholder: $t('page.quartz.notificationPage.placeholderStatus'),
        options: [
          { label: $t('page.quartz.notificationPage.statusPending'), value: NotificationStatusEnum.Pending },
          { label: $t('page.quartz.notificationPage.statusSent'), value: NotificationStatusEnum.Sent },
          { label: $t('page.quartz.notificationPage.statusFailed'), value: NotificationStatusEnum.Failed },
        ],
      },
    },
    {
      fieldName: 'triggeredBy',
      label: $t('page.quartz.notificationPage.triggeredBy'),
      componentProps: { placeholder: $t('page.quartz.notificationPage.placeholderTriggeredBy') },
    },
  ]);
});

// 搜索/重置由 VbenForm 内置提交按钮触发，无需手动处理

// 打开配置对话框
const handleOpenConfigModal = async () => {
  try {
    const response = await getPushPlusConfig() as any;
    Object.assign(configForm, response.data || response);
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
      gridApi.query();
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
          gridApi.query();
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
        const formValues = await gridApi.formApi.getValues();
        const response = await clearNotifications({
          pageIndex: 1,
          pageSize: 1,
          status: formValues?.status,
          triggeredBy: formValues?.triggeredBy,
        });
        if (response.success) {
          message.success($t('page.quartz.notificationPage.clearSuccess'));
          gridApi.query();
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

// 恢复表格排序视觉状态（列头箭头）
onMounted(async () => {
  // 恢复搜索条件到表单
  if (savedSearch) {
    try {
      await gridApi.formApi.setValues(savedSearch);
    } catch {}
  }
  // 手动触发首次查询（autoLoad: false，此时 formApi 已回填搜索条件）
  await gridApi.query();
  // 数据加载后恢复排序视觉状态
  await nextTick();
  try {
    const saved = JSON.parse(localStorage.getItem(SORT_KEY) || 'null');
    if (saved) {
      gridApi.grid?.setSort({ field: saved.field, order: saved.order });
    }
  } catch {}
});
</script>

<template>
  <Page auto-content-height>
    <template #default>
      <!-- 通知列表 -->
      <Grid>
        <!-- 工具栏：配置/测试/清空按钮 -->
        <template #toolbar-actions>
          <div class="flex w-full items-center justify-between">
            <Space>
              <Button type="primary" @click="handleOpenConfigModal">{{ $t('page.quartz.notificationPage.notificationConfig') }}</Button>
              <Button type="default" :loading="loading" @click="handleSendTest">{{ $t('page.quartz.notificationPage.sendTestNotification') }}</Button>
            </Space>
            <Button danger @click="handleClearNotifications">{{ $t('page.quartz.notificationPage.clearAll') }}</Button>
          </div>
        </template>

        <!-- 通知状态 -->
        <template #status="{ row }">
          <Tag :color="notificationStatusMap[row.status as NotificationStatusEnum]?.status || 'default'">
            {{ notificationStatusMap[row.status as NotificationStatusEnum]?.text?.() || $t('page.quartz.notificationPage.unknown') }}
          </Tag>
        </template>

        <!-- 通用日期时间渲染 -->
        <template #datetime="{ row, column }">
          {{ (row as any)[column.field] ? formatDateTime((row as any)[column.field]) : '-' }}
        </template>

        <!-- 发送时长 -->
        <template #duration="{ row }">
          {{ row.duration != null ? formatDuration(row.duration) : '-' }}
        </template>

        <!-- 操作列 -->
        <template #action="{ row }">
          <div class="flex items-center justify-center gap-3">
            <Tooltip :title="$t('page.quartz.notificationPage.detail')">
              <i class="vxe-icon-info-circle-fill text-primary cursor-pointer hover:opacity-80" @click="handleDetail(row)"></i>
            </Tooltip>
            <Tooltip :title="$t('page.quartz.notificationPage.delete')">
              <i class="vxe-icon-delete cursor-pointer hover:opacity-80" style="color: var(--ant-color-error)" @click="handleDelete(row)"></i>
            </Tooltip>
          </div>
        </template>
      </Grid>

      <!-- 配置对话框 -->
      <Modal v-model:open="configModalVisible" :title="$t('page.quartz.notificationPage.notificationConfig')" width="800px"
        :body-style="{ padding: '24px' }" destroyOnClose
        @cancel="configModalVisible = false" centered wrapClassName="quartz-notification-config-modal">
        <Alert :message="$t('page.quartz.notificationPage.configPushPlusDesc')" type="info" show-icon class="config-tip-alert" />

        <Form ref="formRef" :model="configForm" layout="vertical" class="config-form">
          <!-- 基础配置 -->
          <div class="form-section-title">
            <span>{{ $t('page.quartz.notificationPage.basicConfig') }}</span>
            <span class="section-title-action">
              <span class="enable-label">{{ $t('page.quartz.notificationPage.serviceEnableStatus') }}</span>
              <Switch v-model:checked="configForm.enable" size="small" />
            </span>
          </div>
          <Row :gutter="16">
            <Col :xs="24" :md="16">
              <Form.Item label="PushPlus Token" name="token"
                :rules="[{ required: configForm.enable, message: $t('page.quartz.notificationPage.tokenRequired') }]">
                <Input.Password v-model:value="configForm.token" :placeholder="$t('page.quartz.notificationPage.tokenPlaceholder')" autocomplete="off" />
              </Form.Item>
            </Col>
            <Col :xs="24" :md="8">
              <Form.Item :label="$t('page.quartz.notificationPage.topicLabel')" name="topic">
                <Input v-model:value="configForm.topic" :placeholder="$t('page.quartz.notificationPage.topicPlaceholder')" />
              </Form.Item>
            </Col>
            <Col :xs="24" :md="8">
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
            <Col :xs="24" :md="8">
              <Form.Item :label="$t('page.quartz.notificationPage.messageTemplate')" name="template">
                <Select v-model:value="configForm.template">
                  <Select.Option value="html">{{ $t('page.quartz.notificationPage.templateHtml') }}</Select.Option>
                  <Select.Option value="txt">{{ $t('page.quartz.notificationPage.templateTxt') }}</Select.Option>
                  <Select.Option value="json">{{ $t('page.quartz.notificationPage.templateJson') }}</Select.Option>
                  <Select.Option value="markdown">{{ $t('page.quartz.notificationPage.templateMarkdown') }}</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col v-if="['webhook', 'cp', 'mail'].includes(configForm.channel)" :xs="24" :md="8">
              <Form.Item :label="$t('page.quartz.notificationPage.optionLabel')" name="option"
                :rules="[{ required: ['webhook', 'cp'].includes(configForm.channel), message: $t('page.quartz.notificationPage.optionRequired') }]">
                <Input v-model:value="configForm.option" :placeholder="optionPlaceholder" />
              </Form.Item>
            </Col>
            <Col v-if="['wechat', 'cp'].includes(configForm.channel)" :xs="24" :md="8">
              <Form.Item :label="$t('page.quartz.notificationPage.toLabel')" name="to">
                <Input v-model:value="configForm.to" :placeholder="$t('page.quartz.notificationPage.toPlaceholder')" />
              </Form.Item>
            </Col>
          </Row>

          <Alert v-if="showChannelParams" :message="channelTipMessage" type="warning" show-icon class="channel-tip" />

          <!-- 通知策略 -->
          <div class="form-section-title">{{ $t('page.quartz.notificationPage.notificationStrategy') }}</div>
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

          <!-- 高级配置 -->
          <div class="form-section-title advanced-toggle" @click="advancedVisible = !advancedVisible">
            {{ $t('page.quartz.notificationPage.advancedConfig') }}
            <span class="toggle-icon" :class="{ expanded: advancedVisible }">›</span>
          </div>
          <div v-show="advancedVisible" class="advanced-body">
            <Row :gutter="16">
              <Col :xs="24" :md="16">
                <Form.Item :label="$t('page.quartz.notificationPage.callbackUrlLabel')" name="callbackUrl">
                  <Input v-model:value="configForm.callbackUrl" :placeholder="$t('page.quartz.notificationPage.callbackUrlPlaceholder')" />
                </Form.Item>
              </Col>
              <Col :xs="24" :md="8">
                <Form.Item :label="$t('page.quartz.notificationPage.timestampLabel')" name="timestamp">
                  <InputNumber v-model:value="configForm.timestamp" :placeholder="$t('page.quartz.notificationPage.timestampPlaceholder')"
                    :precision="0" :min="0" style="width: 100%" />
                </Form.Item>
              </Col>
            </Row>
          </div>
        </Form>

        <template #footer>
          <Space>
            <Button @click="configModalVisible = false">{{ $t('page.quartz.notificationPage.cancel') }}</Button>
            <Button type="primary" :loading="saveLoading" @click="handleSaveConfig">{{ $t('page.quartz.notificationPage.saveConfig') }}</Button>
          </Space>
        </template>
      </Modal>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="$t('page.quartz.notificationPage.notificationDetail')" width="800px"
        :footer="null" :destroyOnClose="true" centered wrapClassName="quartz-notification-detail-modal">
        <div v-if="currentNotification" class="notification-detail">
          <!-- 顶部：标题 + 状态标签 -->
          <div class="detail-header">
            <span class="header-title">{{ currentNotification.title }}</span>
            <Tag :color="notificationStatusMap[currentNotification.status].status">
              {{ notificationStatusMap[currentNotification.status].text() }}
            </Tag>
          </div>

          <!-- 元数据：Descriptions 组件统一展示 -->
          <Descriptions :column="2" size="small" bordered class="detail-desc">
            <DescriptionsItem :label="$t('page.quartz.notificationPage.triggeredBy')">
              {{ currentNotification.triggeredBy || '—' }}
            </DescriptionsItem>
            <DescriptionsItem :label="$t('page.quartz.notificationPage.duration')">
              {{ formatDuration(currentNotification.duration) }}
            </DescriptionsItem>
            <DescriptionsItem :label="$t('page.quartz.notificationPage.sendTime')">
              {{ currentNotification.sendTime ? formatDateTime(currentNotification.sendTime) : '—' }}
            </DescriptionsItem>
            <DescriptionsItem :label="$t('page.quartz.notificationPage.createTime')">
              {{ formatDateTime(currentNotification.createTime) }}
            </DescriptionsItem>
          </Descriptions>

          <!-- 内容区 -->
          <div class="detail-body">
            <section class="detail-section">
              <div class="section-title">{{ $t('page.quartz.notificationPage.notificationContent') }}</div>
              <div class="content-panel" v-html="currentNotification.content"></div>
            </section>

            <section v-if="currentNotification.errorMessage" class="detail-section">
              <div class="section-title">
                {{ $t('page.quartz.notificationPage.errorInfo') }}
                <span class="section-tag section-tag--error">Error</span>
              </div>
              <pre class="code-panel code-panel--error">{{ currentNotification.errorMessage }}</pre>
            </section>
          </div>

          <!-- 底部按钮 -->
          <div class="detail-footer">
            <Button @click="detailModalVisible = false" type="primary">
              {{ $t('page.quartz.notificationPage.close') }}
            </Button>
          </div>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
/* ============ 详情对话框 ============ */
.notification-detail {
  --space-lg: 20px;
}

/* 顶部：标题 + 状态标签 */
.detail-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: var(--space-lg);
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: hsl(var(--foreground));
  line-height: 1.4;
  word-break: break-word;
}

/* Descriptions 元数据 */
.detail-desc {
  margin-bottom: var(--space-lg);
}

/* 内容区 */
.detail-body {
  display: flex;
  flex-direction: column;
  gap: var(--space-lg);
}

.detail-section {
  min-width: 0;
}

/* section 标题 */
.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin-bottom: 8px;
}

.section-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 1px 7px;
  border-radius: 3px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  line-height: 1.5;
}

.section-tag--error {
  background: rgba(255, 77, 79, 0.1);
  color: #ff4d4f;
}

/* 通知内容：富文本面板 */
.content-panel {
  padding: 14px 16px;
  background: hsl(var(--accent) / 0.5);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  color: hsl(var(--foreground));
  font-size: 14px;
  line-height: 1.7;
  word-break: break-word;
  overflow-x: auto;
  max-height: 420px;
  overflow-y: auto;
}

.content-panel :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 4px;
}

.content-panel :deep(a) {
  color: hsl(var(--primary));
}

.content-panel :deep(table) {
  max-width: 100%;
  border-collapse: collapse;
}

/* 代码面板 */
.code-panel {
  margin: 0;
  padding: 12px 14px;
  background: hsl(var(--accent) / 0.5);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  color: hsl(var(--foreground));
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12.5px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow-x: auto;
  max-height: 360px;
  overflow-y: auto;
}

.code-panel--error {
  border-left: 3px solid #ff4d4f;
  background: rgba(255, 77, 79, 0.04);
}

/* 底部按钮 */
.detail-footer {
  margin-top: var(--space-lg);
  display: flex;
  justify-content: flex-end;
}
</style>

<style scoped lang="less">
.config-form {
  :deep(.ant-form-item) {
    margin-bottom: 16px;
  }

  :deep(.ant-form-item-label) {
    padding-bottom: 4px;
  }

  :deep(.ant-form-item-label > label) {
    font-size: 13px;
  }
}

.form-section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin: 4px 0 14px;
  padding-left: 8px;
  border-left: 3px solid hsl(var(--primary));
  line-height: 1;
}

.form-section-title:not(:first-child) {
  margin-top: 8px;
}

.section-title-action {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 400;

  .enable-label {
    font-size: 12px;
    color: hsl(var(--muted-foreground));
  }
}

.config-tip-alert {
  margin-bottom: 16px;
}

.channel-tip {
  border-radius: 6px;
  margin-top: 4px;
  margin-bottom: 4px;
}

.advanced-toggle {
  cursor: pointer;
  user-select: none;
  color: hsl(var(--muted-foreground));

  .toggle-icon {
    margin-left: 6px;
    font-size: 14px;
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
  padding-top: 4px;
}

.strategy-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;

  .strategy-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 16px;
    background: hsl(var(--accent) / 0.5);
    border-radius: 6px;

    .strategy-info {
      .name {
        font-size: 13px;
        font-weight: 500;
        color: hsl(var(--foreground));
      }

      .desc {
        font-size: 12px;
        color: hsl(var(--muted-foreground));
        margin-top: 3px;
      }
    }
  }
}
</style>