<script setup lang="ts">
import { ref, shallowRef, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Skeleton } from 'ant-design-vue';
import type { EChartsOption } from 'echarts';

// 导入Vben插件与组件
import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

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
      data: hasData ? data.map(i => i.time) : ['无数据'],
      axisLine: { lineStyle: { color: '#f0f0f0' } },
      axisLabel: { color: '#8c8c8c' }
    },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f5f5f5' } } },
    series: [
      {
        name: '成功',
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
        name: '失败',
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
        name: '总数',
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
  const xAxisData = data.length > 0 ? data.map(i => i.timeRange) : ['无数据'];
  return {
    backgroundColor: 'transparent',
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: '1%', right: '2%', bottom: '5%', top: '15%', containLabel: true },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisLabel: { color: '#8c8c8c', rotate: xAxisData.length > 6 ? 30 : 0 }
    },
    yAxis: { type: 'value', splitLine: { lineStyle: { type: 'dashed', color: '#f5f5f5' } } },
    series: [{
      name: '作业数量',
      type: 'bar',
      barWidth: 22,
      data: data.map(i => i.count),
      itemStyle: {
        borderRadius: [4, 4, 0, 0],
        color: (params: any) => {
          const ratio = params.dataIndex / (xAxisData.length - 1 || 1);
          const color = ratio > 0.7 ? '#ff4d4f' : ratio > 0.4 ? '#faad14' : '#1890ff';
          return {
            type: 'linear', x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [{ offset: 0, color: color }, { offset: 1, color: color + '99' }]
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
            <span class="stat-title">总作业规模</span>
            <span class="stat-number">{{ statsOverview.totalJobs }}</span>
          </div>
          <div class="stat-icon blue">📊</div>
        </div>
        <div class="stat-sub">
          <span class="sub-label">启用率</span>
          <span class="sub-value">{{ ((statsOverview.enabledJobs / (statsOverview.totalJobs || 1)) * 100).toFixed(0) }}%</span>
          <div class="mini-bar-bg">
            <div class="mini-bar-fill blue" :style="{ width: (statsOverview.enabledJobs / (statsOverview.totalJobs || 1)) * 100 + '%' }"></div>
          </div>
        </div>
      </Card>
    </Col>

    <Col :xs="24" :sm="12" :lg="6">
      <Card class="stat-card" :loading="loading" :bordered="false">
        <div class="stat-content">
          <div class="stat-main">
            <span class="stat-title">累计执行量</span>
            <span class="stat-number">{{ statsOverview.totalExecutions }}</span>
          </div>
          <div class="stat-icon green">⚡</div>
        </div>
        <div class="stat-sub">
          <span class="sub-label">成功率</span>
          <span class="sub-value success">{{ ((statsOverview.successCount / (statsOverview.totalExecutions || 1)) * 100).toFixed(1) }}%</span>
          <div class="mini-bar-bg">
            <div class="mini-bar-fill green" :style="{ width: (statsOverview.successCount / (statsOverview.totalExecutions || 1)) * 100 + '%' }"></div>
          </div>
        </div>
      </Card>
    </Col>

    <Col :xs="24" :sm="12" :lg="6">
      <Card class="stat-card" :loading="loading" :bordered="false">
        <div class="stat-content">
          <div class="stat-main">
            <span class="stat-title">正常运行数</span>
            <span class="stat-number">{{ jobStatusDistribution.find(d => d.status === 'Normal')?.count || 0 }}</span>
          </div>
          <div class="stat-icon orange">🛡️</div>
        </div>
        <div class="stat-sub">
          <span class="sub-label">正常占比</span>
          <span class="sub-value">{{ (jobStatusDistribution.find(d => d.status === 'Normal')?.percentage || 0).toFixed(1) }}%</span>
          <div class="mini-bar-bg">
            <div class="mini-bar-fill orange" :style="{ width: (jobStatusDistribution.find(d => d.status === 'Normal')?.percentage || 0) + '%' }"></div>
          </div>
        </div>
      </Card>
    </Col>

    <Col :xs="24" :sm="12" :lg="6">
      <Card class="stat-card" :loading="loading" :bordered="false">
        <div class="stat-content">
          <div class="stat-main">
            <span class="stat-title">API 调度数</span>
            <span class="stat-number">{{ jobTypeDistribution.find(d => d.type === 'API')?.count || 0 }}</span>
          </div>
          <div class="stat-icon purple">🔌</div>
        </div>
        <div class="stat-sub">
          <span class="sub-label">API 占比</span>
          <span class="sub-value">{{ (jobTypeDistribution.find(d => d.type === 'API')?.percentage || 0).toFixed(1) }}%</span>
          <div class="mini-bar-bg">
            <div class="mini-bar-fill purple" :style="{ width: (jobTypeDistribution.find(d => d.type === 'API')?.percentage || 0) + '%' }"></div>
          </div>
        </div>
      </Card>
    </Col>

      <Col :span="24">
        <Card title="近30天作业执行趋势" class="chart-card">
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="executionTrendChartRef" style="height: 400px" />
          </Skeleton>
        </Card>
      </Col>

      <Col :span="24">
        <Card title="近30天作业执行耗时" class="chart-card">
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="executionTimeChartRef" style="height: 400px" />
          </Skeleton>
        </Card>
      </Col>
    </Row>
  </Page>
</template>

<style scoped>


/* 图表卡片样式 */
:deep(.ant-card-head) { 
  border-bottom: none; 
  padding: 0 20px;
}

:deep(.ant-card-head-title) { 
  font-size: 15px; 
  font-weight: 600; 
}
</style>

<style scoped>
.stat-card {
  border-radius: 12px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.03);
  transition: all 0.3s ease;
  overflow: hidden;
  background: #fff;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.06);
}

.stat-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.stat-main {
  display: flex;
  flex-direction: column;
}

.stat-title {
  color: #8c8c8c;
  font-size: 13px;
  margin-bottom: 4px;
}

.stat-number {
  font-size: 24px;
  font-weight: 700;
  color: #262626;
  font-family: 'Inter', -apple-system, sans-serif;
}

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}

/* 风格统一的主题色 */
.stat-icon.blue { background: #e6f7ff; }
.stat-icon.green { background: #f6ffed; }
.stat-icon.orange { background: #fff7e6; }
.stat-icon.purple { background: #f9f0ff; }

/* 辅助信息与微缩进度条 */
.stat-sub {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #595959;
}

.sub-label {
  color: #bfbfbf;
}

.sub-value {
  font-weight: 600;
  min-width: 35px;
}

.sub-value.success { color: #52c41a; }

.mini-bar-bg {
  flex: 1;
  height: 4px;
  background: #f0f0f0;
  border-radius: 2px;
  overflow: hidden;
}

.mini-bar-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

.mini-bar-fill.blue { background: #1890ff; }
.mini-bar-fill.green { background: #52c41a; }
.mini-bar-fill.orange { background: #faad14; }
.mini-bar-fill.purple { background: #722ed1; }
</style>