<script setup lang="ts">
import { ref, shallowRef, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Skeleton } from 'ant-design-vue';
import type { EChartsOption } from 'echarts';

// 导入Vben插件与组件
import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

// 导入i18n
import { $t } from '#/locales';

// 导入API和类型
import {
  getSchedulerStatus,
  getJobStats,
  getJobExecutionTrend,
  getJobExecutionTime,
  getJobStatusDistribution,
  getJobTypeDistribution,
} from '../../api/quartz/job';
import type {
  JobStats,
  JobExecutionTrend,
  JobExecutionTime,
  StatsQueryDto,
  JobStatusDistribution,
  JobTypeDistribution,
} from '../../api/quartz/job';

/**
 * 状态与数据初始化
 * 使用 shallowRef 优化性能，防止大型图表数据被过度代理
 */
const loading = ref(false);
const executionTrendChartRef = ref<EchartsUIType | null>(null);
const executionTimeChartRef = ref<EchartsUIType | null>(null);

const { renderEcharts: renderExecutionTrend } = useEcharts(executionTrendChartRef);
const { renderEcharts: renderExecutionTime } = useEcharts(executionTimeChartRef);

const statsOverview = ref<JobStats>({
  totalJobs: 0,
  enabledJobs: 0,
  disabledJobs: 0,
  totalExecutions: 0,
  successCount: 0,
  failedCount: 0,
});

// 使用 shallowRef 存储数组数据
const jobExecutionTrend = shallowRef<JobExecutionTrend[]>([]);
const jobExecutionTimeData = shallowRef<JobExecutionTime[]>([]);
const jobStatusDistribution = shallowRef<JobStatusDistribution[]>([]);
const jobTypeDistribution = shallowRef<JobTypeDistribution[]>([]);

/**
 * 图表配置生成器 (抽离配置逻辑，保持 fetch 函数纯粹)
 */
const getExecutionTrendOption = (data: JobExecutionTrend[]): EChartsOption => {
  const hasData = data.length > 0;
  const colors = {
    success: { line: '#52c41a', area: 'rgba(82, 196, 26, 0.1)' },
    failed: { line: '#ff4d4f', area: 'rgba(255, 77, 79, 0.1)' },
    total: { line: '#1890ff', area: 'rgba(24, 144, 255, 0.05)' }
  };

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      extraCssText: 'backdrop-filter: blur(4px); box-shadow: 0 4px 12px rgba(0,0,0,0.1);',
      formatter: (params: any) => {
        let html = `<div style="margin-bottom: 8px; font-weight: 500; color: #595959">${params[0].axisValue}</div>`;
        params.forEach((item: any) => {
          html += `
            <div style="display: flex; align-items: center; justify-content: space-between; min-width: 120px; margin-bottom: 4px;">
              <span style="font-size: 13px; color: #8c8c8c">
                <span style="display:inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${item.color}; margin-right: 8px;"></span>
                ${item.seriesName}
              </span>
              <span style="font-weight: 600; color: #262626;">${item.value}</span>
            </div>`;
        });
        return html;
      }
    },
    legend: { icon: 'rect', itemWidth: 10, itemHeight: 4, right: 0, top: 0 },
    grid: { left: '1%', right: '2%', bottom: '5%', top: '15%', containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: hasData ? data.map(i => i.time) : [$t('page.quartz.analyticsPage.noData')],
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { color: '#8c8c8c' }
    },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f5f5f5' } } },
    series: [
      {
        name: $t('page.quartz.analyticsPage.success'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        data: data.map(i => i.successCount),
        itemStyle: { color: colors.success.line },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [{ offset: 0, color: colors.success.area }, { offset: 1, color: 'transparent' }]
          }
        }
      },
      {
        name: $t('page.quartz.analyticsPage.failed'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        data: data.map(i => i.failedCount),
        itemStyle: { color: colors.failed.line },
        areaStyle: {
          color: {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [{ offset: 0, color: colors.failed.area }, { offset: 1, color: 'transparent' }]
          }
        }
      },
      {
        name: $t('page.quartz.analyticsPage.total'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        data: data.map(i => i.totalCount),
        itemStyle: { color: colors.total.line },
        lineStyle: { width: 2, type: 'dashed', opacity: 0.5 }
      }
    ]
  };
};

const getExecutionTimeOption = (data: JobExecutionTime[]): EChartsOption => {
  const xAxisData = data.length > 0 ? data.map(i => i.timeRange) : [$t('page.quartz.analyticsPage.noData')];
  // 检测是否为暗色模式，用于调整文字颜色
  const isDark = document.documentElement.classList.contains('dark');

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      extraCssText: 'backdrop-filter: blur(4px);'
    },
    grid: { left: '1%', right: '2%', bottom: '5%', top: '15%', containLabel: true },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisLabel: {
        color: isDark ? 'rgba(255,255,255,0.45)' : '#8c8c8c',
        rotate: xAxisData.length > 6 ? 30 : 0
      },
      axisLine: { lineStyle: { color: isDark ? '#303030' : '#f0f0f0' } }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { type: 'dashed', color: isDark ? '#303030' : '#f5f5f5' } },
      axisLabel: { color: isDark ? 'rgba(255,255,255,0.45)' : '#8c8c8c' }
    },
    series: [{
      name: $t('page.quartz.analyticsPage.jobCount'),
      type: 'bar',
      barWidth: 22,
      data: data.map(i => i.count),
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: (params: any) => {
          // 根据数据索引或耗时档位计算"紧张程度"
          // 假设 xAxisData 是按耗时从小到大排列的
          const ratio = params.dataIndex / (xAxisData.length - 1 || 1);

          let color;
          if (ratio < 0.25) {
            color = '#1890ff'; // 蓝色 - 极速
          } else if (ratio < 0.5) {
            color = '#52c41a'; // 绿色 - 正常
          } else if (ratio < 0.75) {
            color = '#faad14'; // 黄色 - 偏慢
          } else {
            color = '#ff4d4f'; // 红色 - 极慢（紧张感）
          }

          return {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: color },           // 顶部亮色
              { offset: 1, color: color + '99' }     // 底部带透明度，增加通透感
            ]
          };
        }
      }
    }]
  };
};

/**
 * 业务逻辑：获取并渲染数据
 */
const fetchData = async () => {
  loading.value = true;
  const query: StatsQueryDto = { timeRangeType: 'last30Days' };

  try {
    // 并行请求，提高加载速度
    const [statsRes, trendRes, timeRes, schedulerRes, statusDistributionRes, typeDistributionRes] = await Promise.all([
      getJobStats(query),
      getJobExecutionTrend(query),
      getJobExecutionTime(query),
      getSchedulerStatus(),
      getJobStatusDistribution(query),
      getJobTypeDistribution(query)
    ]);

    // 更新基础统计 (优先使用 statsRes, schedulerRes 作为补充)
    if (statsRes.success) {
      statsOverview.value = statsRes.data;
    }
    if (schedulerRes.success) {
      // 若总数为空则使用调度器数据
      if (!statsOverview.value.totalJobs) statsOverview.value.totalJobs = schedulerRes.data.jobCount || 0;
    }

    // 更新趋势图数据
    jobExecutionTrend.value = trendRes?.success ? trendRes.data : [];
    renderExecutionTrend(getExecutionTrendOption(jobExecutionTrend.value));

    // 更新耗时图数据
    jobExecutionTimeData.value = timeRes?.success ? timeRes.data : [];
    renderExecutionTime(getExecutionTimeOption(jobExecutionTimeData.value));

    // 更新作业状态分布数据
    jobStatusDistribution.value = statusDistributionRes?.success ? statusDistributionRes.data : [];

    // 更新作业类型分布数据
    jobTypeDistribution.value = typeDistributionRes?.success ? typeDistributionRes.data : [];

  } catch (error) {
    console.error('Data Fetch Error:', error);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchData);
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[20, 20]">
      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.totalJobs') }}</span>
              <span class="stat-number">{{ statsOverview.totalJobs }}<small>{{ $t('page.quartz.analyticsPage.unit') }}</small></span>
            </div>
            <div class="stat-icon blue">🗂️</div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{ $t('page.quartz.analyticsPage.enabledDisabled') }}</span>
            <span class="sub-value">{{ statsOverview.enabledJobs }}/{{ statsOverview.disabledJobs }}</span>
            <div class="mini-bar-bg">
              <div class="mini-bar-fill blue"
                :style="{ width: (statsOverview.enabledJobs / (statsOverview.totalJobs || 1)) * 100 + '%' }"></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.totalExecutions') }}</span>
              <span class="stat-number">{{ statsOverview.totalExecutions }}<small>{{ $t('page.quartz.analyticsPage.times') }}</small></span>
            </div>
            <div class="stat-icon green">🚀</div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{ $t('page.quartz.analyticsPage.successRate') }}</span>
            <span class="sub-value success">{{ ((statsOverview.successCount / (statsOverview.totalExecutions || 1)) *
              100).toFixed(1) }}%</span>
            <div class="mini-bar-bg">
              <div class="mini-bar-fill green"
                :style="{ width: (statsOverview.successCount / (statsOverview.totalExecutions || 1)) * 100 + '%' }">
              </div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.normalRunning') }}</span>
              <span class="stat-number">{{jobStatusDistribution.find(d => d.status === 'Normal')?.count || 0
                }}<small>{{ $t('page.quartz.analyticsPage.unit') }}</small></span>
            </div>
            <div class="stat-icon orange">💗</div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{ $t('page.quartz.analyticsPage.normalPaused') }}</span>
            <span class="sub-value">
              {{jobStatusDistribution.find(d => d.status === 'Normal')?.count || 0}}/{{jobStatusDistribution.find(d =>
                d.status === 'Paused')?.count || 0}}
            </span>
            <div class="mini-bar-bg">
              <div class="mini-bar-fill orange"
                :style="{ width: (jobStatusDistribution.find(d => d.status === 'Normal')?.percentage || 0) + '%' }">
              </div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.jobTypeDistribution') }}</span>
              <div class="dual-numbers">
                <span class="dll-val">DLL <b>{{jobTypeDistribution.find(d => d.type === 'DLL')?.count || 0
                    }}</b></span>
                <span class="api-val">API <b>{{jobTypeDistribution.find(d => d.type === 'API')?.count || 0
                    }}</b></span>
              </div>
            </div>
            <div class="stat-icon purple">🗃</div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{(jobTypeDistribution.find(d => d.type === 'DLL')?.percentage || 0).toFixed(0)
              }}%</span>
            <div class="mini-bar-bg dual-bg">
              <div class="mini-bar-fill purple"
                :style="{ width: (jobTypeDistribution.find(d => d.type === 'DLL')?.percentage || 0) + '%' }"></div>
              <div class="mini-bar-fill cyan"
                :style="{ width: (jobTypeDistribution.find(d => d.type === 'API')?.percentage || 0) + '%' }"></div>
            </div>
            <span class="sub-label">{{(jobTypeDistribution.find(d => d.type === 'API')?.percentage || 0).toFixed(0)
              }}%</span>
          </div>
        </Card>
      </Col>

      <Col :span="24">
        <Card :title="$t('page.quartz.analyticsPage.executionTrend')" class="chart-card">
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="executionTrendChartRef" style="height: 400px" />
          </Skeleton>
        </Card>
      </Col>

      <Col :span="24">
        <Card :title="$t('page.quartz.analyticsPage.executionTime')" class="chart-card">
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="executionTimeChartRef" style="height: 400px" />
          </Skeleton>
        </Card>
      </Col>
    </Row>
  </Page>
</template>

<style scoped>
/* --- 1. 基础卡片样式 (适配暗色/浅色) --- */
.stat-card {
  border-radius: 12px;
  background-color: #ffffff;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.03);
  transition: all 0.3s ease;
  overflow: hidden;
  min-height: 145px;
  display: flex;
  flex-direction: column;
  border: 1px solid #f0f0f0;
}

/* 暗色模式卡片底色 */
:where(.dark) .stat-card {
  background-color: #1f1f1f !important;
  border-color: #303030 !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.stat-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 8px;
  padding: 4px;
}

.stat-main {
  display: flex;
  flex-direction: column;
  flex: 1;
}

/* 文字颜色适配 */
.stat-title {
  color: #8c8c8c;
  font-size: 13px;
  margin-bottom: 6px;
}

:where(.dark) .stat-title {
  color: rgba(255, 255, 255, 0.45);
}

.stat-number {
  font-size: 24px;
  font-weight: 700;
  color: #262626;
}

:where(.dark) .stat-number {
  color: rgba(255, 255, 255, 0.85);
}

.stat-number small {
  font-size: 12px;
  color: #bfbfbf;
  margin-left: 4px;
  font-weight: normal;
}

/* DLL/API 核心数值 */
.dual-numbers {
  display: flex;
  gap: 12px;
}

.dll-val b {
  font-size: 24px;
  color: #722ed1;
  margin-left: 4px;
}

:where(.dark) .dll-val b {
  color: #9254de;
}

.api-val b {
  font-size: 24px;
  color: #13c2c2;
  margin-left: 4px;
}

:where(.dark) .api-val b {
  color: #14e1e1;
}

/* --- 2. 图标背景适配 --- */
.stat-icon {
  width: 42px;
  height: 42px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}

.stat-icon.blue {
  background: #e6f7ff;
}

.stat-icon.green {
  background: #f6ffed;
}

.stat-icon.orange {
  background: #fff7e6;
}

.stat-icon.purple {
  background: #f9f0ff;
}

:where(.dark) .stat-icon.blue {
  background: rgba(24, 144, 255, 0.15);
}

:where(.dark) .stat-icon.green {
  background: rgba(82, 196, 26, 0.15);
}

:where(.dark) .stat-icon.orange {
  background: rgba(250, 173, 20, 0.15);
}

:where(.dark) .stat-icon.purple {
  background: rgba(114, 46, 209, 0.15);
}

/* --- 3. 进度条核心修复 (重点) --- */
.stat-sub {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  margin-top: auto;
  padding-top: 8px;
}

.sub-label {
  color: #bfbfbf;
  white-space: nowrap;
}

.sub-value {
  font-weight: 600;
  min-width: 45px;
  text-align: right;
  color: #595959;
}

:where(.dark) .sub-value {
  color: rgba(255, 255, 255, 0.65);
}

.mini-bar-bg {
  flex: 1;
  height: 6px;
  background: #f5f5f5;
  /* 浅色模式背景 */
  border-radius: 3px;
  overflow: hidden;
  display: flex;
}

/* 暗色模式下进度条槽的颜色 */
:where(.dark) .mini-bar-bg {
  background: #333333 !important;
}

.mini-bar-fill {
  height: 100%;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

/* 强制指定填充颜色，防止被暗色选择器覆盖 */
.mini-bar-fill.blue {
  background-color: #1890ff !important;
}

.mini-bar-fill.green {
  background-color: #52c41a !important;
}

.mini-bar-fill.orange {
  background-color: #faad14 !important;
}

.mini-bar-fill.purple {
  background-color: #722ed1 !important;
  border-right: 1px solid #fff;
}

.mini-bar-fill.cyan {
  background-color: #13c2c2 !important;
}

/* 暗色模式下，DLL的白色分割线也要变深 */
:where(.dark) .mini-bar-fill.purple {
  border-right-color: #1f1f1f;
}
</style>
