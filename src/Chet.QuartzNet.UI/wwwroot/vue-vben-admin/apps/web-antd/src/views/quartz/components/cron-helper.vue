<template>
  <Modal
    v-model:visible="visible"
    title="Cron帮助"
    @cancel="handleCancel"
    width="800px"
    :footer="null"
  >
    <div class="cron-helper">
      <Card class="mb-4" title="常用Cron表达式">
        <Table
          :columns="cronColumns"
          :data-source="cronExamples"
          :pagination="false"
          :row-key="'id'"
          @row-click="handleSelectCron"
          :row-hover="true"
        >
          <template #action="{ record }">
            <Button type="link" @click.stop="handleSelectCron(record)">
              选择
            </Button>
          </template>
        </Table>
      </Card>

      <Card title="Cron表达式格式说明">
        <div class="format-description">
          <p>标准Cron表达式格式：[秒] [分] [时] [日] [月] [周] [年](可选)</p>
          <ul>
            <li><strong>秒 (0-59)</strong>: 允许值范围：0-59，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/)</li>
            <li><strong>分 (0-59)</strong>: 允许值范围：0-59，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/)</li>
            <li><strong>时 (0-23)</strong>: 允许值范围：0-23，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/)</li>
            <li><strong>日 (1-31)</strong>: 允许值范围：1-31，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/), L, W</li>
            <li><strong>月 (1-12 或 JAN-DEC)</strong>: 允许值范围：1-12或JAN-DEC，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/)</li>
            <li><strong>周 (1-7 或 SUN-SAT)</strong>: 允许值范围：1-7或SUN-SAT，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/), L, #</li>
            <li><strong>年 (可选)</strong>: 允许值范围：1970-2099，支持通配符(*), 问号(?), 连字符(-), 逗号(,), 斜杠(/)</li>
          </ul>
        </div>
      </Card>
    </div>
  </Modal>
</template>

<script lang="ts">
import { defineComponent, PropType } from 'vue';
import { Modal, Card, Button, Table } from 'ant-design-vue';
import type { ColumnsType } from 'ant-design-vue';

interface CronExample {
  id: string;
  name: string;
  expression: string;
  description: string;
}

export default defineComponent({
  name: 'CronHelperModal',
  components: {
    Modal,
    Card,
    Table,
    Button
  },
  props: {
    visible: {
      type: Boolean as PropType<boolean>,
      default: false
    }
  },
  emits: ['cancel', 'select'],
  setup(props, { emit }) {
    // 常用Cron表达式示例
    const cronExamples: CronExample[] = [
      {
        id: '1',
        name: '每秒执行',
        expression: '*/1 * * * * ?',
        description: '每秒执行一次'
      },
      {
        id: '2',
        name: '每5秒执行',
        expression: '*/5 * * * * ?',
        description: '每5秒执行一次'
      },
      {
        id: '3',
        name: '每分钟执行',
        expression: '0 */1 * * * ?',
        description: '每分钟的第0秒执行'
      },
      {
        id: '4',
        name: '每5分钟执行',
        expression: '0 */5 * * * ?',
        description: '每5分钟执行一次'
      },
      {
        id: '5',
        name: '每小时执行',
        expression: '0 0 */1 * * ?',
        description: '每小时的第0分0秒执行'
      },
      {
        id: '6',
        name: '每天凌晨执行',
        expression: '0 0 0 * * ?',
        description: '每天凌晨0点执行'
      },
      {
        id: '7',
        name: '每天上午9点执行',
        expression: '0 0 9 * * ?',
        description: '每天上午9点执行'
      },
      {
        id: '8',
        name: '每天中午12点执行',
        expression: '0 0 12 * * ?',
        description: '每天中午12点执行'
      },
      {
        id: '9',
        name: '每周一执行',
        expression: '0 0 0 ? * MON',
        description: '每周一凌晨0点执行'
      },
      {
        id: '10',
        name: '每月1号执行',
        expression: '0 0 0 1 * ?',
        description: '每月1号凌晨0点执行'
      },
      {
        id: '11',
        name: '工作上午执行',
        expression: '0 0 9 ? * MON-FRI',
        description: '周一至周五上午9点执行'
      },
      {
        id: '12',
        name: '每季度第一天执行',
        expression: '0 0 0 1 1,4,7,10 ?',
        description: '每季度第一天凌晨0点执行'
      }
    ];

    // 表格列配置
    const cronColumns: BasicColumn<CronExample>[] = [
      {
        title: '名称',
        dataIndex: 'name',
        key: 'name',
        width: 120
      },
      {
        title: '表达式',
        dataIndex: 'expression',
        key: 'expression',
        width: 200,
        copyable: true
      },
      {
        title: '描述',
        dataIndex: 'description',
        key: 'description'
      },
      {
        title: '操作',
        key: 'action',
        width: 80,
        fixed: 'right',
        slots: {
          default: 'action'
        }
      }
    ];

    // 选择Cron表达式
    const handleSelectCron = (record: CronExample) => {
      emit('select', record.expression);
    };

    // 取消
    const handleCancel = () => {
      emit('cancel');
    };

    return {
      cronExamples,
      cronColumns,
      handleSelectCron,
      handleCancel
    };
  }
});
</script>

<style scoped lang="less">
.cron-helper {
  .format-description {
    p {
      margin-bottom: 16px;
      font-weight: bold;
    }
    
    ul {
      padding-left: 20px;
      li {
        margin-bottom: 8px;
        line-height: 1.5;
      }
    }
  }
}
</style>