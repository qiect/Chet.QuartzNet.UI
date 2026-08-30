<script lang="ts" setup>
import { ref, computed, watch } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, message } from 'ant-design-vue';

import { useVbenForm, z } from '#/adapter/form';
import { $t } from '#/locales';
import { useI18n } from '@vben/locales';

import { getSystemConfig, saveSystemConfig } from '../../api/quartz/system-config';
import { useSystemConfig } from '../../composables/use-system-config';

// 加载状态
const loading = ref(false);

const { locale } = useI18n();

// 环境选项
const environmentOptions = computed(() => [
  { label: $t('page.quartz.systemConfigPage.envDEV'), value: 'DEV' },
  { label: $t('page.quartz.systemConfigPage.envTEST'), value: 'TEST' },
  { label: $t('page.quartz.systemConfigPage.envUAT'), value: 'UAT' },
  { label: $t('page.quartz.systemConfigPage.envPROD'), value: 'PROD' },
]);

const [BaseForm, formApi] = useVbenForm({
  commonConfig: {
    colon: true,
    componentProps: {
      class: 'w-full',
    },
    labelWidth: 140,
  },
  layout: 'horizontal',
  // 提交与重置由 VbenForm 内置按钮触发
  handleSubmit,
  handleReset,
  submitButtonOptions: {
    content: $t('page.quartz.systemConfigPage.save'),
  },
  resetButtonOptions: {
    content: $t('page.quartz.systemConfigPage.reset'),
  },
  schema: [
    {
      component: 'Input',
      componentProps: {
        placeholder: $t('page.quartz.systemConfigPage.serviceNamePlaceholder'),
      },
      fieldName: 'serviceName',
      label: $t('page.quartz.systemConfigPage.serviceName'),
      rules: z.string().min(1, $t('page.quartz.systemConfigPage.serviceNameRequired')),
    },
    {
      component: 'Select',
      componentProps: {
        options: environmentOptions.value,
        placeholder: $t('page.quartz.systemConfigPage.environmentPlaceholder'),
      },
      fieldName: 'environment',
      label: $t('page.quartz.systemConfigPage.environment'),
      rules: 'selectRequired',
    },
    {
      component: 'Textarea',
      componentProps: {
        placeholder: $t('page.quartz.systemConfigPage.serviceDescriptionPlaceholder'),
        rows: 3,
        maxlength: 200,
        showCount: true,
      },
      fieldName: 'serviceDescription',
      formItemClass: 'md:col-span-2',
      label: $t('page.quartz.systemConfigPage.serviceDescription'),
    },
  ],
  wrapperClass: 'grid-cols-1 md:grid-cols-2',
});

// 加载系统配置并回填表单
async function loadConfig() {
  loading.value = true;
  try {
    const response = (await getSystemConfig()) as any;
    const data = response?.data ?? response;
    formApi.setValues({
      serviceName: data?.serviceName || '',
      environment: data?.environment || 'DEV',
      serviceDescription: data?.serviceDescription || '',
    });
  } catch (error) {
    message.error($t('page.quartz.systemConfigPage.getConfigFailed'));
    console.error($t('page.quartz.systemConfigPage.getConfigFailed'), error);
  } finally {
    loading.value = false;
  }
}

// 监听语言切换，更新表单标签和选项
watch(locale, () => {
  formApi.updateSchema([
    {
      fieldName: 'serviceName',
      label: $t('page.quartz.systemConfigPage.serviceName'),
      componentProps: { placeholder: $t('page.quartz.systemConfigPage.serviceNamePlaceholder') },
      rules: z.string().min(1, $t('page.quartz.systemConfigPage.serviceNameRequired')),
    },
    {
      fieldName: 'environment',
      label: $t('page.quartz.systemConfigPage.environment'),
      componentProps: {
        options: environmentOptions.value,
        placeholder: $t('page.quartz.systemConfigPage.environmentPlaceholder'),
      },
    },
    {
      fieldName: 'serviceDescription',
      label: $t('page.quartz.systemConfigPage.serviceDescription'),
      componentProps: { placeholder: $t('page.quartz.systemConfigPage.serviceDescriptionPlaceholder') },
    },
  ]);
  formApi.setState({
    submitButtonOptions: { content: $t('page.quartz.systemConfigPage.save') },
    resetButtonOptions: { content: $t('page.quartz.systemConfigPage.reset') },
  });
});

// 保存（由 VbenForm handleSubmit 调用，values 已经过校验）
async function handleSubmit(values: Record<string, any>) {
  formApi.setState({ submitButtonOptions: { loading: true } });
  try {
    const response = await saveSystemConfig({
      serviceName: values.serviceName || '',
      environment: values.environment || 'DEV',
      serviceDescription: values.serviceDescription || '',
    });
    if (response.success) {
      message.success($t('page.quartz.systemConfigPage.saveSuccess'));
      // 同步全局状态（标题/横幅立即生效）
      const { systemConfig } = useSystemConfig();
      systemConfig.value = {
        serviceName: values.serviceName || '',
        environment: values.environment || 'DEV',
        serviceDescription: values.serviceDescription || '',
      };
    } else {
      message.error(response.message || $t('page.quartz.systemConfigPage.saveFailed'));
    }
  } catch (error: any) {
    message.error(error.message || $t('page.quartz.systemConfigPage.saveFailed'));
    console.error($t('page.quartz.systemConfigPage.saveFailed'), error);
  } finally {
    formApi.setState({ submitButtonOptions: { loading: false } });
  }
}

// 重置（重新从服务器加载）
async function handleReset() {
  await loadConfig();
  message.info($t('page.quartz.systemConfigPage.resetSuccess'));
}

loadConfig();
</script>

<template>
  <Page content-class="flex flex-col gap-4">
    <Card :title="$t('page.quartz.systemConfigPage.basicSection')">
      <div class="text-muted-foreground mb-4">
        <p>
          {{ $t('page.quartz.systemConfigPage.description') }}
        </p>
      </div>
      <BaseForm />
    </Card>
  </Page>
</template>