<script setup lang="ts">
import { h, onMounted } from 'vue';
import { useRouter } from 'vue-router';

import { Button, notification } from 'ant-design-vue';

import { $t } from '#/locales';

import { getRecoverySummary } from '../../../api/quartz/scheduler';
import type { RecoverySummaryDto } from '../../../api/quartz/scheduler';

const router = useRouter();

const storageKey = (bootId: string) => `quartz-recovery-notice:${bootId}`;
// 用一次性标记传筛选意图，不走 URL query，避免与菜单入口生成两个标签页
// log.vue 中使用同名的 sessionStorage key 消费
const LOG_PENDING_FILTER_KEY = 'quartz-log-pending-filter';

const goToLogs = (key: string) => {
  notification.close(key);
  sessionStorage.setItem(LOG_PENDING_FILTER_KEY, 'needsAttention');
  router.push({ path: '/log' });
};

const showNotice = (summary: RecoverySummaryDto) => {
  const key = storageKey(summary.bootId);
  if (localStorage.getItem(key)) {
    return;
  }
  localStorage.setItem(key, '1');

  const options = {
    key,
    duration: 0,
    message: $t('page.quartz.recoveryNotice.title', {
      count: summary.interruptedCount,
    }),
    description: $t('page.quartz.recoveryNotice.description', {
      rerun: summary.rerunCount,
      skipped: summary.skippedCount,
      failed: summary.failedCount,
    }),
    btn: () =>
      h(
        Button,
        {
          type: 'primary',
          size: 'small',
          onClick: () => goToLogs(key),
        },
        () => $t('page.quartz.recoveryNotice.detail'),
      ),
  };

  if (summary.failedCount > 0) {
    notification.error(options);
  } else if (summary.skippedCount > 0) {
    notification.warning(options);
  } else {
    notification.success(options);
  }
};

const loadSummary = async (): Promise<RecoverySummaryDto | null> => {
  try {
    const response = await getRecoverySummary();
    return response.success ? (response.data ?? null) : null;
  } catch {
    return null;
  }
};

onMounted(async () => {
  let summary = await loadSummary();
  if (!summary) {
    return;
  }

  // 补偿默认有 30 秒启动延迟，轮询到恢复流程结束（最多约 60 秒）
  let retries = 0;
  while (summary.isRunning && retries < 20) {
    await new Promise((resolve) => setTimeout(resolve, 3000));
    summary = (await loadSummary()) ?? summary;
    retries++;
  }

  if (summary.interruptedCount <= 0) {
    return;
  }

  showNotice(summary);
});
</script>

<template>
  <!-- 仅执行副作用，不渲染内容 -->
  <span style="display: none"></span>
</template>
