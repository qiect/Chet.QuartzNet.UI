<script setup lang="ts">
import { ref, onMounted } from 'vue';
// 导入日期格式化工具
import { formatDateTime } from '@vben/utils';
import { Page } from '@vben/common-ui';
import {
  Button,
  Card,
  Row,
  Col,
  Space,
  Statistic,
  // SyncOutlined,
  Skeleton,
} from 'ant-design-vue';
import type { EChartsOption } from 'echarts';

// 导入Vben集成的ECharts组件
import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

// 导入作业API服务
import {
  getSchedulerStatus,
  getJobStats,
  getJobExecutionTrend,
  getJobExecutionTime,
} from '../../api/quartz/job';
import type {
  JobStats,
  JobExecutionTrend,
  JobExecutionTime,
  StatsQueryDto,
} from '../../api/quartz/job';

// 作业类型和状态映射
const jobStatusMap = {
  0: { text: '正常', status: 'success' },
  1: { text: '已暂停', status: 'error' },
  2: { text: '已完成', status: 'default' },
  3: { text: '错误', status: 'error' },
  4: { text: '阻塞', status: 'warning' },
};

// 响应式数据
const loading = ref(false);

// 统计概览数据
const statsOverview = ref<JobStats>({
  totalJobs: 0,
  enabledJobs: 0,
  disabledJobs: 0,
  executingJobs: 0,
  successCount: 0,
  failedCount: 0,
  pausedCount: 0,
  blockedCount: 0,
});

const jobExecutionTrend = ref<JobExecutionTrend[]>([]);
const jobExecutionTimeData = ref<JobExecutionTime[]>([]);



// Vben ECharts组件引用
const executionTrendChartRef = ref<EchartsUIType | null>(null);
const executionTimeChartRef = ref<EchartsUIType | null>(null);

// 使用Vben ECharts组合式函数
const { renderEcharts: renderExecutionTrend } = useEcharts(executionTrendChartRef);
const { renderEcharts: renderExecutionTime } = useEcharts(executionTimeChartRef);


// 获取统计数据
const fetchStatsData = async () => {
  loading.value = true;
  try {
    // 构建查询参数
    const query: StatsQueryDto = {
      timeRangeType: 'last30Days', // 默认使用近30天
    };

    // 获取作业统计数据
    const statsResponse = await getJobStats(query);
    if (statsResponse.success && statsResponse.data) {
      statsOverview.value = statsResponse.data as JobStats;
    }

    // 获取作业执行趋势数据
    const executionTrendResponse = await getJobExecutionTrend(query);
    if (executionTrendResponse && executionTrendResponse.success && executionTrendResponse.data) {
      jobExecutionTrend.value = executionTrendResponse.data as JobExecutionTrend[];
    } else {
      jobExecutionTrend.value = [];
    }

    // 获取作业执行耗时数据
    const executionTimeResponse = await getJobExecutionTime(query);
    if (executionTimeResponse && executionTimeResponse.success && executionTimeResponse.data) {
      jobExecutionTimeData.value = executionTimeResponse.data as JobExecutionTime[];
    } else {
      jobExecutionTimeData.value = [];
    }
    // 渲染图表
    renderAllCharts();
  } catch (error) {
    console.error('获取统计数据失败:', error);
  } finally {
    loading.value = false;
  }
};



// 作业执行趋势图表配置
const getExecutionTrendChartOption = (): EChartsOption => {
  const hasData = jobExecutionTrend.value.length > 0;
  const xAxisData = hasData ? jobExecutionTrend.value.map(item => item.time) : ['暂无数据'];

  // 定义统一配色
  const colors = {
    success: { line: '#52c41a', area: 'rgba(82, 196, 26, 0.1)' },
    failed: { line: '#ff4d4f', area: 'rgba(255, 77, 79, 0.1)' },
    total: { line: '#1890ff', area: 'rgba(24, 144, 255, 0.05)' }
  };

  return {
    backgroundColor: 'transparent', // 允许背景通透
    title: {
      left: 0, // 改为左对齐更符合现代仪表盘布局
      textStyle: {
        fontSize: 16,
        fontWeight: 500,
        color: '#1f1f1f',
      },
    },
    tooltip: {
      trigger: 'axis',
      padding: 12,
      backgroundColor: 'rgba(255, 255, 255, 0.9)',
      borderColor: '#f0f0f0',
      borderWidth: 1,
      borderRadius: 8,
      shadowBlur: 10,
      shadowColor: 'rgba(0,0,0,0.05)',
      extraCssText: 'backdrop-filter: blur(4px);', // 现代毛玻璃效果
      axisPointer: {
        lineStyle: {
          color: '#d9d9d9',
          width: 1,
          type: 'dashed'
        }
      },
      formatter: (params: any) => {
        let html = `<div style="margin-bottom: 8px; font-weight: 500; color: #595959">${params[0].axisValue}</div>`;
        params.forEach((item: any) => {
          html += `
            <div style="display: flex; align-items: center; justify-content: space-between; min-width: 120px; margin-bottom: 4px;">
              <span style="display: flex; align-items: center; font-size: 13px; color: #8c8c8c">
                <span style="width: 8px; height: 8px; border-radius: 50%; background: ${item.color}; margin-right: 8px;"></span>
                ${item.seriesName}
              </span>
              <span style="font-weight: 600; color: #262626; margin-left: 12px;">${item.value}</span>
            </div>`;
        });
        return html;
      }
    },
    legend: {
      icon: 'rect', // 使用矩形图标更显现代
      itemWidth: 10,
      itemHeight: 4,
      right: 0,
      top: 0,
      textStyle: { color: '#8c8c8c' }
    },
    grid: {
      left: '0%',
      right: '2%',
      bottom: '5%',
      top: '18%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: xAxisData,
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { color: '#8c8c8c', fontSize: 12, margin: 12 },
      axisTick: { show: false }
    },
    yAxis: {
      type: 'value',
      splitNumber: 4, // 减少刻度线，视觉更清爽
      axisLabel: { color: '#8c8c8c' },
      splitLine: {
        lineStyle: {
          color: '#f5f5f5',
          type: 'solid' // 也可以用 dashed
        }
      }
    },
    series: [
      {
        name: '成功',
        type: 'line',
        smooth: 0.4, // 适度的平滑度
        showSymbol: false, // 默认隐藏圆点，hover 时显示
        data: hasData ? jobExecutionTrend.value.map(item => item.successCount) : [0],
        itemStyle: { color: colors.success.line },
        lineStyle: { width: 3 },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: colors.success.area },
              { offset: 1, color: 'transparent' }
            ]
          }
        }
      },
      {
        name: '失败',
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        data: hasData ? jobExecutionTrend.value.map(item => item.failedCount) : [0],
        itemStyle: { color: colors.failed.line },
        lineStyle: { width: 3 },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: colors.failed.area },
              { offset: 1, color: 'transparent' }
            ]
          }
        }
      },
      {
        name: '总数',
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        data: hasData ? jobExecutionTrend.value.map(item => item.totalCount) : [0],
        itemStyle: { color: colors.total.line },
        lineStyle: { width: 2, type: 'dashed', opacity: 0.6 }, // 总数建议用细虚线弱化
      }
    ]
  };
};

// 作业执行耗时统计图表配置
const getExecutionTimeChartOption = (): EChartsOption => {
  // 1. 保持原字段 jobExecutionTimeData
  const chartData = jobExecutionTimeData.value || [];
  const hasData = chartData.length > 0;

  // 2. 保持原字段 timeRange 和 count
  const xAxisData = hasData ? chartData.map(item => item.timeRange) : ['暂无数据'];
  const seriesData = hasData ? chartData.map(item => item.count) : [0];

  return {
    backgroundColor: 'transparent',
    title: {
      left: 0,
      textStyle: {
        fontSize: 16,
        fontWeight: '600',
        color: '#262626',
      },
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow', shadowStyle: { color: 'rgba(0, 0, 0, 0.02)' } },
      backgroundColor: 'rgba(255, 255, 255, 0.98)',
      borderColor: '#f0f0f0',
      borderWidth: 1,
      borderRadius: 8,
      padding: 12,
      extraCssText: 'box-shadow: 0 4px 12px rgba(0,0,0,0.1);',
      formatter: function(params: any) {
        const param = params[0];
        // 动态匹配提示框的小圆点颜色
        const color = param.color.colorStops ? param.color.colorStops[0].color : param.color;
        return `
          <div style="color: #8c8c8c; font-size: 12px; margin-bottom: 4px;">时长区间</div>
          <div style="display: flex; align-items: center; justify-content: space-between;">
            <div style="display: flex; align-items: center;">
              <div style="width: 8px; height: 8px; background-color: ${color}; border-radius: 50%; margin-right: 8px;"></div>
              <span style="font-weight: bold; color: #595959;">${param.axisValueLabel}</span>
            </div>
            <span style="margin-left: 20px; font-weight: 600; color: #262626;">${param.value} 个</span>
          </div>`;
      },
    },
    grid: {
      left: '0%',
      right: '2%',
      bottom: '5%',
      top: '18%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: {
        color: '#8c8c8c',
        fontSize: 12,
        margin: 12,
        rotate: xAxisData.length > 5 ? 30 : 0,
      },
    },
    yAxis: {
      type: 'value',
      name: '(数量)',
      nameTextStyle: { color: '#8c8c8c', align: 'right', padding: [0, 5, 0, 0] },
      axisLine: { show: false },
      axisTick: { show: false },
      splitLine: { lineStyle: { color: '#f5f5f5', type: 'dashed' } },
      axisLabel: { color: '#8c8c8c' },
    },
    series: [
      {
        name: '作业数量',
        type: 'bar',
        data: seriesData,
        barWidth: 20, // 调细柱子，更清新
        showBackground: true, // 增加背景槽，更有设计感
        backgroundStyle: {
          color: 'rgba(0, 0, 0, 0.02)',
          borderRadius: [4, 4, 0, 0],
        },
        itemStyle: {
          borderRadius: [4, 4, 0, 0],
          // 核心：根据数据索引动态计算颜色（从左到右：绿 -> 黄 -> 红）
          color: function (params: any) {
            const index = params.dataIndex;
            const total = xAxisData.length;
            const ratio = index / (total - 1 || 1);

            // 定义颜色的阈值
            let topColor = '#52c41a'; // 默认绿色
            if (ratio > 0.4 && ratio <= 0.7) {
              topColor = '#faad14'; // 中间黄色
            } else if (ratio > 0.7) {
              topColor = '#ff4d4f'; // 右侧红色（紧迫）
            } else if (ratio > 0) {
              topColor = '#1890ff'; // 较短时长蓝色
            }

            return {
              type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
              colorStops: [
                { offset: 0, color: topColor }, // 顶部亮色
                { offset: 1, color: topColor + '99' } // 底部稍透明
              ]
            };
          },
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(0,0,0,0.1)',
          },
        },
        label: {
          show: true,
          position: 'top',
          color: '#bfbfbf',
          fontSize: 11,
          formatter: (p: any) => (p.value > 0 ? p.value : ''),
        },
        animationDuration: 1000,
        animationEasing: 'cubicOut',
      },
    ],
  };
};

// 渲染所有图表
const renderAllCharts = () => {
  try {
    renderExecutionTrend(getExecutionTrendChartOption());
    renderExecutionTime(getExecutionTimeChartOption());
  } catch (error) {
    console.error('渲染图表时发生错误:', error);
  }
};

// 获取调度器状态
const getSchedulerStatusInfo = async () => {
  try {
    const response = await getSchedulerStatus();
    if (response.success && response.data) {
      const schedulerData = response.data as { jobCount?: number; executingJobCount?: number };
      // 更新统计概览数据
      statsOverview.value = {
        totalJobs: schedulerData.jobCount || 0,
        enabledJobs: Math.floor((schedulerData.jobCount || 0) * 0.8), // 临时数据，后续会被fetchStatsData覆盖
        disabledJobs: Math.floor((schedulerData.jobCount || 0) * 0.2), // 临时数据，后续会被fetchStatsData覆盖
        executingJobs: schedulerData.executingJobCount || 0,
        successCount: 0,
        failedCount: 0,
        pausedCount: 0,
        blockedCount: 0,
      };
    }
  } catch (error) {
    console.error('获取调度器状态失败:', error);
  }
};



// 刷新数据
const handleRefresh = async () => {
  await fetchStatsData();
};

// 生命周期
onMounted(async () => {
  // 获取调度器状态
  await getSchedulerStatusInfo();

  // 获取统计数据
  await fetchStatsData();
});
</script>

<template>
  <Page auto-content-height>

    <!-- 图表展示区 -->
    <Row :gutter="[24, 24]">
      <!-- 统计概览卡片 -->
      <Col :xs="24" :sm="12" :md="12" :lg="6" :xl="6">
      <Card hoverable class="statistic-card" :loading="loading">
        <div class="statistic-content" v-if="!loading">
          <div class="statistic-prefix">📊</div>
          <div class="statistic-info">
            <div class="statistic-title">总作业数</div>
            <div class="statistic-value">{{ statsOverview.totalJobs }}</div>
          </div>
        </div>
        <template v-else>
          <Skeleton active :paragraph="{ rows: 1 }" />
        </template>
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="12" :lg="6" :xl="6">
      <Card hoverable class="statistic-card" :loading="loading">
        <div class="statistic-content" v-if="!loading">
          <div class="statistic-prefix">✅</div>
          <div class="statistic-info">
            <div class="statistic-title">启用作业数</div>
            <div class="statistic-value">{{ statsOverview.enabledJobs }}</div>
          </div>
        </div>
        <template v-else>
          <Skeleton active :paragraph="{ rows: 1 }" />
        </template>
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="12" :lg="6" :xl="6">
      <Card hoverable class="statistic-card" :loading="loading">
        <div class="statistic-content" v-if="!loading">
          <div class="statistic-prefix">❌</div>
          <div class="statistic-info">
            <div class="statistic-title">禁用作业数</div>
            <div class="statistic-value">{{ statsOverview.disabledJobs }}</div>
          </div>
        </div>
        <template v-else>
          <Skeleton active :paragraph="{ rows: 1 }" />
        </template>
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="12" :lg="6" :xl="6">
      <Card hoverable class="statistic-card" :loading="loading">
        <div class="statistic-content" v-if="!loading">
          <div class="statistic-prefix">⏳</div>
          <div class="statistic-info">
            <div class="statistic-title">正在执行</div>
            <div class="statistic-value">{{ statsOverview.executingJobs }}</div>
          </div>
        </div>
        <template v-else>
          <Skeleton active :paragraph="{ rows: 1 }" />
        </template>
      </Card>
      </Col>

      <!-- 作业执行趋势 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24" :xl="24">
      <Card title="近30天作业执行趋势" :loading="loading" class="chart-card">
        <EchartsUI ref="executionTrendChartRef" :style="{ height: '400px' }" />
      </Card>
      </Col>



      <!-- 作业执行耗时统计 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24" :xl="24">
      <Card title="近30天作业执行耗时" :loading="loading" class="chart-card">
        <EchartsUI ref="executionTimeChartRef" :style="{ height: '400px' }" />
      </Card>
      </Col>
    </Row>
  </Page>
</template>

<style scoped>
/* VbenAdmin 风格样式优化 */
.mb-4 {
  margin-bottom: 16px;
}

.mt-4 {
  margin-top: 16px;
}

.text-right {
  text-align: right;
}

/* 统计卡片样式 */
.statistic-card {
  border-radius: 12px;
  overflow: hidden;
  transition: all 0.3s ease;
  box-shadow: 0 2px 8px var(--color-shadow);
  border: 1px solid var(--color-border);
}

.statistic-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 6px 16px var(--color-shadow-dark);
  border-color: var(--color-border-hover);
}

.statistic-content {
  display: flex;
  align-items: center;
  gap: 16px;
}

.statistic-prefix {
  font-size: 28px;
  line-height: 1;
}

.statistic-info {
  flex: 1;
}

.statistic-title {
  font-size: 14px;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
  font-weight: 500;
}

.statistic-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.2;
}

/* 图表卡片样式 */
.chart-card {
  border-radius: 12px;
  overflow: hidden;
  transition: all 0.3s ease;
  box-shadow: 0 2px 8px var(--color-shadow);
  border: 1px solid var(--color-border);
}

.chart-card:hover {
  box-shadow: 0 6px 16px var(--color-shadow-dark);
}

/* 卡片标题样式 */
:deep(.ant-card-head) {
  border-bottom: 1px solid var(--color-border);
  padding: 0 24px;
}

:deep(.ant-card-head-title) {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
  padding: 16px 0;
}

:deep(.ant-card-body) {
  padding: 24px;
}

/* 页面整体间距 */
:deep(.vben-page) {
  padding: 24px;
}

/* 过滤器卡片样式 */
.filter-card {
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  border: 1px solid #f0f0f0;
  background: linear-gradient(135deg, #ffffff 0%, #fafafa 100%);
}

/* 在较小屏幕上调整间距 */
@media (max-width: 768px) {
  :deep(.vben-page) {
    padding: 16px;
  }
  
  :deep(.ant-card-body) {
    padding: 16px;
  }
  
  .statistic-content {
    gap: 12px;
  }
  
  .statistic-title {
    font-size: 12px;
  }
  
  .statistic-value {
    font-size: 20px;
  }
  
  .filter-card :deep(.ant-space) {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .filter-card :deep(.ant-space-item) {
    width: 100%;
  }
  
  .filter-card :deep(.ant-select) {
    width: 100%;
  }
  
  .filter-card :deep(.ant-picker) {
    width: 100%;
  }
}
</style>
