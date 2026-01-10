<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue';
// 导入日期格式化工具
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
  Dropdown,
  Menu,
  Typography,
} from 'ant-design-vue';
import type { FormInstance, PaginationProps } from 'ant-design-vue';

// 定义SortOrder类型
type SortOrder = 'ascend' | 'descend' | undefined;

// 导入通知API服务
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
  [NotificationStatusEnum.Pending]: { text: '待发送', status: 'default' },
  [NotificationStatusEnum.Sent]: { text: '发送成功', status: 'success' },
  [NotificationStatusEnum.Failed]: { text: '发送失败', status: 'error' },
};

// 响应式数据
const loading = ref(false);
const dataSource = ref<QuartzNotificationDto[]>([]);
const total = ref(0);
const currentPage = ref(1);
const pageSize = ref(10);

// 详情对话框
const detailModalVisible = ref(false);
const detailModalTitle = ref('通知详情');
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
const configModalTitle = ref('通知配置');
const configForm = reactive<PushPlusConfigDto>({
  token: '',
  channel: 'wechat',
  template: 'html',
  topic: '',
  enable: false,
  strategy: {
    notifyOnJobSuccess: false,
    notifyOnJobFailure: true,
    notifyOnSchedulerError: true,
  },
});

const formRef = ref<FormInstance>();

// 列配置
const columns = computed(() => [
  {
    title: '通知标题',
    dataIndex: 'title',
    ellipsis: true,
  },
  {
    title: '触发来源',
    dataIndex: 'triggeredBy',
    ellipsis: true,
  },
  {
    title: '状态',
    dataIndex: 'status',
    ellipsis: true,
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      const status = notificationStatusMap[record.status];
      return {
        children: h(
          Tag,
          { color: status?.status || 'default' },
          status?.text || record.status || '未知',
        ),
      };
    },
  },
  {
    title: '发送时间',
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
    title: '耗时(ms)',
    dataIndex: 'duration',
    ellipsis: true,
    sorter: true,
    sortOrder: sortBy.value === 'duration' ? sortOrder.value : undefined,
  },
  {
    title: '创建时间',
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
    title: '操作',
    key: 'action',
    width: 80,
    fixed: 'right',
    customRender: ({ record }: { record: QuartzNotificationDto }) => {
      // 创建详情菜单项
      const detailMenuItem = h(
        Menu.Item,
        {
          onClick: () => handleDetail(record),
        },
        '详情',
      );

      // 创建删除菜单项
      const deleteMenuItem = h(
        Menu.Item,
        {
          onClick: () => handleDelete(record),
          danger: true,
        },
        '删除',
      );

      // 创建菜单
      const menu = h(Menu, null, [detailMenuItem, deleteMenuItem]);

      // 创建按钮
      const button = h(
        Button,
        {
          type: 'primary',
          disabled: loading.value,
        },
        '操作',
      );

      // 创建下拉菜单
      const dropdown = h(
        Dropdown,
        {
          trigger: ['click'],
          overlay: menu,
        },
        () => button,
      );

      return {
        children: dropdown,
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
  showTotal: (total, range) => `${range[0]}-${range[1]} 共 ${total} 条`,
  pageSizeOptions: ['10', '20', '50', '100'],
}));

// 表格变化事件处理
const handleTableChange = (pagination: any, _filters: any, sorter: any) => {
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
        ? 'ascend'
        : sorter.order === 'descend'
          ? 'descend'
          : undefined;
  }

  // 重新加载数据
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
    message.error('获取通知列表失败');
    console.error('获取通知列表失败:', error);
  } finally {
    loading.value = false;
  }
};

// 处理搜索
const handleSearch = async () => {
  if (searchFormRef.value) {
    // 触发表单验证
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
    // 获取当前配置
    const response = await getPushPlusConfig();
    // 正确处理API响应，获取data字段
    Object.assign(configForm, response.data);
    configModalVisible.value = true;
  } catch (error) {
    message.error('获取配置失败');
    console.error('获取配置失败:', error);
  }
};

// 保存配置
const handleSaveConfig = async () => {
  if (!formRef.value) return;

  try {
    await formRef.value.validateFields();
    loading.value = true;

    const response = await savePushPlusConfig(configForm);
    if (response.success) {
      message.success('配置保存成功');
      configModalVisible.value = false;
    } else {
      message.error(response.message || '配置保存失败');
    }
  } catch (error: any) {
    if (error.errorFields) {
      return; // 表单验证错误已显示
    }
    const errorMessage = error.message || '配置保存失败';
    message.error(errorMessage);
    console.error('保存配置失败:', error);
  } finally {
    loading.value = false;
  }
};

// 发送测试通知
const handleSendTest = async () => {
  try {
    loading.value = true;
    const response = await sendTestNotification();
    if (response.success) {
      message.success('测试通知发送成功');
      loadNotificationList();
    } else {
      message.error(response.message || '测试通知发送失败');
    }
  } catch (error) {
    message.error('测试通知发送失败');
    console.error('发送测试通知失败:', error);
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
    title: '确认删除',
    content: '确定要删除这条通知吗？此操作不可恢复。',
    okText: '确定',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        const response = await deleteNotification(notification.notificationId);
        if (response.success) {
          message.success('通知删除成功');
          loadNotificationList();
        } else {
          message.error(response.message || '通知删除失败');
        }
      } catch (error) {
        message.error('通知删除失败');
        console.error('删除通知失败:', error);
      }
    },
  });
};

// 清空通知
const handleClearNotifications = () => {
  Modal.confirm({
    title: '确认清空',
    content: '确定要清空所有符合条件的通知吗？此操作不可恢复。',
    okText: '确定',
    okType: 'danger',
    cancelText: '取消',
    async onOk() {
      try {
        const response = await clearNotifications({
          pageIndex: 1,
          pageSize: 1,
          status: searchForm.value.status,
          triggeredBy: searchForm.value.triggeredBy,
        });
        if (response.success) {
          message.success('通知清空成功');
          loadNotificationList();
        } else {
          message.error(response.message || '通知清空失败');
        }
      } catch (error) {
        message.error('通知清空失败');
        console.error('清空通知失败:', error);
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
  <Page>
    <template #default>
      <Card class="mb-4">
        <Form ref="searchFormRef" :model="searchForm" layout="horizontal" :label-align="'right'">
          <Row :gutter="16">
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="6" :xxl="4">
              <Form.Item label="通知状态" name="status">
                <Select v-model:value="searchForm.status" placeholder="请选择状态" allowClear>
                  <Select.Option :value="NotificationStatusEnum.Pending">待发送</Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Sent">发送成功</Select.Option>
                  <Select.Option :value="NotificationStatusEnum.Failed">发送失败</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="12" :md="12" :lg="8" :xl="6" :xxl="4">
              <Form.Item label="触发来源" name="triggeredBy">
                <Input v-model:value="searchForm.triggeredBy" placeholder="请输入触发来源" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24" :lg="8" :xl="12" :xxl="16" class="text-right">
              <Space>
                <Button type="primary" @click="handleSearch">搜索</Button>
                <Button @click="handleReset">重置</Button>
              </Space>
            </Col>
          </Row>
        </Form>
      </Card>

      <!-- 通知管理卡片 -->
      <Card>
        <div class="mb-4 flex items-center justify-between">
          <Space>
            <Button type="primary" @click="handleOpenConfigModal">通知配置</Button>
            <Button type="default" @click="handleSendTest">发送测试通知</Button>
          </Space>
          <Space>
            <Button danger @click="handleClearNotifications">清空</Button>
          </Space>
        </div>
        <!-- 通知列表 -->
        <Table :columns="columns" :data-source="dataSource" :pagination="pagination" :loading="loading"
          :rowKey="(record) => record.notificationId" @change="handleTableChange" size="middle"
          :scroll="{ x: 'max-content' }" />
      </Card>

      <!-- 配置对话框 -->
      <Modal v-model:open="configModalVisible" :title="configModalTitle" width="800px" :body-style="{ padding: '24px' }"
        destroyOnClose @cancel="configModalVisible = false">
        <Form ref="formRef" :model="configForm" layout="horizontal" :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }"
          :label-align="'right'">
          <Row :gutter="16">
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="是否启用" name="enable" valuePropName="checked">
                <Switch v-model:checked="configForm.enable" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="Token" name="token" :rules="[
                {
                  required: configForm.enable,
                  message: '请输入PushPlus Token',
                },
              ]">
                <Input v-model:value="configForm.token" placeholder="请输入PushPlus Token" />
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="推送渠道" name="channel">
                <Select v-model:value="configForm.channel">
                  <Select.Option value="wechat">微信</Select.Option>
                  <Select.Option value="cp">企业微信</Select.Option>
                  <Select.Option value="webhook">钉钉</Select.Option>
                  <Select.Option value="mail">邮件</Select.Option>
                  <Select.Option value="sms">短信</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="消息模板" name="template">
                <Select v-model:value="configForm.template">
                  <Select.Option value="html">HTML</Select.Option>
                  <Select.Option value="text">TEXT</Select.Option>
                  <Select.Option value="markdown">Markdown</Select.Option>
                </Select>
              </Form.Item>
            </Col>
            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="主题" name="topic">
                <Input v-model:value="configForm.topic" placeholder="请输入主题（可选）" />
              </Form.Item>
            </Col>

            <Col :xs="24" :sm="24" :md="24">
              <Form.Item label="通知策略" class="mt-4">
                <Form.Item label="作业成功时发送" name="strategy.notifyOnJobSuccess" valuePropName="checked">
                  <Switch v-model:checked="configForm.strategy.notifyOnJobSuccess" />
                </Form.Item>
                <Form.Item label="作业失败时发送" name="strategy.notifyOnJobFailure" valuePropName="checked">
                  <Switch v-model:checked="configForm.strategy.notifyOnJobFailure" />
                </Form.Item>
                <Form.Item label="调度器异常时发送" name="strategy.notifyOnSchedulerError" valuePropName="checked">
                  <Switch v-model:checked="configForm.strategy.notifyOnSchedulerError" />
                </Form.Item>
              </Form.Item>
            </Col>
          </Row>
        </Form>

        <template #footer>
          <Space>
            <Button @click="configModalVisible = false">取消</Button>
            <Button type="primary" @click="handleSaveConfig">保存</Button>
          </Space>
        </template>
      </Modal>

      <!-- 详情对话框 -->
      <Modal v-model:open="detailModalVisible" :title="detailModalTitle" width="1000px" :footer="null"
        :destroyOnClose="true">
        <div v-if="currentNotification" class="notification-detail">
          <!-- 头部信息 -->
          <div class="detail-header mb-4 rounded-lg bg-gray-50 p-4">
            <div class="mb-3 flex items-center justify-between">
              <Typography.Title :level="4" class="m-0">
                {{ currentNotification.title }}
              </Typography.Title>
              <Tag :color="notificationStatusMap[currentNotification.status].status" class="text-lg">
                {{ notificationStatusMap[currentNotification.status].text }}
              </Tag>
            </div>

            <!-- 基本信息行 -->
            <div class="mt-2 grid grid-cols-1 gap-4">
              <div class="flex items-center">
                <span class="mr-2 font-bold">触发来源:</span>
                <span>{{ currentNotification.triggeredBy || '-' }}</span>
              </div>
              <div class="grid grid-cols-1 gap-4 md:grid-cols-3">
                <div class="flex items-center">
                  <span class="mr-2 font-bold">发送时间:</span>
                  <span>{{
                    currentNotification.sendTime
                      ? formatDateTime(currentNotification.sendTime)
                      : '-'
                  }}</span>
                </div>
                <div class="flex items-center">
                  <span class="mr-2 font-bold">发送耗时:</span>
                  <span>{{
                    currentNotification.duration
                      ? `${currentNotification.duration} ms`
                      : '0 ms'
                  }}</span>
                </div>
                <div class="flex items-center">
                  <span class="mr-2 font-bold">创建时间:</span>
                  <span>{{ formatDateTime(currentNotification.createTime) }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- 内容区域 -->
          <div class="detail-content">
            <!-- 通知内容 -->
            <div class="mb-6">
              <Typography.Title :level="5" class="mb-2">通知内容</Typography.Title>
              <div class="content-box rounded-lg border border-gray-200 bg-gray-50 p-4">
                <div v-html="currentNotification.content"></div>
              </div>
            </div>

            <!-- 错误信息 -->
            <div v-if="currentNotification.errorMessage" class="mb-6">
              <Typography.Title :level="5" class="mb-2">错误信息</Typography.Title>
              <div class="rounded-lg border border-red-200 bg-red-50 p-4">
                <pre class="word-break-break-word m-0 whitespace-pre-wrap text-sm text-red-800">{{
                  currentNotification.errorMessage }}</pre>
              </div>
            </div>
          </div>
        </div>

        <!-- 底部按钮 -->
        <div class="mt-4 flex justify-end">
          <Button @click="detailModalVisible = false" type="primary">关闭</Button>
        </div>
      </Modal>
    </template>
  </Page>
</template>

<style scoped>
/* VbenAdmin 风格样式优化 */
.mb-4 {
  margin-bottom: 16px;
}

.text-right {
  text-align: right;
}

.pl-6 {
  padding-left: 24px;
}

.mt-4 {
  margin-top: 16px;
}

/* 通知详情样式 */
.content-box {
  min-height: 150px;
  max-height: 500px;
  overflow-y: auto;
}
</style>
