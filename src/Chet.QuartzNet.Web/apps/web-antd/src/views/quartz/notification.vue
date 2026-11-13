<script setup lang="ts">
import {
  ref,
  computed,
  reactive,
  onActivated,
  onMounted,
  nextTick,
  watch,
} from 'vue';
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
  Popover,
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
import { Check, Eye } from '@vben/icons';
import dayjs from 'dayjs';

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
// 导入表头排序标识恢复逻辑
import { applyGridSortIndicator } from './composables/grid-sort-indicator';

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

// 消息模板格式卡片（每种格式对应后端一种内容生成方式）
const templateOptions = computed(() => [
  {
    value: 'html',
    label: $t('page.quartz.notificationPage.templateHtml'),
    desc: $t('page.quartz.notificationPage.templateHtmlDesc'),
    glyph: '</>',
    color: '#e5533d',
    tint: 'rgba(229, 83, 61, 0.12)',
  },
  {
    value: 'markdown',
    label: $t('page.quartz.notificationPage.templateMarkdown'),
    desc: $t('page.quartz.notificationPage.templateMarkdownDesc'),
    glyph: 'MD',
    color: '#1f6feb',
    tint: 'rgba(31, 111, 235, 0.12)',
  },
  {
    value: 'txt',
    label: $t('page.quartz.notificationPage.templateTxt'),
    desc: $t('page.quartz.notificationPage.templateTxtDesc'),
    glyph: 'TXT',
    color: '#7a5af8',
    tint: 'rgba(122, 90, 248, 0.12)',
  },
  {
    value: 'json',
    label: $t('page.quartz.notificationPage.templateJson'),
    desc: $t('page.quartz.notificationPage.templateJsonDesc'),
    glyph: '{}',
    color: '#0f9d58',
    tint: 'rgba(15, 157, 88, 0.12)',
  },
]);

// 当前选中的模板格式名称
const currentTemplateLabel = computed(
  () =>
    templateOptions.value.find(
      (item) => item.value === (configForm.template || '').toLowerCase(),
    )?.label ?? configForm.template,
);

// 预览使用的格式（后端对未识别格式回退为 HTML，这里保持一致）
const previewMode = computed(() => {
  const mode = (configForm.template || 'html').toLowerCase();
  return ['html', 'markdown', 'txt', 'json'].includes(mode) ? mode : 'html';
});

// 预览示例数据（打开配置时刷新触发时间）
const previewSample = reactive({
  jobName: 'SampleJob',
  triggerTime: dayjs().format('YYYY-MM-DD HH:mm:ss'),
});

// 纯文本模板预览
const previewText = computed(() =>
  [
    `${$t('page.quartz.notificationPage.previewFieldJob')}: ${previewSample.jobName}`,
    `${$t('page.quartz.notificationPage.previewFieldStatus')}: ${$t('page.quartz.notificationPage.previewStatusSuccess')}`,
    `${$t('page.quartz.notificationPage.previewFieldTime')}: ${previewSample.triggerTime}`,
    `${$t('page.quartz.notificationPage.previewFieldDuration')}: 1.28 s`,
    '',
    $t('page.quartz.notificationPage.previewFooter'),
  ].join('\n'),
);

// JSON 模板预览（与后端 GenerateJobExecutionJsonContent 的字段保持一致）
const previewJson = computed(() =>
  JSON.stringify(
    {
      eventType: 'jobExecution',
      title: `${$t('page.quartz.notificationPage.previewStatusSuccess')}: ${previewSample.jobName}`,
      jobName: previewSample.jobName,
      jobGroup: 'DEFAULT',
      status: 'success',
      statusText: $t('page.quartz.notificationPage.previewStatusSuccess'),
      message: 'Job executed successfully',
      executionTime: previewSample.triggerTime,
      duration: 1280,
      source: 'Chet.QuartzNET.UI',
    },
    null,
    2,
  ),
);

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
// 默认排序：与后端缺省排序（创建时间倒序）保持一致，保证首次进入也有排序标识
const DEFAULT_SORT = { field: 'createTime', order: 'desc' as const };
const initialSort = savedSort ?? DEFAULT_SORT;
// 当前生效的排序：点击表头后会同步更新，搜索/刷新后据此恢复列头标识与查询参数
const currentSort = { field: initialSort.field, order: initialSort.order };
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
    defaultSort: initialSort,
  },
  customConfig: { storage: true },
  columnConfig: { resizable: true },
  pagerConfig: { enabled: true },
  proxyConfig: {
    enabled: true,
    autoLoad: false,
    ajax: {
      query: async ({ page, sort }: any, formValues: any) => {
        // 代理 reload（搜索/刷新）会清空列头排序，回退到当前生效的排序
        const sortField = sort?.field ?? currentSort.field;
        const sortOrderRaw = sort?.order ?? currentSort.order;
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
      // 搜索/刷新走的是代理 reload，vxe 在 reload 前会 clearSort 清空列头排序状态，
      // 数据仍按 initialSort 排序，这里把列头标识补回来，避免标识与数据不一致
      querySuccess: () => {
        restoreSortIndicator();
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
    commonConfig: {
      // 关闭浏览器对搜索框的自动填充/历史建议
      componentProps: { autocomplete: 'off' },
    },
    showCollapseButton: false,
    submitOnChange: false,
    submitOnEnter: true,
  },
  gridEvents: {
    sortChange: ({ property, field, order }: any) => {
      const sortField = property || field;
      if (sortField && order) {
        currentSort.field = sortField;
        currentSort.order = order;
        localStorage.setItem(
          SORT_KEY,
          JSON.stringify({ field: sortField, order }),
        );
      } else {
        // 取消排序后回退到默认排序，保证列头标识与查询参数始终一致
        currentSort.field = DEFAULT_SORT.field;
        currentSort.order = DEFAULT_SORT.order;
        localStorage.removeItem(SORT_KEY);
      }
    },
  },
});

// 对话框支持拖动
useDraggableModal(configModalVisible, 'quartz-notification-config-modal');
useDraggableModal(detailModalVisible, 'quartz-notification-detail-modal');

// 恢复表头排序标识：仅在当前排序与期望不一致时设置，避免触发多余查询
const restoreSortIndicator = () => applyGridSortIndicator(gridApi, currentSort);

// 页面被 keep-alive 缓存后重新激活时，表头排序标识会丢失，需要重新同步
let sortIndicatorActivated = false;
onActivated(async () => {
  if (!sortIndicatorActivated) {
    // 首次激活与 onMounted 中的恢复重叠，跳过
    sortIndicatorActivated = true;
    return;
  }
  await nextTick();
  await restoreSortIndicator();
});

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
    // 兼容历史配置：模板格式与通知策略缺失时回退为默认值，避免卡片无选中态
    configForm.template = (configForm.template || 'html').toLowerCase();
    if (!configForm.strategy) {
      configForm.strategy = {
        notifyOnJobSuccess: false,
        notifyOnJobFailure: true,
        notifyOnSchedulerError: true,
      };
    }
    // 预览示例的触发时间每次打开时刷新
    previewSample.triggerTime = dayjs().format('YYYY-MM-DD HH:mm:ss');
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
  // defaultSort 未生效时兜底恢复排序标识
  await restoreSortIndicator();
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
      <Modal v-model:open="configModalVisible" :title="$t('page.quartz.notificationPage.notificationConfig')" width="900px"
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

          <!-- 消息模板 -->
          <div class="form-section-title">
            <span>{{ $t('page.quartz.notificationPage.messageTemplate') }}</span>
            <span class="section-title-action">
              <Popover trigger="hover" placement="rightTop" overlay-class-name="template-preview-popover"
                :overlay-inner-style="{ padding: 0, background: 'transparent', boxShadow: 'none' }"
                :mouse-enter-delay="0.15" :mouse-leave-delay="0.1">
                <template #content>
                  <div class="template-preview">
                    <div class="preview-bar">
                      <span class="preview-dots"><i></i><i></i><i></i></span>
                      <span class="preview-bar__title">{{ $t('page.quartz.notificationPage.templatePreview') }}</span>
                      <span class="preview-bar__tag">{{ currentTemplateLabel }}</span>
                    </div>

                    <div class="preview-canvas">
                      <!-- HTML：富文本消息卡片 -->
                      <div v-if="previewMode === 'html'" class="mock-rich">
                        <div class="mock-rich__head">
                          <span class="mock-badge"><Check :size="13" /></span>
                          <div class="mock-rich__titles">
                            <span class="mock-rich__title">{{ $t('page.quartz.notificationPage.previewStatusSuccess') }}</span>
                            <span class="mock-rich__sub">{{ previewSample.jobName }}</span>
                          </div>
                        </div>
                        <div class="mock-row">
                          <span>{{ $t('page.quartz.notificationPage.previewFieldJob') }}</span>
                          <b>{{ previewSample.jobName }}</b>
                        </div>
                        <div class="mock-row">
                          <span>{{ $t('page.quartz.notificationPage.previewFieldStatus') }}</span>
                          <b class="is-ok">{{ $t('page.quartz.notificationPage.previewStatusSuccess') }}</b>
                        </div>
                        <div class="mock-row">
                          <span>{{ $t('page.quartz.notificationPage.previewFieldTime') }}</span>
                          <b>{{ previewSample.triggerTime }}</b>
                        </div>
                        <div class="mock-row">
                          <span>{{ $t('page.quartz.notificationPage.previewFieldDuration') }}</span>
                          <b>1.28 s</b>
                        </div>
                        <div class="mock-foot">{{ $t('page.quartz.notificationPage.previewFooter') }}</div>
                      </div>

                      <!-- Markdown：结构化表格 -->
                      <div v-else-if="previewMode === 'markdown'" class="mock-md">
                        <div class="mock-md__title">
                          {{ $t('page.quartz.notificationPage.previewStatusSuccess') }}: {{ previewSample.jobName }}
                        </div>
                        <table>
                          <thead>
                            <tr>
                              <th>{{ $t('page.quartz.notificationPage.previewFieldJob') }}</th>
                              <th>{{ $t('page.quartz.notificationPage.previewFieldStatus') }}</th>
                              <th>{{ $t('page.quartz.notificationPage.previewFieldTime') }}</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr>
                              <td>{{ previewSample.jobName }}</td>
                              <td class="is-ok">{{ $t('page.quartz.notificationPage.previewStatusSuccess') }}</td>
                              <td>{{ previewSample.triggerTime }}</td>
                            </tr>
                          </tbody>
                        </table>
                        <div class="mock-foot">{{ $t('page.quartz.notificationPage.previewFooter') }}</div>
                      </div>

                      <!-- TXT：纯文本 -->
                      <pre v-else-if="previewMode === 'txt'" class="preview-code">{{ previewText }}</pre>

                      <!-- JSON：结构化数据 -->
                      <pre v-else class="preview-code">{{ previewJson }}</pre>
                    </div>

                    <div class="preview-foot">{{ $t('page.quartz.notificationPage.templatePreviewHint') }}</div>
                  </div>
                </template>
                <span class="preview-trigger">
                  <Eye :size="13" />
                  <span>{{ $t('page.quartz.notificationPage.templatePreview') }}</span>
                </span>
              </Popover>
            </span>
          </div>

          <div class="template-grid" role="radiogroup" :aria-label="$t('page.quartz.notificationPage.messageTemplate')">
            <button v-for="opt in templateOptions" :key="opt.value" type="button" class="template-card"
              :class="{ 'is-active': configForm.template === opt.value }" role="radio"
              :aria-checked="configForm.template === opt.value" @click="configForm.template = opt.value">
              <span class="template-card__icon" :style="{ color: opt.color, background: opt.tint }">
                {{ opt.glyph }}
              </span>
              <span class="template-card__name">{{ opt.label }}</span>
              <span class="template-card__desc">{{ opt.desc }}</span>
              <span class="template-card__check">
                <Check :size="10" />
              </span>
            </button>
          </div>

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
        :body-style="{ padding: '24px' }" :footer="null" :destroyOnClose="true" centered
        wrapClassName="quartz-notification-detail-modal">
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

/* ============ 消息模板：格式卡片 ============ */
.template-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 18px;
}

.template-card {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  width: 100%;
  padding: 12px 12px 14px;
  overflow: hidden;
  font-family: inherit;
  text-align: left;
  cursor: pointer;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 12px;
  transition:
    border-color 0.22s ease,
    box-shadow 0.22s ease,
    transform 0.22s ease,
    background 0.22s ease;

  &:hover {
    transform: translateY(-2px);
    border-color: hsl(var(--primary) / 0.45);
    box-shadow: 0 10px 22px -16px hsl(var(--primary));
  }

  &:focus-visible {
    outline: 2px solid hsl(var(--primary) / 0.45);
    outline-offset: 2px;
  }

  &.is-active {
    background: linear-gradient(
      180deg,
      hsl(var(--primary) / 0.09),
      hsl(var(--card)) 72%
    );
    border-color: hsl(var(--primary));
    box-shadow:
      0 0 0 1px hsl(var(--primary) / 0.35),
      0 12px 26px -18px hsl(var(--primary));
  }
}

.template-card__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.02em;
  border-radius: 9px;
  transition: transform 0.22s ease;
}

.template-card.is-active .template-card__icon {
  transform: scale(1.06);
}

.template-card__name {
  font-size: 13px;
  font-weight: 600;
  line-height: 1.3;
  color: hsl(var(--foreground));
}

.template-card__desc {
  font-size: 11px;
  line-height: 1.5;
  color: hsl(var(--muted-foreground));
}

.template-card__check {
  position: absolute;
  top: 8px;
  right: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  font-size: 10px;
  color: #fff;
  background: hsl(var(--primary));
  border-radius: 50%;
  opacity: 0;
  transform: scale(0.4);
  transition:
    opacity 0.22s ease,
    transform 0.22s ease;
}

.template-card.is-active .template-card__check {
  opacity: 1;
  transform: scale(1);
}

/* ============ 推送预览（悬浮显示） ============ */
.preview-trigger {
  display: inline-flex;
  gap: 4px;
  align-items: center;
  padding: 2px 9px;
  font-size: 11px;
  font-weight: 500;
  line-height: 1.6;
  color: hsl(var(--primary));
  cursor: pointer;
  user-select: none;
  background: hsl(var(--primary) / 0.08);
  border: 1px solid hsl(var(--primary) / 0.2);
  border-radius: 999px;
  transition:
    background 0.2s ease,
    border-color 0.2s ease;

  &:hover {
    background: hsl(var(--primary) / 0.16);
    border-color: hsl(var(--primary) / 0.4);
  }
}

/* 悬浮层容器去掉默认内边距与底色，由预览卡片自带样式 */
:global(.template-preview-popover .ant-popover-inner) {
  padding: 0;
  background: transparent;
  box-shadow: none;
}

:global(.template-preview-popover .ant-popover-arrow) {
  display: none;
}

.template-preview {
  width: 380px;
  overflow: hidden;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 14px;
  box-shadow:
    0 24px 48px -24px hsl(var(--foreground) / 0.45),
    0 2px 8px -4px hsl(var(--foreground) / 0.2);
}

.preview-bar {
  display: flex;
  gap: 8px;
  align-items: center;
  padding: 8px 12px;
  background: linear-gradient(180deg, hsl(var(--accent) / 0.75), hsl(var(--card)));
  border-bottom: 1px solid hsl(var(--border));
}

.preview-dots {
  display: inline-flex;
  gap: 4px;

  i {
    width: 7px;
    height: 7px;
    background: hsl(var(--muted-foreground) / 0.3);
    border-radius: 50%;
  }
}

.preview-bar__title {
  font-size: 11px;
  font-weight: 600;
  color: hsl(var(--muted-foreground));
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.preview-bar__tag {
  padding: 1px 8px;
  margin-left: auto;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--background) / 0.7);
  border: 1px solid hsl(var(--border));
  border-radius: 999px;
}

.preview-canvas {
  max-height: 340px;
  padding: 14px;
  overflow: auto;
  background: hsl(var(--accent) / 0.32);
}

.preview-foot {
  padding: 0 14px 12px;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--accent) / 0.32);
}

/* HTML 预览：消息卡片 */
.mock-rich {
  padding: 14px 16px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
  box-shadow: 0 6px 18px -16px hsl(var(--foreground));
}

.mock-rich__head {
  display: flex;
  gap: 10px;
  align-items: center;
  padding-bottom: 10px;
  border-bottom: 1px dashed hsl(var(--border));
}

.mock-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  font-size: 13px;
  color: #52c41a;
  background: rgba(82, 196, 26, 0.14);
  border-radius: 50%;
}

.mock-rich__titles {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.mock-rich__title {
  font-size: 13.5px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

.mock-rich__sub {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 11.5px;
  color: hsl(var(--muted-foreground));
}

.mock-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 7px 0;
  font-size: 12.5px;
  color: hsl(var(--muted-foreground));
  border-bottom: 1px dashed hsl(var(--border) / 0.6);

  b {
    font-weight: 500;
    color: hsl(var(--foreground));
    word-break: break-all;
    text-align: right;
  }

  b.is-ok {
    font-weight: 600;
    color: #52c41a;
  }
}

.mock-foot {
  margin-top: 10px;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  text-align: center;
}

/* Markdown 预览 */
.mock-md {
  padding: 14px 16px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
  box-shadow: 0 6px 18px -16px hsl(var(--foreground));

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 11.5px;
  }

  th,
  td {
    padding: 6px 8px;
    text-align: left;
    border: 1px solid hsl(var(--border));
  }

  th {
    font-weight: 600;
    color: hsl(var(--muted-foreground));
    background: hsl(var(--accent) / 0.6);
  }

  td.is-ok {
    font-weight: 600;
    color: #52c41a;
  }
}

.mock-md__title {
  margin-bottom: 10px;
  font-size: 13.5px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

/* TXT / JSON 预览 */
.preview-code {
  max-height: 240px;
  padding: 14px 16px;
  margin: 0;
  overflow: auto;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12px;
  line-height: 1.7;
  color: hsl(var(--foreground));
  white-space: pre-wrap;
  word-break: break-word;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
}

@media (max-width: 767px) {
  .template-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
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