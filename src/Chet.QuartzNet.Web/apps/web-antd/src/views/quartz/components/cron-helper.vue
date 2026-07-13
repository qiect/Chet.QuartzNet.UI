<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import { Info } from '@vben/icons';

import { Alert, Button, Modal, Table } from 'ant-design-vue';

import { $t } from '#/locales';

defineProps<{ visible: boolean }>();
const emit = defineEmits(['cancel', 'select', 'update:visible']);

const cronExamples = [
  {
    id: '1',
    name: $t('page.quartz.cronHelperPage.everySecond'),
    expression: '*/1 * * * * ?',
    description: $t('page.quartz.cronHelperPage.everySecondDesc'),
  },
  {
    id: '2',
    name: $t('page.quartz.cronHelperPage.everyMinute'),
    expression: '0 */1 * * * ?',
    description: $t('page.quartz.cronHelperPage.everyMinuteDesc'),
  },
  {
    id: '3',
    name: $t('page.quartz.cronHelperPage.everyHour'),
    expression: '0 0 */1 * * ?',
    description: $t('page.quartz.cronHelperPage.everyHourDesc'),
  },
  {
    id: '4',
    name: $t('page.quartz.cronHelperPage.dailyMidnight'),
    expression: '0 0 0 * * ?',
    description: $t('page.quartz.cronHelperPage.dailyMidnightDesc'),
  },
  {
    id: '5',
    name: $t('page.quartz.cronHelperPage.everyMonday'),
    expression: '0 0 0 ? * MON',
    description: $t('page.quartz.cronHelperPage.everyMondayDesc'),
  },
  {
    id: '6',
    name: $t('page.quartz.cronHelperPage.monthlyFirst'),
    expression: '0 0 0 1 * ?',
    description: $t('page.quartz.cronHelperPage.monthlyFirstDesc'),
  },
];

const formatInfo = [
  {
    field: $t('page.quartz.cronHelperPage.second'),
    range: '0-59',
    symbols: '*, -, ,, /',
  },
  {
    field: $t('page.quartz.cronHelperPage.minute'),
    range: '0-59',
    symbols: '*, -, ,, /',
  },
  {
    field: $t('page.quartz.cronHelperPage.hour'),
    range: '0-23',
    symbols: '*, -, ,, /',
  },
  {
    field: $t('page.quartz.cronHelperPage.day'),
    range: '1-31',
    symbols: '*, -, ,, /, ?, L, W',
  },
  {
    field: $t('page.quartz.cronHelperPage.month'),
    range: '1-12 / JAN-DEC',
    symbols: '*, -, ,, /',
  },
  {
    field: $t('page.quartz.cronHelperPage.week'),
    range: '1-7 / SUN-SAT',
    symbols: '*, -, ,, /, ?, L, #',
  },
];

// Cron 标准格式字符串
const formatString = `[${$t('page.quartz.cronHelperPage.second')}] [${$t(
  'page.quartz.cronHelperPage.minute',
)}] [${$t('page.quartz.cronHelperPage.hour')}] [${$t(
  'page.quartz.cronHelperPage.day',
)}] [${$t('page.quartz.cronHelperPage.month')}] [${$t(
  'page.quartz.cronHelperPage.week',
)}] [Year]`;

const symbolsLabel = () =>
  `${$t('page.quartz.cronHelperPage.supportedSymbols')}：`;

const cronColumns: TableColumnsType<any> = [
  {
    title: $t('page.quartz.cronHelperPage.businessScenario'),
    dataIndex: 'name',
    key: 'name',
    width: 140,
  },
  {
    title: $t('page.quartz.cronHelperPage.expression'),
    dataIndex: 'expression',
    key: 'expression',
    width: 180,
  },
  {
    title: $t('page.quartz.cronHelperPage.executionLogic'),
    dataIndex: 'description',
    key: 'description',
  },
  {
    title: $t('page.quartz.cronHelperPage.action'),
    key: 'action',
    width: 80,
    align: 'center',
  },
];

const handleSelectCron = (record: any) => {
  emit('select', record.expression);
  emit('update:visible', false);
};

const handleCancel = () => emit('update:visible', false);
</script>

<template>
  <Modal
    :open="visible"
    :title="$t('page.quartz.cronHelperPage.title')"
    @cancel="handleCancel"
    width="860px"
    :footer="null"
    :z-index="10000"
    centered
    destroy-on-close
  >
    <div class="cron-helper-container">
      <!-- 常用表达式示例 -->
      <section class="section-box">
        <div class="section-title">
          <span class="title-bar"></span>
          {{ $t('page.quartz.cronHelperPage.commonExamples') }}
        </div>
        <Table
          :columns="cronColumns"
          :data-source="cronExamples"
          :pagination="false"
          size="middle"
          class="custom-table"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'expression'">
              <code class="cron-code">{{ record.expression }}</code>
            </template>
            <template v-if="column.key === 'action'">
              <Button
                type="link"
                size="small"
                class="select-btn"
                @click="handleSelectCron(record)"
              >
                {{ $t('page.quartz.cronHelperPage.select') }}
              </Button>
            </template>
          </template>
        </Table>
      </section>

      <!-- Cron 格式详解 -->
      <section class="section-box">
        <div class="section-title">
          <span class="title-bar"></span>
          {{ $t('page.quartz.cronHelperPage.formatDetails') }}
        </div>

        <Alert class="custom-alert" type="info" show-icon>
          <template #message>
            {{ $t('page.quartz.cronHelperPage.standardFormat') }}：
            <code class="format-tag">{{ formatString }}</code>
          </template>
        </Alert>

        <div class="format-grid">
          <div v-for="item in formatInfo" :key="item.field" class="format-card">
            <div class="card-header">
              <span class="field-name">{{ item.field }}</span>
              <span class="range-tag">{{ item.range }}</span>
            </div>
            <div class="card-body">
              <span class="body-label">{{ symbolsLabel() }}</span>
              <code class="symbols-code">{{ item.symbols }}</code>
            </div>
          </div>
        </div>

        <div class="symbol-tip">
          <Info :size="13" />
          <span>{{ $t('page.quartz.cronHelperPage.symbolTip') }}</span>
        </div>
      </section>
    </div>
  </Modal>
</template>

<style scoped lang="less">
.cron-helper-container {
  padding: 4px 4px 8px;

  .section-box {
    margin-bottom: 20px;

    &:last-child {
      margin-bottom: 0;
    }
  }

  .section-title {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 15px;
    font-weight: 600;
    margin-bottom: 12px;
    color: hsl(var(--foreground));

    .title-bar {
      display: inline-block;
      width: 3px;
      height: 14px;
      border-radius: 2px;
      background: hsl(var(--primary));
      flex-shrink: 0;
    }
  }

  /* 表达式代码块 */
  .cron-code {
    display: inline-block;
    padding: 3px 10px;
    background: hsl(var(--muted) / 0.6);
    border: 1px solid hsl(var(--border));
    border-radius: 6px;
    color: hsl(var(--destructive));
    font-family:
      'JetBrains Mono', 'Fira Code', 'Cascadia Code', 'Courier New', monospace;
    font-weight: 600;
    font-size: 13px;
    letter-spacing: 0.3px;
  }

  /* 格式说明标签 */
  .format-tag {
    display: inline-block;
    padding: 2px 8px;
    background: hsl(var(--primary) / 0.1);
    border: 1px solid hsl(var(--primary) / 0.2);
    border-radius: 4px;
    color: hsl(var(--primary));
    font-family: 'JetBrains Mono', 'Fira Code', 'Courier New', monospace;
    font-size: 12px;
    font-weight: 500;
    margin-left: 4px;
  }

  /* Alert 提示框样式调整 */
  :deep(.custom-alert) {
    margin-bottom: 14px;
    border-radius: 8px;
    background: hsl(var(--primary) / 0.06);
    border: 1px solid hsl(var(--primary) / 0.2);

    .ant-alert-icon {
      color: hsl(var(--primary));
    }

    .ant-alert-message {
      color: hsl(var(--foreground));
      font-size: 13px;
      line-height: 1.6;
    }
  }

  /* 格式卡片网格 */
  .format-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 10px;
    margin-bottom: 12px;
  }

  .format-card {
    background: hsl(var(--muted) / 0.4);
    border: 1px solid hsl(var(--border));
    border-radius: 8px;
    padding: 10px 12px;
    transition: all 0.2s ease;

    &:hover {
      border-color: hsl(var(--primary) / 0.4);
      background: hsl(var(--muted) / 0.6);
      transform: translateY(-1px);
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 6px;

      .field-name {
        font-weight: 600;
        color: hsl(var(--foreground));
        font-size: 13px;
      }

      .range-tag {
        font-size: 11px;
        background: hsl(var(--primary) / 0.1);
        color: hsl(var(--primary));
        padding: 1px 8px;
        border-radius: 10px;
        font-family: 'JetBrains Mono', 'Courier New', monospace;
        font-weight: 500;
      }
    }

    .card-body {
      font-size: 12px;
      color: hsl(var(--muted-foreground));
      display: flex;
      align-items: center;
      gap: 4px;
      flex-wrap: wrap;

      .body-label {
        white-space: nowrap;
      }

      .symbols-code {
        color: hsl(var(--primary));
        font-family: 'JetBrains Mono', 'Courier New', monospace;
        font-size: 12px;
        font-weight: 500;
      }
    }
  }

  /* 符号说明提示 */
  .symbol-tip {
    display: flex;
    align-items: center;
    gap: 6px;
    margin-top: 4px;
    padding: 8px 12px;
    background: hsl(var(--warning) / 0.06);
    border: 1px solid hsl(var(--warning) / 0.2);
    border-radius: 6px;
    color: hsl(var(--muted-foreground));
    font-size: 12px;
    line-height: 1.5;

    svg {
      color: hsl(var(--warning));
      flex-shrink: 0;
      margin-top: 1px;
    }
  }

  /* 表格样式微调 */
  :deep(.custom-table) {
    .ant-table-thead > tr > th {
      background: hsl(var(--muted) / 0.4);
      color: hsl(var(--foreground));
      font-weight: 600;
      font-size: 13px;
      border-bottom: 1px solid hsl(var(--border));
    }

    .ant-table-tbody > tr > td {
      border-bottom: 1px solid hsl(var(--border) / 0.6);
      font-size: 13px;
      color: hsl(var(--foreground) / 0.85);
    }

    .ant-table-tbody > tr:hover > td {
      background: hsl(var(--muted) / 0.5);
    }
  }

  /* 选择按钮 */
  .select-btn {
    padding: 0 8px;
    font-size: 12px;
    height: 24px;
  }
}

/* 响应式 */
@media (max-width: 768px) {
  .format-grid {
    grid-template-columns: repeat(2, 1fr) !important;
  }
}

@media (max-width: 480px) {
  .format-grid {
    grid-template-columns: 1fr !important;
  }
}
</style>
