<script setup lang="ts">
// 导入Vben插件与组件
import type { EchartsUIType } from '@vben/plugins/echarts';

import type {
  JobExecutionTime,
  JobExecutionTrend,
  JobStats,
  JobStatusDistribution,
  JobTypeDistribution,
  StatsQueryDto,
} from '../../api/quartz/job';

import { onMounted, ref, shallowRef, watch } from 'vue';

import { Page } from '@vben/common-ui';
import {
  Boxes,
  CircleCheckBig,
  Database,
  RefreshCw,
  Rocket,
} from '@vben/icons';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';
import { usePreferences } from '@vben/preferences';

import {
  Button,
  Card,
  Col,
  Empty,
  message,
  Row,
  Skeleton,
  Tooltip,
} from 'ant-design-vue';

// 导入i18n
import { $t } from '#/locales';

// 导入API和类型
import {
  getJobExecutionTime,
  getJobExecutionTrend,
  getJobStats,
  getJobStatusDistribution,
  getJobTypeDistribution,
  getSchedulerStatus,
} from '../../api/quartz/job';

/**
 * 主题感知：通过 isDark 实现图表在切换主题时自动重渲染
 */
const { isDark } = usePreferences();

/**
 * 状态与数据初始化
 * 使用 shallowRef 优化性能，防止大型图表数据被过度代理
 */
const loading = ref(false);
const executionTrendChartRef = ref<EchartsUIType>();
const executionTimeChartRef = ref<EchartsUIType>();

const { renderEcharts: renderExecutionTrend } = useEcharts(
  executionTrendChartRef,
);
const { renderEcharts: renderExecutionTime } = useEcharts(
  executionTimeChartRef,
);

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
 * 工具：读取 VbenAdmin 设计令牌（HSL 值），返回 ECharts 可用的颜色字符串
 * 读取失败时回退到一组合理的默认值
 */
const readToken = (name: string, fallback: string): string => {
  if (typeof document === 'undefined') return fallback;
  const raw = getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();
  if (!raw) return fallback;
  // 令牌格式 "H S% L%" 或 "H S% L% / A%"
  return raw.includes('/') ? `hsl(${raw})` : `hsl(${raw})`;
};

interface ChartPalette {
  success: string;
  destructive: string;
  primary: string;
  warning: string;
  muted: string;
  border: string;
  foreground: string;
  card: string;
  popover: string;
}

const buildPalette = (): ChartPalette => ({
  success: readToken('--success', 'hsl(144 57% 58%)'),
  destructive: readToken('--destructive', 'hsl(359 68% 56%)'),
  primary: readToken('--primary', 'hsl(211 100% 50%)'),
  warning: readToken('--warning', 'hsl(42 84% 61%)'),
  muted: readToken('--muted-foreground', 'hsl(240 5% 65%)'),
  border: readToken('--border', 'hsl(240 4% 90%)'),
  foreground: readToken('--foreground', 'hsl(240 10% 8%)'),
  card: readToken('--card', 'hsl(0 0% 100%)'),
  popover: readToken('--popover', 'hsl(0 0% 100%)'),
});

/**
 * 将 HSL 颜色转为 rgba 字符串（用于面积渐变末端透明度）
 * 支持 "H S% L%" 与 "H S% L% / A%" 两种格式
 */
const hslToRgba = (hsl: string, alpha: number): string => {
  // 提取 H S L 三个数字
  const match = hsl.match(/hsl\(([^)]+)\)/);
  if (!match || !match[1]) return hsl;
  const parts = match[1].split('/').map((s) => s.trim());
  const nums = (parts[0] ?? '').split(/\s+/).map((n) => Number.parseFloat(n));
  const h = nums[0] ?? 0;
  const s = nums[1] ?? 0;
  const l = nums[2] ?? 0;
  if ([h, s, l].some((n) => Number.isNaN(n))) return hsl;
  // HSL -> RGB
  const sNorm = s / 100;
  const lNorm = l / 100;
  const c = (1 - Math.abs(2 * lNorm - 1)) * sNorm;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = lNorm - c / 2;
  let b = 0;
  let g = 0;
  let r = 0;
  if (h < 60) [r, g, b] = [c, x, 0];
  else if (h < 120) [r, g, b] = [x, c, 0];
  else if (h < 180) [r, g, b] = [0, c, x];
  else if (h < 240) [r, g, b] = [0, x, c];
  else if (h < 300) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];
  const to255 = (v: number) => Math.round((v + m) * 255);
  return `rgba(${to255(r)}, ${to255(g)}, ${to255(b)}, ${alpha})`;
};

/**
 * 图表配置生成器 (抽离配置逻辑，保持 fetch 函数纯粹)
 */
const getExecutionTrendOption = (
  data: JobExecutionTrend[],
): Record<string, any> => {
  const palette = buildPalette();
  const hasData = data.length > 0;

  const tooltipBg = palette.popover;
  const tooltipFg = palette.foreground;
  const tooltipMuted = palette.muted;
  const tooltipBorder = palette.border;

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      backgroundColor: tooltipBg,
      borderColor: tooltipBorder,
      borderWidth: 1,
      padding: [10, 12],
      extraCssText:
        'backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px); box-shadow: 0 6px 16px rgba(0,0,0,0.08); border-radius: 8px;',
      textStyle: { color: tooltipFg, fontSize: 12 },
      formatter: (params: any) => {
        let html = `<div style="margin-bottom: 8px; font-weight: 600; color: ${tooltipFg}; font-size: 13px;">${params[0].axisValue}</div>`;
        params.forEach((item: any) => {
          html += `
            <div style="display: flex; align-items: center; justify-content: space-between; min-width: 140px; margin-bottom: 4px;">
              <span style="font-size: 12px; color: ${tooltipMuted}; display:flex; align-items:center;">
                <span style="display:inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${item.color}; margin-right: 8px;"></span>
                ${item.seriesName}
              </span>
              <span style="font-weight: 600; color: ${tooltipFg}; margin-left: 16px;">${item.value}</span>
            </div>`;
        });
        return html;
      },
    },
    legend: {
      icon: 'roundRect',
      itemWidth: 10,
      itemHeight: 4,
      right: 0,
      top: 0,
      textStyle: { color: palette.muted, fontSize: 12 },
    },
    grid: {
      left: '1%',
      right: '2%',
      bottom: '5%',
      top: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: hasData
        ? data.map((i) => i.time)
        : [$t('page.quartz.analyticsPage.noData')],
      axisLine: { lineStyle: { color: palette.border } },
      axisTick: { show: false },
      axisLabel: { color: palette.muted, fontSize: 11 },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: palette.border, type: 'dashed' } },
      axisLabel: { color: palette.muted, fontSize: 11 },
    },
    series: [
      {
        name: $t('page.quartz.analyticsPage.success'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        symbolSize: 6,
        data: data.map((i) => i.successCount),
        itemStyle: { color: palette.success },
        lineStyle: { width: 2.5 },
        emphasis: { focus: 'series' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: hslToRgba(palette.success, 0.22) },
              { offset: 1, color: hslToRgba(palette.success, 0) },
            ],
          },
        },
      },
      {
        name: $t('page.quartz.analyticsPage.failed'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        symbolSize: 6,
        data: data.map((i) => i.failedCount),
        itemStyle: { color: palette.destructive },
        lineStyle: { width: 2.5 },
        emphasis: { focus: 'series' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: hslToRgba(palette.destructive, 0.22) },
              { offset: 1, color: hslToRgba(palette.destructive, 0) },
            ],
          },
        },
      },
      {
        name: $t('page.quartz.analyticsPage.total'),
        type: 'line',
        smooth: 0.4,
        showSymbol: false,
        symbolSize: 6,
        data: data.map((i) => i.totalCount),
        itemStyle: { color: palette.primary },
        lineStyle: { width: 2, type: 'dashed', opacity: 0.55 },
        emphasis: { focus: 'series' },
      },
    ],
  };
};

const getExecutionTimeOption = (
  data: JobExecutionTime[],
): Record<string, any> => {
  const palette = buildPalette();
  const xAxisData =
    data.length > 0
      ? data.map((i) => i.timeRange)
      : [$t('page.quartz.analyticsPage.noData')];

  const tooltipBg = palette.popover;
  const tooltipFg = palette.foreground;
  const tooltipMuted = palette.muted;
  const tooltipBorder = palette.border;

  // 耗时档位颜色：从快到慢，蓝 -> 绿 -> 黄 -> 红
  const tierColors: string[] = [
    palette.primary,
    palette.success,
    palette.warning,
    palette.destructive,
  ];

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
        shadowStyle: { color: palette.border, opacity: 0.4 },
      },
      backgroundColor: tooltipBg,
      borderColor: tooltipBorder,
      borderWidth: 1,
      padding: [10, 12],
      extraCssText:
        'backdrop-filter: blur(8px); -webkit-backdrop-filter: blur(8px); box-shadow: 0 6px 16px rgba(0,0,0,0.08); border-radius: 8px;',
      textStyle: { color: tooltipFg, fontSize: 12 },
      formatter: (params: any) => {
        const item = params[0];
        return `
          <div style="margin-bottom: 6px; font-weight: 600; color: ${tooltipFg}; font-size: 13px;">${item.axisValue}</div>
          <div style="display: flex; align-items: center; justify-content: space-between; min-width: 120px;">
            <span style="font-size: 12px; color: ${tooltipMuted}; display:flex; align-items:center;">
              <span style="display:inline-block; width: 8px; height: 8px; border-radius: 50%; background: ${item.color}; margin-right: 8px;"></span>
              ${item.seriesName}
            </span>
            <span style="font-weight: 600; color: ${tooltipFg}; margin-left: 16px;">${item.value}</span>
          </div>`;
      },
    },
    grid: {
      left: '1%',
      right: '2%',
      bottom: '5%',
      top: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisLabel: {
        color: palette.muted,
        fontSize: 11,
        rotate: xAxisData.length > 6 ? 30 : 0,
      },
      axisLine: { lineStyle: { color: palette.border } },
      axisTick: { show: false },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { type: 'dashed', color: palette.border } },
      axisLabel: { color: palette.muted, fontSize: 11 },
    },
    series: [
      {
        name: $t('page.quartz.analyticsPage.jobCount'),
        type: 'bar',
        barWidth: 22,
        data: data.map((i) => i.count),
        itemStyle: {
          borderRadius: [6, 6, 0, 0],
          color: (params: any) => {
            const ratio = params.dataIndex / (xAxisData.length - 1 || 1);
            let color: string;
            if (ratio < 0.25) color = tierColors[0] ?? palette.primary;
            else if (ratio < 0.5) color = tierColors[1] ?? palette.success;
            else if (ratio < 0.75) color = tierColors[2] ?? palette.warning;
            else color = tierColors[3] ?? palette.destructive;

            return {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color },
                { offset: 1, color: hslToRgba(color, 0.55) },
              ],
            };
          },
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowColor: hslToRgba(palette.primary, 0.25),
          },
        },
      },
    ],
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
    const [
      statsRes,
      trendRes,
      timeRes,
      schedulerRes,
      statusDistributionRes,
      typeDistributionRes,
    ] = await Promise.all([
      getJobStats(query),
      getJobExecutionTrend(query),
      getJobExecutionTime(query),
      getSchedulerStatus(),
      getJobStatusDistribution(query),
      getJobTypeDistribution(query),
    ]);

    // 更新基础统计 (优先使用 statsRes, schedulerRes 作为补充)
    if (statsRes.success && statsRes.data) {
      statsOverview.value = statsRes.data;
    }
    if (
      schedulerRes.success &&
      schedulerRes.data && // 若总数为空则使用调度器数据
      !statsOverview.value.totalJobs
    )
      statsOverview.value.totalJobs = schedulerRes.data.jobCount || 0;

    // 更新趋势图数据
    jobExecutionTrend.value =
      trendRes?.success && trendRes.data ? trendRes.data : [];
    renderExecutionTrend(getExecutionTrendOption(jobExecutionTrend.value));

    // 更新耗时图数据
    jobExecutionTimeData.value =
      timeRes?.success && timeRes.data ? timeRes.data : [];
    renderExecutionTime(getExecutionTimeOption(jobExecutionTimeData.value));

    // 更新作业状态分布数据
    jobStatusDistribution.value =
      statusDistributionRes?.success && statusDistributionRes.data
        ? statusDistributionRes.data
        : [];

    // 更新作业类型分布数据
    jobTypeDistribution.value =
      typeDistributionRes?.success && typeDistributionRes.data
        ? typeDistributionRes.data
        : [];
  } catch (error) {
    console.error('Data Fetch Error:', error);
    message.error($t('page.quartz.analyticsPage.loadFailed'));
  } finally {
    loading.value = false;
  }
};

/**
 * 主题切换时，使用最新数据重新渲染图表（保证颜色与主题匹配）
 */
watch(isDark, () => {
  if (jobExecutionTrend.value.length > 0) {
    renderExecutionTrend(getExecutionTrendOption(jobExecutionTrend.value));
  }
  if (jobExecutionTimeData.value.length > 0) {
    renderExecutionTime(getExecutionTimeOption(jobExecutionTimeData.value));
  }
});

onMounted(fetchData);

// 派生数据，便于模板使用
const normalCount = () =>
  jobStatusDistribution.value.find((d) => d.status === 'Normal')?.count || 0;
const pausedCount = () =>
  jobStatusDistribution.value.find((d) => d.status === 'Paused')?.count || 0;
const normalPercentage = () =>
  jobStatusDistribution.value.find((d) => d.status === 'Normal')?.percentage ||
  0;

const dllCount = () =>
  jobTypeDistribution.value.find((d) => d.type === 'DLL')?.count || 0;
const apiCount = () =>
  jobTypeDistribution.value.find((d) => d.type === 'API')?.count || 0;
const dllPercentage = () =>
  jobTypeDistribution.value.find((d) => d.type === 'DLL')?.percentage || 0;
const apiPercentage = () =>
  jobTypeDistribution.value.find((d) => d.type === 'API')?.percentage || 0;

const enabledRate = () =>
  (statsOverview.value.enabledJobs / (statsOverview.value.totalJobs || 1)) *
  100;
const successRate = () =>
  (statsOverview.value.successCount /
    (statsOverview.value.totalExecutions || 1)) *
  100;
const typePercentageText = () =>
  `${dllPercentage().toFixed(0)}% / ${apiPercentage().toFixed(0)}%`;
const durationLegendText = () =>
  `${$t('page.quartz.analyticsPage.durationLegend')}：`;
const enabledDisabledText = () =>
  `${statsOverview.value.enabledJobs}/${statsOverview.value.disabledJobs}`;
const successRateText = () => `${successRate().toFixed(1)}%`;
const normalPausedText = () => `${normalCount()}/${pausedCount()}`;

// 耗时档位图例
const durationTiers = [
  {
    color: 'var(--chart-tier-1, hsl(var(--primary)))',
    label: 'page.quartz.analyticsPage.durationFast',
  },
  {
    color: 'var(--chart-tier-2, hsl(var(--success)))',
    label: 'page.quartz.analyticsPage.durationNormal',
  },
  {
    color: 'var(--chart-tier-3, hsl(var(--warning)))',
    label: 'page.quartz.analyticsPage.durationSlow',
  },
  {
    color: 'var(--chart-tier-4, hsl(var(--destructive)))',
    label: 'page.quartz.analyticsPage.durationVerySlow',
  },
];
</script>

<template>
  <Page auto-content-height>
    <!-- 顶部操作栏 -->
    <div class="analytics-toolbar">
      <div class="toolbar-left">
        <span class="toolbar-title">{{ $t('page.quartz.analytics') }}</span>
      </div>
      <div class="toolbar-right">
        <Tooltip :title="$t('page.quartz.analyticsPage.refresh')">
          <Button :loading="loading" @click="fetchData">
            <template #icon>
              <RefreshCw :size="14" :class="{ 'spin-animation': loading }" />
            </template>
            {{ $t('page.quartz.analyticsPage.refresh') }}
          </Button>
        </Tooltip>
      </div>
    </div>

    <!-- 统计概览卡片 -->
    <Row :gutter="[16, 16]" class="stat-row">
      <Col :xs="24" :sm="12" :lg="6">
        <Card
          class="stat-card stat-card--primary"
          :loading="loading"
          :bordered="false"
        >
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{
                $t('page.quartz.analyticsPage.totalJobs')
              }}</span>
              <div class="stat-number-row">
                <span class="stat-number">{{ statsOverview.totalJobs }}</span>
                <small class="stat-unit">{{
                  $t('page.quartz.analyticsPage.unit')
                }}</small>
              </div>
            </div>
            <div class="stat-icon">
              <Boxes :size="20" />
            </div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{
              $t('page.quartz.analyticsPage.enabledDisabled')
            }}</span>
            <span class="sub-value">{{ enabledDisabledText() }}</span>
            <div class="mini-bar-bg">
              <div
                class="mini-bar-fill fill-primary"
                :style="{ width: `${enabledRate()}%` }"
              ></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card
          class="stat-card stat-card--success"
          :loading="loading"
          :bordered="false"
        >
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{
                $t('page.quartz.analyticsPage.totalExecutions')
              }}</span>
              <div class="stat-number-row">
                <span class="stat-number">{{
                  statsOverview.totalExecutions
                }}</span>
                <small class="stat-unit">{{
                  $t('page.quartz.analyticsPage.times')
                }}</small>
              </div>
            </div>
            <div class="stat-icon">
              <Rocket :size="20" />
            </div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{
              $t('page.quartz.analyticsPage.successRate')
            }}</span>
            <span class="sub-value success">{{ successRateText() }}</span>
            <div class="mini-bar-bg">
              <div
                class="mini-bar-fill fill-success"
                :style="{ width: `${successRate()}%` }"
              ></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card
          class="stat-card stat-card--warning"
          :loading="loading"
          :bordered="false"
        >
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{
                $t('page.quartz.analyticsPage.normalRunning')
              }}</span>
              <div class="stat-number-row">
                <span class="stat-number">{{ normalCount() }}</span>
                <small class="stat-unit">{{
                  $t('page.quartz.analyticsPage.unit')
                }}</small>
              </div>
            </div>
            <div class="stat-icon">
              <CircleCheckBig :size="20" />
            </div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{
              $t('page.quartz.analyticsPage.normalPaused')
            }}</span>
            <span class="sub-value">{{ normalPausedText() }}</span>
            <div class="mini-bar-bg">
              <div
                class="mini-bar-fill fill-warning"
                :style="{ width: `${normalPercentage()}%` }"
              ></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card
          class="stat-card stat-card--info"
          :loading="loading"
          :bordered="false"
        >
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{
                $t('page.quartz.analyticsPage.jobTypeDistribution')
              }}</span>
              <div class="dual-numbers">
                <div class="dual-item">
                  <span class="dual-label">DLL</span>
                  <b class="dual-val">{{ dllCount() }}</b>
                </div>
                <div class="dual-divider"></div>
                <div class="dual-item">
                  <span class="dual-label">API</span>
                  <b class="dual-val">{{ apiCount() }}</b>
                </div>
              </div>
            </div>
            <div class="stat-icon">
              <Database :size="20" />
            </div>
          </div>
          <div class="stat-sub">
            <span class="sub-label">{{ typePercentageText() }}</span>
            <div class="mini-bar-bg dual-bg">
              <div
                class="mini-bar-fill fill-info"
                :style="{ width: `${dllPercentage()}%` }"
              ></div>
              <div
                class="mini-bar-fill fill-cyan"
                :style="{ width: `${apiPercentage()}%` }"
              ></div>
            </div>
          </div>
        </Card>
      </Col>
    </Row>

    <!-- 图表区域 -->
    <Row :gutter="[16, 16]" class="chart-row">
      <Col :span="24">
        <Card class="chart-card" :bordered="false">
          <div class="chart-header">
            <div class="chart-title-group">
              <span class="chart-title">{{
                $t('page.quartz.analyticsPage.executionTrend')
              }}</span>
              <span class="chart-subtitle">{{
                $t('page.quartz.analyticsPage.executionTrendDesc')
              }}</span>
            </div>
          </div>
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <Empty
              v-if="jobExecutionTrend.length === 0 && !loading"
              :description="$t('page.quartz.analyticsPage.noAnalyticsData')"
              class="chart-empty"
            />
            <EchartsUI
              v-else
              ref="executionTrendChartRef"
              style="height: 380px"
            />
          </Skeleton>
        </Card>
      </Col>

      <Col :span="24">
        <Card class="chart-card" :bordered="false">
          <div class="chart-header">
            <div class="chart-title-group">
              <span class="chart-title">{{
                $t('page.quartz.analyticsPage.executionTime')
              }}</span>
              <span class="chart-subtitle">{{
                $t('page.quartz.analyticsPage.executionTimeDesc')
              }}</span>
            </div>
            <div class="chart-legend">
              <span class="legend-label">{{ durationLegendText() }}</span>
              <span
                v-for="(tier, idx) in durationTiers"
                :key="idx"
                class="legend-item"
              >
                <span
                  class="legend-dot"
                  :style="{ background: tier.color }"
                ></span>
                {{ $t(tier.label) }}
              </span>
            </div>
          </div>
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <Empty
              v-if="jobExecutionTimeData.length === 0 && !loading"
              :description="$t('page.quartz.analyticsPage.noAnalyticsData')"
              class="chart-empty"
            />
            <EchartsUI
              v-else
              ref="executionTimeChartRef"
              style="height: 380px"
            />
          </Skeleton>
        </Card>
      </Col>
    </Row>
  </Page>
</template>

<style scoped lang="less">
/* --- 顶部工具栏 --- */
.analytics-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;

  .toolbar-title {
    font-size: 16px;
    font-weight: 600;
    color: hsl(var(--foreground));
  }

  .toolbar-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }
}

.spin-animation {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* --- 统计卡片 --- */
.stat-row {
  margin-bottom: 4px;
}

.stat-card {
  position: relative;
  border-radius: 12px;
  background-color: hsl(var(--card));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.04);
  transition: all 0.25s ease;
  overflow: hidden;
  min-height: 148px;
  display: flex;
  flex-direction: column;
  border: 1px solid hsl(var(--border));

  /* 顶部装饰条 */
  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 2px;
    opacity: 0.85;
  }

  &::after {
    content: '';
    position: absolute;
    top: -40px;
    right: -40px;
    width: 120px;
    height: 120px;
    border-radius: 50%;
    opacity: 0.06;
    pointer-events: none;
  }

  &:hover {
    transform: translateY(-2px);
    box-shadow: 0 6px 20px hsl(var(--foreground) / 0.08);
    border-color: hsl(var(--border) / 0.8);
  }

  &--primary {
    &::before {
      background: hsl(var(--primary));
    }
    &::after {
      background: hsl(var(--primary));
    }
    .stat-icon {
      color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.1);
    }
  }
  &--success {
    &::before {
      background: hsl(var(--success));
    }
    &::after {
      background: hsl(var(--success));
    }
    .stat-icon {
      color: hsl(var(--success));
      background: hsl(var(--success) / 0.1);
    }
  }
  &--warning {
    &::before {
      background: hsl(var(--warning));
    }
    &::after {
      background: hsl(var(--warning));
    }
    .stat-icon {
      color: hsl(var(--warning));
      background: hsl(var(--warning) / 0.1);
    }
  }
  &--info {
    &::before {
      background: hsl(var(--primary));
    }
    &::after {
      background: hsl(var(--primary));
    }
    .stat-icon {
      color: hsl(var(--primary));
      background: hsl(var(--primary) / 0.1);
    }
  }

  :deep(.ant-card-body) {
    padding: 16px 20px;
    flex: 1;
    display: flex;
    flex-direction: column;
  }
}

.stat-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
  gap: 12px;
}

.stat-main {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.stat-title {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
  margin-bottom: 8px;
  font-weight: 500;
}

.stat-number-row {
  display: flex;
  align-items: baseline;
  gap: 4px;
}

.stat-number {
  font-size: 26px;
  font-weight: 700;
  color: hsl(var(--foreground));
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.5px;
}

.stat-unit {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  font-weight: 400;
}

/* --- 图标 --- */
.stat-icon {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

/* --- 双数字 (DLL/API) --- */
.dual-numbers {
  display: flex;
  align-items: center;
  gap: 12px;

  .dual-item {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 2px;
  }

  .dual-label {
    font-size: 11px;
    color: hsl(var(--muted-foreground));
    letter-spacing: 0.4px;
    font-weight: 500;
  }

  .dual-val {
    font-size: 22px;
    color: hsl(var(--foreground));
    font-variant-numeric: tabular-nums;
    line-height: 1.1;
  }

  .dual-divider {
    width: 1px;
    height: 28px;
    background: hsl(var(--border));
  }
}

/* --- 副信息 & 进度条 --- */
.stat-sub {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  margin-top: auto;
  padding-top: 8px;
  border-top: 1px dashed hsl(var(--border) / 0.7);
}

.sub-label {
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.sub-value {
  font-weight: 600;
  min-width: 50px;
  text-align: right;
  color: hsl(var(--foreground));
  font-variant-numeric: tabular-nums;

  &.success {
    color: hsl(var(--success));
  }
}

.mini-bar-bg {
  flex: 1;
  height: 5px;
  background: hsl(var(--muted) / 0.6);
  border-radius: 3px;
  overflow: hidden;
  display: flex;

  &.dual-bg {
    gap: 2px;
  }
}

.mini-bar-fill {
  height: 100%;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
  border-radius: 3px;

  &.fill-primary {
    background-color: hsl(var(--primary));
  }
  &.fill-success {
    background-color: hsl(var(--success));
  }
  &.fill-warning {
    background-color: hsl(var(--warning));
  }
  &.fill-info {
    background-color: hsl(var(--primary));
  }
  &.fill-cyan {
    background-color: hsl(var(--primary) / 0.6);
  }
}

/* --- 图表卡片 --- */
.chart-row {
  margin-top: 4px;
}

.chart-card {
  border-radius: 12px;
  background-color: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.03);

  :deep(.ant-card-body) {
    padding: 16px 20px 12px;
  }
}

.chart-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
  padding-bottom: 12px;
  border-bottom: 1px solid hsl(var(--border) / 0.6);
}

.chart-title-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
  position: relative;
  padding-left: 10px;

  &::before {
    content: '';
    position: absolute;
    left: 0;
    top: 2px;
    bottom: 4px;
    width: 3px;
    border-radius: 2px;
    background: hsl(var(--primary));
  }
}

.chart-title {
  font-size: 15px;
  font-weight: 600;
  color: hsl(var(--foreground));
  line-height: 1.4;
}

.chart-subtitle {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  line-height: 1.4;
}

.chart-legend {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  font-size: 11px;

  .legend-label {
    color: hsl(var(--muted-foreground));
  }

  .legend-item {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    color: hsl(var(--foreground) / 0.75);
  }

  .legend-dot {
    display: inline-block;
    width: 8px;
    height: 8px;
    border-radius: 2px;
  }
}

.chart-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 380px;
}

/* --- 响应式适配 --- */
@media (max-width: 768px) {
  .chart-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .stat-card {
    :deep(.ant-card-body) {
      padding: 14px 16px;
    }
  }

  .stat-number {
    font-size: 22px;
  }
}
</style>
