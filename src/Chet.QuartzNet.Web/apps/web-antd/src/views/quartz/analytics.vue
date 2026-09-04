<script setup lang="ts">
import { ref, shallowRef, onMounted, computed, h, watch } from 'vue';
import { Page } from '@vben/common-ui';
import {
  Card,
  Row,
  Col,
  Skeleton,
  Table,
  Tag,
} from 'ant-design-vue';
import type { EChartsOption } from 'echarts';

import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { $t } from '#/locales';
import { useI18n } from '@vben/locales';
import { usePreferences } from '@vben/preferences';

import {
  getAnalyticsOverview,
  getAnalyticsJobPerformance,
} from '../../api/quartz/analytics';
import type {
  JobStats,
  JobExecutionTrend,
  JobHealth,
  JobExecutionHeatmap,
  TopSlowJob,
  StatsQueryDto,
  JobStatusDistribution,
} from '../../api/quartz/analytics';
import { useSystemConfig } from '../../composables/use-system-config';

const loading = ref(false);
const { locale } = useI18n();
const { isDark } = usePreferences();
const trendChartRef = ref<EchartsUIType | null>(null);
const healthChartRef = ref<EchartsUIType | null>(null);
const heatmapChartRef = ref<EchartsUIType | null>(null);

const { renderEcharts: renderTrend } = useEcharts(trendChartRef);
const {
  renderEcharts: renderHealth,
  getChartInstance: getHealthInstance,
} = useEcharts(healthChartRef);
const { renderEcharts: renderHeatmap } = useEcharts(heatmapChartRef);

const overviewChartRef = ref<EchartsUIType | null>(null);
const activityChartRef = ref<EchartsUIType | null>(null);
const qualityChartRef = ref<EchartsUIType | null>(null);
const durationChartRef = ref<EchartsUIType | null>(null);

const { renderEcharts: renderOverview } = useEcharts(overviewChartRef);
const { renderEcharts: renderActivity } = useEcharts(activityChartRef);
const { renderEcharts: renderQuality } = useEcharts(qualityChartRef);
const { renderEcharts: renderDuration } = useEcharts(durationChartRef);

const statsOverview = ref<JobStats>({
  totalJobs: 0,
  enabledJobs: 0,
  disabledJobs: 0,
  totalExecutions: 0,
  successCount: 0,
  failedCount: 0,
});

const trendData = shallowRef<JobExecutionTrend[]>([]);
const jobHealthData = shallowRef<JobHealth[]>([]);
const heatmapData = shallowRef<JobExecutionHeatmap[]>([]);
const topSlowData = shallowRef<TopSlowJob[]>([]);
const jobStatusDistribution = shallowRef<JobStatusDistribution[]>([]);

const normalCount = computed(
  () => jobStatusDistribution.value.find((d) => d.status === 'Normal')?.count || 0,
);
const pausedCount = computed(
  () => jobStatusDistribution.value.find((d) => d.status === 'Paused')?.count || 0,
);
const enabledRatio = computed(() =>
  (statsOverview.value.enabledJobs / (statsOverview.value.totalJobs || 1)) * 100,
);
const dailyTrendData = computed(() => {
  const map = new Map<string, { time: string; successCount: number; failedCount: number; totalCount: number }>();
  for (const item of trendData.value) {
    const day = item.time.slice(0, 10);
    const existing = map.get(day);
    if (existing) {
      existing.successCount += item.successCount;
      existing.failedCount += item.failedCount;
      existing.totalCount += item.totalCount;
    } else {
      map.set(day, {
        time: day,
        successCount: item.successCount,
        failedCount: item.failedCount,
        totalCount: item.totalCount,
      });
    }
  }
  return Array.from(map.values()).sort((a, b) => a.time.localeCompare(b.time));
});
const recent7Data = computed(() => dailyTrendData.value.slice(-7));
const recent7TotalExecutions = computed(() =>
  recent7Data.value.reduce((sum, d) => sum + d.totalCount, 0),
);
const recent7DailyAvg = computed(() => {
  const n = recent7Data.value.length;
  return n > 0 ? Number((recent7TotalExecutions.value / n).toFixed(1)) : 0;
});
const recent7SuccessCount = computed(() =>
  recent7Data.value.reduce((sum, d) => sum + d.successCount, 0),
);
const recent7FailedCount = computed(() =>
  recent7Data.value.reduce((sum, d) => sum + d.failedCount, 0),
);
const recent7SuccessRate = computed(() => {
  const total = recent7TotalExecutions.value;
  return total > 0 ? Number(((recent7SuccessCount.value / total) * 100).toFixed(1)) : 0;
});
const avgDurationValue = computed(() => {
  const data = jobHealthData.value;
  if (data.length === 0) return 0;
  const totalExec = data.reduce((sum, d) => sum + d.executionCount, 0);
  if (totalExec === 0) return 0;
  return data.reduce((sum, d) => sum + d.avgDuration * d.executionCount, 0) / totalExec;
});
const p99Duration = computed(() => {
  const data = jobHealthData.value;
  if (data.length === 0) return 0;
  return Math.max(...data.map((d) => d.maxDuration));
});
const slowestJobName = computed(() => {
  const data = jobHealthData.value;
  if (data.length === 0) return '-';
  const sorted = [...data].sort((a, b) => b.avgDuration - a.avgDuration);
  return sorted[0]?.jobName ?? '-';
});

const trendSummary = ref({
  recent7Avg: 0,
  prev7Avg: 0,
  changePercent: 0,
  anomalyCount: 0,
});

const getTrendOption = (data: JobExecutionTrend[]): EChartsOption => {
  const dates = data.map((d) => d.time);
  const successValues = data.map((d) => d.successCount);
  const failedValues = data.map((d) => d.failedCount);
  const totalValues = data.map((d) => d.totalCount);
  const successRateValues = data.map((d) =>
    d.totalCount > 0 ? Number(((d.successCount / d.totalCount) * 100).toFixed(1)) : 0,
  );

  const detectTimeGranularity = (times: string[]): 'day' | 'hour' | 'minute' | 'unknown' => {
    if (times.length === 0) return 'unknown';
    const sample = times[0]!;
    if (/^\d{4}-\d{2}-\d{2}$/.test(sample)) return 'day';
    if (/^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}$/.test(sample) || /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(sample)) {
      if (times.length >= 2) {
        const t1 = new Date(times[0]!).getTime();
        const t2 = new Date(times[1]!).getTime();
        const diffMs = Math.abs(t2 - t1);
        if (diffMs <= 60 * 60 * 1000) return 'hour';
        if (diffMs <= 24 * 60 * 60 * 1000) return 'day';
      }
      return 'hour';
    }
    return 'unknown';
  };

  const granularity = detectTimeGranularity(dates);
  const periodLabel = granularity === 'day'
    ? $t('page.quartz.analyticsPage.dayOverDay')
    : granularity === 'hour'
      ? $t('page.quartz.analyticsPage.hourOverHour')
      : $t('page.quartz.analyticsPage.periodOverPeriod');

  const n = totalValues.length;
  const avgTotal = n > 0 ? Number((totalValues.reduce((a, b) => a + b, 0) / n).toFixed(1)) : 0;

  const movingAvg = (arr: number[], window: number): number[] =>
    arr.map((_, i) => {
      const start = Math.max(0, i - window + 1);
      const slice = arr.slice(start, i + 1);
      return Number((slice.reduce((a, b) => a + b, 0) / slice.length).toFixed(1));
    });

  const ma7Values = movingAvg(totalValues, 7);

  const failedMean = n > 0 ? failedValues.reduce((a, b) => a + b, 0) / n : 0;
  const failedStd = n > 0
    ? Math.sqrt(failedValues.reduce((sum, v) => sum + (v - failedMean) ** 2, 0) / n)
    : 0;
  const anomalyThreshold = failedMean + 2 * failedStd;

  const anomalyPoints: { coord: [number, number]; value: number }[] = [];
  for (let i = 0; i < n; i++) {
    if (failedValues[i]! > anomalyThreshold && failedValues[i]! > 0) {
      anomalyPoints.push({ coord: [i, totalValues[i]!], value: failedValues[i]! });
    }
  }

  const dailyTotalValues = dailyTrendData.value.map((d) => d.totalCount);
  const recent7 = dailyTotalValues.slice(-7);
  const prev7 = dailyTotalValues.slice(-14, -7);
  const recent7Avg = recent7.length > 0 ? recent7.reduce((a, b) => a + b, 0) / recent7.length : 0;
  const prev7Avg = prev7.length > 0 ? prev7.reduce((a, b) => a + b, 0) / prev7.length : 0;
  const changePercent = prev7Avg > 0 ? Number((((recent7Avg - prev7Avg) / prev7Avg) * 100).toFixed(1)) : 0;

  trendSummary.value = {
    recent7Avg: Number(recent7Avg.toFixed(1)),
    prev7Avg: Number(prev7Avg.toFixed(1)),
    changePercent,
    anomalyCount: anomalyPoints.length,
  };

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      borderWidth: 0,
      padding: [10, 14],
      textStyle: { fontSize: 12, color: '#595959' },
      extraCssText: 'backdrop-filter: blur(8px); box-shadow: 0 6px 16px rgba(0,0,0,0.08);',
      formatter: (params: any[]) => {
        if (!params || params.length === 0) return '';
        const idx = params[0]!.dataIndex;
        const date = params[0]!.axisValue;
        let html = `<div style="font-weight:600;color:#262626;font-size:13px;margin-bottom:6px;">${date}</div>`;
        for (const p of params) {
          if (p.seriesName === $t('page.quartz.analyticsPage.successRate')) {
            html += `<div style="font-size:12px;line-height:20px;"><span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:${p.color};margin-right:6px;"></span>${p.seriesName}: <b style="color:#262626;">${p.value}%</b></div>`;
          } else if (p.seriesName === $t('page.quartz.analyticsPage.ma7')) {
            html += `<div style="font-size:12px;line-height:20px;"><span style="display:inline-block;width:8px;height:3px;border-radius:1px;background:${p.color};margin-right:6px;"></span>${p.seriesName}: <b style="color:#262626;">${p.value}</b></div>`;
          } else {
            html += `<div style="font-size:12px;line-height:20px;"><span style="display:inline-block;width:8px;height:3px;border-radius:1px;background:${p.color};margin-right:6px;"></span>${p.seriesName}: <b style="color:#262626;">${p.value}</b></div>`;
          }
        }
        if (idx > 0 && idx < totalValues.length) {
          const prev = totalValues[idx - 1]!;
          const curr = totalValues[idx]!;
          if (prev > 0) {
            const pct = (((curr - prev) / prev) * 100).toFixed(1);
            const arrow = Number(pct) >= 0 ? '↑' : '↓';
            const clr = Number(pct) >= 0 ? '#52c41a' : '#ff4d4f';
            html += `<div style="font-size:11px;color:#8c8c8c;margin-top:4px;border-top:1px dashed #e8e8e8;padding-top:4px;">${periodLabel}: <span style="color:${clr};font-weight:500;">${arrow}${Math.abs(Number(pct))}%</span></div>`;
          }
        }
        return html;
      },
    },
    legend: {
      data: [
        $t('page.quartz.analyticsPage.total'),
        $t('page.quartz.analyticsPage.ma7'),
        $t('page.quartz.analyticsPage.success'),
        $t('page.quartz.analyticsPage.failed'),
        $t('page.quartz.analyticsPage.successRate'),
      ],
      right: 20,
      top: 0,
      itemWidth: 16,
      itemHeight: 3,
      textStyle: { fontSize: 12, color: '#8c8c8c' },
    },
    grid: { left: 50, right: 50, bottom: 30, top: 36 },
    xAxis: {
      type: 'category',
      data: dates,
      boundaryGap: false,
      axisLabel: { color: '#8c8c8c', fontSize: 11, rotate: dates.length > 15 ? 45 : 0 },
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#e8e8e8' } },
    },
    yAxis: [
      {
        type: 'value',
        axisLabel: { color: '#8c8c8c', fontSize: 11 },
        splitLine: { show: false },
        axisLine: { show: false },
        axisTick: { show: false },
      },
      {
        type: 'value',
        min: 0,
        max: 100,
        axisLabel: { color: '#8c8c8c', fontSize: 11, formatter: (v: number) => `${v}%` },
        splitLine: { show: false },
        axisLine: { show: false },
        axisTick: { show: false },
      },
    ],
    series: [
      {
        name: $t('page.quartz.analyticsPage.total'),
        type: 'line',
        data: totalValues,
        smooth: true,
        symbol: 'circle',
        symbolSize: 6,
        showSymbol: false,
        lineStyle: { width: 2.5, color: '#1890ff' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(24,144,255,0.12)' },
              { offset: 1, color: 'rgba(24,144,255,0.01)' },
            ],
          },
        },
        itemStyle: { color: '#1890ff' },
        markLine: {
          silent: true,
          symbol: 'none',
          lineStyle: { color: '#1890ff', type: 'dashed', width: 1, opacity: 0.4 },
          label: {
            formatter: `{c}`,
            fontSize: 10,
            color: '#8c8c8c',
          },
          data: [{ yAxis: avgTotal, name: $t('page.quartz.analyticsPage.avgLine') }],
        },
        markPoint: anomalyPoints.length > 0 ? {
          symbol: 'pin',
          symbolSize: 30,
          itemStyle: { color: '#ff4d4f' },
          label: {
            show: true,
            formatter: () => `!`,
            fontSize: 11,
            fontWeight: 700,
            color: '#fff',
          },
          data: anomalyPoints,
          tooltip: {
            formatter: (params: any) => {
              const idx = params.coord?.[0] ?? params.dataIndex;
              const dateStr = typeof idx === 'number' && dates[idx] ? dates[idx] : '';
              const failVal = typeof idx === 'number' && failedValues[idx] ? failedValues[idx] : params.value;
              return `<b style="color:#ff4d4f;">${$t('page.quartz.analyticsPage.anomalyDay')}</b><br/>
                <span style="color:#8c8c8c;">${dateStr}</span><br/>
                <span style="color:#ff4d4f;">${$t('page.quartz.analyticsPage.failed')}: <b>${failVal}</b></span><br/>
                <span style="color:#8c8c8c;">${$t('page.quartz.analyticsPage.threshold')}: <b>${anomalyThreshold.toFixed(1)}</b></span>`;
            },
          },
        } : undefined,
      },
      {
        name: $t('page.quartz.analyticsPage.ma7'),
        type: 'line',
        data: ma7Values,
        smooth: true,
        symbol: 'none',
        showSymbol: false,
        lineStyle: { width: 1.5, color: '#1890ff', type: 'dashed', opacity: 0.5 },
        itemStyle: { color: '#1890ff' },
      },
      {
        name: $t('page.quartz.analyticsPage.success'),
        type: 'line',
        data: successValues,
        smooth: true,
        symbol: 'circle',
        symbolSize: 6,
        showSymbol: false,
        lineStyle: { width: 2, color: '#52c41a' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(82,196,26,0.08)' },
              { offset: 1, color: 'rgba(82,196,26,0.01)' },
            ],
          },
        },
        itemStyle: { color: '#52c41a' },
      },
      {
        name: $t('page.quartz.analyticsPage.failed'),
        type: 'line',
        data: failedValues,
        smooth: true,
        symbol: 'circle',
        symbolSize: 6,
        showSymbol: false,
        lineStyle: { width: 2, color: '#ff4d4f' },
        itemStyle: { color: '#ff4d4f' },
      },
      {
        name: $t('page.quartz.analyticsPage.successRate'),
        type: 'line',
        yAxisIndex: 1,
        data: successRateValues,
        smooth: true,
        symbol: 'diamond',
        symbolSize: 5,
        showSymbol: false,
        lineStyle: { width: 1.5, color: '#722ed1', type: 'dashed' },
        itemStyle: { color: '#722ed1' },
      },
    ],
  };
};

const getHealthOption = (data: JobHealth[]): EChartsOption => {
  const statusColorMap: Record<string, string> = {
    Normal: '#1677ff',
    Paused: '#faad14',
    Completed: '#13c2c2',
    Error: '#ff4d4f',
    Blocked: '#8c8c8c',
  };

  const statusGradientMap: Record<string, { from: string; to: string }> = {
    Normal: { from: '#69b1ff', to: '#1677ff' },
    Paused: { from: '#ffd666', to: '#faad14' },
    Completed: { from: '#5cdbd3', to: '#13c2c2' },
    Error: { from: '#ff7875', to: '#ff4d4f' },
    Blocked: { from: '#bfbfbf', to: '#8c8c8c' },
  };

  const maxExecCount = Math.max(...data.map((d) => d.executionCount), 1);

  const sortedDurations = [...data.map((d) => d.avgDuration)].sort((a, b) => a - b);
  const medianDuration = sortedDurations.length > 0
    ? sortedDurations[Math.floor(sortedDurations.length / 2)]
    : 0;

  const successThreshold = 80;
  const durationThreshold = medianDuration || 1;

  const getQuadrant = (successRate: number, avgDuration: number) => {
    if (successRate >= successThreshold && avgDuration <= durationThreshold)
      return { key: 'healthy', color: '#52c41a', bg: 'rgba(82,196,26,0.08)' };
    if (successRate >= successThreshold && avgDuration > durationThreshold)
      return { key: 'slow', color: '#faad14', bg: 'rgba(250,173,20,0.08)' };
    if (successRate < successThreshold && avgDuration <= durationThreshold)
      return { key: 'unstable', color: '#fa8c16', bg: 'rgba(250,140,22,0.08)' };
    return { key: 'critical', color: '#ff4d4f', bg: 'rgba(255,77,79,0.08)' };
  };

  const quadrantLabelMap: Record<string, string> = {
    healthy: $t('page.quartz.analyticsPage.quadrantHealthy'),
    slow: $t('page.quartz.analyticsPage.quadrantSlow'),
    unstable: $t('page.quartz.analyticsPage.quadrantUnstable'),
    critical: $t('page.quartz.analyticsPage.quadrantCritical'),
  };

  const sortedData = [...data].sort((a, b) => b.executionCount - a.executionCount);

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      borderWidth: 0,
      padding: [10, 14],
      textStyle: { fontSize: 12, color: '#595959' },
      extraCssText: 'backdrop-filter: blur(8px); box-shadow: 0 6px 16px rgba(0,0,0,0.08);',
      formatter: (params: any) => {
        const d = params.data;
        if (!d) return '';
        const dur = formatDuration(d.avgDuration);
        const enabledText = d.isEnabled
          ? $t('page.quartz.analyticsPage.enabled')
          : $t('page.quartz.analyticsPage.disabled');
        const q = getQuadrant(d.successRate, d.avgDuration);
        const quadrant = quadrantLabelMap[q.key];
        return `
          <div style="font-weight:600;color:#262626;font-size:13px;margin-bottom:6px;">${d.jobName}</div>
          <div style="display:inline-block;padding:1px 8px;border-radius:3px;font-size:11px;font-weight:500;color:${q.color};background:${q.color}14;margin-bottom:4px;">${quadrant}</div>
          <div style="color:#8c8c8c;font-size:12px;line-height:20px;">
            ${$t('page.quartz.analyticsPage.jobGroup')}: ${d.jobGroup}<br/>
            ${$t('page.quartz.analyticsPage.jobStatus')}: ${enabledText}<br/>
            ${$t('page.quartz.analyticsPage.jobHealthSuccessRate')}: <b style="color:#262626;">${d.successRate}%</b><br/>
            ${$t('page.quartz.analyticsPage.jobHealthAvgDuration')}: <b style="color:#262626;">${dur}</b><br/>
            ${$t('page.quartz.analyticsPage.jobHealthExecutionCount')}: <b style="color:#262626;">${d.executionCount}</b>
          </div>`;
      },
    },
    grid: { left: 60, right: 30, bottom: 50, top: 30 },
    xAxis: {
      name: $t('page.quartz.analyticsPage.jobHealthSuccessRate') + ' (%)',
      nameLocation: 'middle',
      nameGap: 30,
      type: 'value',
      min: 0,
      max: 100,
      splitLine: { show: false },
      axisLabel: { color: '#8c8c8c', fontSize: 12 },
    },
    yAxis: {
      name: $t('page.quartz.analyticsPage.jobHealthAvgDuration'),
      nameLocation: 'middle',
      nameGap: 50,
      type: 'value',
      axisLabel: {
        color: '#8c8c8c',
        fontSize: 12,
        formatter: (v: number) => formatDuration(v),
      },
      splitLine: { show: false },
    },
    graphic: [
      {
        type: 'text',
        right: 36,
        bottom: 12,
        style: {
          text: $t('page.quartz.analyticsPage.quadrantHealthy'),
          fill: '#52c41a',
          fontSize: 10,
          fontWeight: 600,
          opacity: 0.85,
          backgroundColor: 'rgba(82,196,26,0.10)',
          padding: [3, 8, 3, 8],
          borderRadius: 4,
        },
      },
      {
        type: 'text',
        right: 36,
        top: 10,
        style: {
          text: $t('page.quartz.analyticsPage.quadrantSlow'),
          fill: '#faad14',
          fontSize: 10,
          fontWeight: 600,
          opacity: 0.85,
          backgroundColor: 'rgba(250,173,20,0.10)',
          padding: [3, 8, 3, 8],
          borderRadius: 4,
        },
      },
      {
        type: 'text',
        left: 68,
        bottom: 12,
        style: {
          text: $t('page.quartz.analyticsPage.quadrantUnstable'),
          fill: '#fa8c16',
          fontSize: 10,
          fontWeight: 600,
          opacity: 0.85,
          backgroundColor: 'rgba(250,140,22,0.10)',
          padding: [3, 8, 3, 8],
          borderRadius: 4,
        },
      },
      {
        type: 'text',
        left: 68,
        top: 10,
        style: {
          text: $t('page.quartz.analyticsPage.quadrantCritical'),
          fill: '#ff4d4f',
          fontSize: 10,
          fontWeight: 600,
          opacity: 0.85,
          backgroundColor: 'rgba(255,77,79,0.10)',
          padding: [3, 8, 3, 8],
          borderRadius: 4,
        },
      },
    ],
    series: [
      {
        type: 'scatter',
        symbolSize: (val: number[], params: any) => {
          const count = params.data.executionCount;
          return Math.max(8, Math.min(40, (count / maxExecCount) * 40));
        },
        data: sortedData.map((d) => {
          const gradient = statusGradientMap[d.status] || { from: '#bfbfbf', to: '#8c8c8c' };
          const q = getQuadrant(d.successRate, d.avgDuration);
          return {
            value: [d.successRate, d.avgDuration],
            jobName: d.jobName,
            jobGroup: d.jobGroup,
            status: d.status,
            isEnabled: d.isEnabled,
            successRate: d.successRate,
            avgDuration: d.avgDuration,
            executionCount: d.executionCount,
            itemStyle: {
              color: {
                type: 'radial',
                x: 0.3,
                y: 0.3,
                r: 1,
                colorStops: [
                  { offset: 0, color: gradient.from },
                  { offset: 1, color: gradient.to },
                ],
              },
              opacity: d.isEnabled ? 0.88 : 0.28,
              shadowBlur: d.isEnabled ? 8 : 0,
              shadowColor: 'rgba(0,0,0,0.12)',
              shadowOffsetY: 2,
              borderColor: d.isEnabled ? 'rgba(255,255,255,0.8)' : 'rgba(255,255,255,0.3)',
              borderWidth: 1.5,
            },
            label: { show: false },
          };
        }),
        emphasis: {
          scale: 1.6,
          focus: 'self',
          itemStyle: {
            shadowBlur: 20,
            shadowColor: 'rgba(0,0,0,0.25)',
            shadowOffsetY: 4,
            borderColor: '#fff',
            borderWidth: 2.5,
          },
        },
        selectedMode: 'single',
        select: {
          itemStyle: {
            borderColor: '#1890ff',
            borderWidth: 2.5,
            shadowBlur: 16,
            shadowColor: 'rgba(24,144,255,0.35)',
          },
        },
        markArea: {
          silent: true,
          data: [
            [
              {
                xAxis: successThreshold,
                yAxis: 0,
                itemStyle: { color: 'rgba(82,196,26,0.06)' },
                label: { show: false },
              },
              { xAxis: 100, yAxis: durationThreshold },
            ],
            [
              {
                xAxis: successThreshold,
                yAxis: durationThreshold,
                itemStyle: { color: 'rgba(250,173,20,0.06)' },
                label: { show: false },
              },
              { xAxis: 100 },
            ],
            [
              {
                xAxis: 0,
                yAxis: 0,
                itemStyle: { color: 'rgba(250,140,22,0.06)' },
                label: { show: false },
              },
              { xAxis: successThreshold, yAxis: durationThreshold },
            ],
            [
              {
                xAxis: 0,
                yAxis: durationThreshold,
                itemStyle: { color: 'rgba(255,77,79,0.06)' },
                label: { show: false },
              },
              { xAxis: successThreshold },
            ],
          ],
        },
        markLine: {
          silent: true,
          symbol: 'none',
          lineStyle: { color: '#d9d9d9', type: 'dashed', width: 1 },
          label: { show: false },
          data: [
            { xAxis: successThreshold },
            { yAxis: durationThreshold },
          ],
        },
      },
    ],
  };
};

const getHeatmapOption = (data: JobExecutionHeatmap[]): EChartsOption => {
  const now = new Date();
  const currentDayOfWeek = now.getDay() === 0 ? 7 : now.getDay();
  const currentHour = now.getHours();

  const days = [
    $t('page.quartz.analyticsPage.dayMon'),
    $t('page.quartz.analyticsPage.dayTue'),
    $t('page.quartz.analyticsPage.dayWed'),
    $t('page.quartz.analyticsPage.dayThu'),
    $t('page.quartz.analyticsPage.dayFri'),
    $t('page.quartz.analyticsPage.daySat'),
    $t('page.quartz.analyticsPage.daySun'),
  ];
  const hours = Array.from({ length: 24 }, (_, i) => `${i}`);

  const maxCount = Math.max(...data.map((d) => d.count), 1);

  const currentMomentBlueStops = ['#e6f7ff', '#bae7ff', '#91d5ff', '#69c0ff', '#40a9ff'];
  const getCurrentMomentColor = (count: number): string => {
    const ratio = Math.min(count / maxCount, 1);
    const idx = Math.min(Math.floor(ratio * currentMomentBlueStops.length), currentMomentBlueStops.length - 1);
    return currentMomentBlueStops[idx];
  };

  const heatmapValues = data.map((d) => {
    const isCurrentMoment = d.dayOfWeek === currentDayOfWeek && d.hour === currentHour;
    const base: any = { value: [d.hour, d.dayOfWeek - 1, d.count] };
    if (isCurrentMoment) {
      base.itemStyle = {
        color: getCurrentMomentColor(d.count),
        borderColor: '#ffffff',
        borderWidth: 1.5,
        borderType: 'dashed',
      };
    }
    return base;
  });

  const yAxisData = days.map((name, i) =>
    i === currentDayOfWeek - 1 ? `{today|●}${name}` : name,
  );

  return {
    backgroundColor: 'transparent',
    tooltip: {
      borderWidth: 0,
      padding: [10, 14],
      textStyle: { fontSize: 12, color: '#595959' },
      extraCssText: 'backdrop-filter: blur(8px); box-shadow: 0 6px 16px rgba(0,0,0,0.08);',
      formatter: (params: any) => {
        const d = data.find(
          (item) => item.dayOfWeek === params.value[1] + 1 && item.hour === params.value[0],
        );
        if (!d) return '';
        const dayName = days[params.value[1]];
        const isCurrentMoment = params.value[1] + 1 === currentDayOfWeek && params.value[0] === currentHour;
        const todayBadge = isCurrentMoment
          ? ` <span style="display:inline-block;font-size:10px;color:#1890ff;background:#e6f7ff;padding:0 4px;border-radius:2px;margin-left:4px;">${$t('page.quartz.analyticsPage.todayTag')}</span>`
          : '';
        return `<b style="color:#262626;">${dayName} ${params.value[0]}:00${todayBadge}</b><br/>
          <span style="color:#8c8c8c;">${$t('page.quartz.analyticsPage.heatmapExec')}: <b style="color:#262626;">${d.count}</b> ${$t('page.quartz.analyticsPage.times')}</span><br/>
          <span style="color:#52c41a;">${$t('page.quartz.analyticsPage.success')}: <b>${d.successCount}</b></span> /
          <span style="color:#ff4d4f;">${$t('page.quartz.analyticsPage.failed')}: <b>${d.failedCount}</b></span>`;
      },
    },
    grid: { left: 50, right: 20, bottom: 40, top: 10 },
    xAxis: {
      type: 'category',
      data: hours,
      splitArea: { show: true, areaStyle: { color: ['rgba(0,0,0,0.02)', 'transparent'] } },
      axisLabel: { color: '#8c8c8c', fontSize: 11, interval: 1 },
      axisTick: { show: false },
      axisLine: { show: false },
    },
    yAxis: {
      type: 'category',
      data: yAxisData,
      splitArea: { show: true, areaStyle: { color: ['rgba(0,0,0,0.02)', 'transparent'] } },
      axisLabel: {
        color: '#8c8c8c',
        fontSize: 11,
        rich: {
          today: { color: '#1890ff', fontSize: 10, padding: [0, 4, 0, 0] },
        },
      },
      axisTick: { show: false },
      axisLine: { show: false },
    },
    visualMap: {
      min: 0,
      max: maxCount,
      calculable: false,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      inRange: {
        color: ['#fff1f0', '#ffa39e', '#ff7875', '#ff4d4f', '#cf1322'],
      },
      textStyle: { color: '#8c8c8c', fontSize: 10 },
      show: true,
    },
    series: [
      {
        type: 'heatmap',
        data: heatmapValues,
        label: { show: false },
        emphasis: {
          itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' },
        },
      },
    ],
  };
};

const getOverviewOption = (): EChartsOption => {
  const enabledPct = Number(enabledRatio.value.toFixed(0));
  return {
    backgroundColor: 'transparent',
    series: [
      {
        type: 'pie',
        radius: ['62%', '80%'],
        center: ['50%', '50%'],
        silent: true,
        label: { show: false },
        animation: false,
        data: [
          { value: statsOverview.value.enabledJobs, itemStyle: { color: '#52c41a' } },
          { value: statsOverview.value.disabledJobs, itemStyle: { color: '#d9d9d9' } },
        ],
      },
      {
        type: 'pie',
        radius: ['38%', '56%'],
        center: ['50%', '50%'],
        silent: true,
        label: { show: false },
        animation: false,
        data: [
          { value: normalCount.value, itemStyle: { color: '#1677ff' } },
          { value: pausedCount.value, itemStyle: { color: '#faad14' } },
        ],
      },
    ],
    graphic: [
      {
        type: 'group',
        left: 'center',
        top: 'center',
        children: [
          {
            type: 'text',
            style: {
              text: `${enabledPct}%`,
              fill: '#262626',
              fontSize: 13,
              fontWeight: 700,
              fontFamily: 'DIN Alternate, sans-serif',
              textAlign: 'center',
              y: -7,
            },
          },
          {
            type: 'text',
            style: {
              text: $t('page.quartz.analyticsPage.enabledStatus'),
              fill: '#8c8c8c',
              fontSize: 7,
              textAlign: 'center',
              y: 7,
            },
          },
        ],
      },
    ],
  };
};

const getActivityOption = (): EChartsOption => ({
  backgroundColor: 'transparent',
  grid: { left: 0, right: 0, top: 5, bottom: 0, containLabel: false },
  xAxis: { type: 'category', show: false, boundaryGap: false },
  yAxis: { type: 'value', show: false, min: 0 },
  series: [
    {
      type: 'line',
      data: recent7Data.value.map((d) => d.totalCount),
      smooth: true,
      symbol: 'none',
      lineStyle: { width: 2, color: '#52c41a' },
      areaStyle: {
        color: {
          type: 'linear',
          x: 0,
          y: 0,
          x2: 0,
          y2: 1,
          colorStops: [
            { offset: 0, color: 'rgba(82,196,26,0.25)' },
            { offset: 1, color: 'rgba(82,196,26,0.02)' },
          ],
        },
      },
    },
  ],
});

const getQualityOption = (): EChartsOption => {
  const success = recent7SuccessCount.value;
  const failed = recent7FailedCount.value;
  const rate = recent7SuccessRate.value;
  return {
    backgroundColor: 'transparent',
    series: [
      {
        type: 'pie',
        radius: ['58%', '74%'],
        center: ['50%', '50%'],
        silent: true,
        label: { show: false },
        animation: false,
        data: [
          { value: success, name: $t('page.quartz.analyticsPage.success'), itemStyle: { color: '#52c41a' } },
          { value: failed, name: $t('page.quartz.analyticsPage.failed'), itemStyle: { color: '#ff4d4f' } },
        ],
      },
    ],
    graphic: [
      {
        type: 'text',
        left: 'center',
        top: 'center',
        style: {
          text: `${rate}%`,
          fill: '#262626',
          fontSize: 12,
          fontWeight: 700,
          fontFamily: 'DIN Alternate, sans-serif',
          textAlign: 'center',
        },
      },
    ],
  };
};

const getDurationOption = (): EChartsOption => {
  const data = jobHealthData.value;
  if (data.length === 0) {
    return {
      backgroundColor: 'transparent',
      grid: { left: 0, right: 0, top: 5, bottom: 0, containLabel: false },
      xAxis: { type: 'category', show: false },
      yAxis: { type: 'value', show: false },
      series: [],
    };
  }
  const sorted = [...data].sort((a, b) => b.avgDuration - a.avgDuration).slice(0, 7);
  const durations = sorted.map((d) => d.avgDuration);
  const maxDur = Math.max(...durations, 1);
  return {
    backgroundColor: 'transparent',
    grid: { left: 0, right: 0, top: 5, bottom: 0, containLabel: false },
    xAxis: { type: 'category', show: false },
    yAxis: { type: 'value', show: false, min: 0, max: maxDur * 1.2 },
    series: [
      {
        type: 'bar',
        data: durations,
        barWidth: 6,
        itemStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: '#b37feb' },
              { offset: 1, color: '#722ed1' },
            ],
          },
          borderRadius: [2, 2, 0, 0],
        },
        silent: true,
        animation: false,
      },
    ],
  };
};

const formatDuration = (ms: number): string => {
  if (ms < 1000) return `${Math.round(ms)} ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)} s`;
  if (ms < 3600000) return `${(ms / 60000).toFixed(1)} min`;
  return `${(ms / 3600000).toFixed(1)} h`;
};

const formatDateTime = (dt?: string | null): string => {
  if (!dt) return '-';
  const d = new Date(dt);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const topSlowColumns = computed(() => [
  {
    title: '#',
    key: 'rank',
    width: 36,
    customRender: ({ index }: { index: number }) => index + 1,
  },
  {
    title: $t('page.quartz.jobPage.jobName'),
    dataIndex: 'jobName',
    key: 'jobName',
    width: 180,
    ellipsis: true,
  },
  {
    title: $t('page.quartz.jobPage.jobGroup'),
    dataIndex: 'jobGroup',
    key: 'jobGroup',
    width: 90,
    ellipsis: true,
  },
  {
    title: $t('page.quartz.analyticsPage.avgDuration'),
    dataIndex: 'avgDuration',
    key: 'avgDuration',
    width: 90,
    customRender: ({ text }: { text: number }) => formatDuration(text),
    sorter: (a: TopSlowJob, b: TopSlowJob) => a.avgDuration - b.avgDuration,
  },
  {
    title: $t('page.quartz.analyticsPage.maxDuration'),
    dataIndex: 'maxDuration',
    key: 'maxDuration',
    width: 90,
    customRender: ({ text }: { text: number }) => formatDuration(text),
  },
  {
    title: $t('page.quartz.analyticsPage.successRate'),
    dataIndex: 'successRate',
    key: 'successRate',
    width: 80,
    customRender: ({ text }: { text: number }) => {
      const color = text >= 95 ? '#52c41a' : text >= 80 ? '#faad14' : '#ff4d4f';
      return h(Tag, { color }, () => `${text}%`);
    },
  },
  {
    title: $t('page.quartz.analyticsPage.lastExecution'),
    dataIndex: 'lastExecutionTime',
    key: 'lastExecutionTime',
    width: 130,
    customRender: ({ text }: { text: string }) => formatDateTime(text),
  },
]);

/**
 * 健康象限图：禁止气泡在 hover/选中状态下的 z2 层级提升。
 *
 * ECharts 进入 emphasis/select 状态时会临时将元素 z2 提升（+10/+9），
 * 被 hover 的大气泡会跳到原本绘制在其上层的小气泡之上，再叠加 emphasis.scale
 * 的放大效果，小气泡会被完全遮挡而无法命中选中。将提升量置 0 后，气泡层级
 * 始终由数据顺序决定（小气泡在后、绘制在上层、优先命中）。
 */
const disableHealthBubbleZLift = (chart: any): number => {
  const model = chart?.getModel?.();
  if (!model) {
    return 0;
  }
  let patched = 0;
  model.getSeriesByType('scatter').forEach((seriesModel: any) => {
    seriesModel.getData().eachItemGraphicEl((el: any) => {
      const path = el?.getSymbolPath?.();
      if (path) {
        path.z2EmphasisLift = 0;
        path.z2SelectLift = 0;
        patched += 1;
      }
    });
  });
  return patched;
};

const fetchData = async () => {
  loading.value = true;
  const query: StatsQueryDto = {};

  try {
    const [overviewRes, perfRes] = await Promise.all([
      getAnalyticsOverview(query),
      getAnalyticsJobPerformance({ ...query, topCount: 10 }),
    ]);

    if (overviewRes.success && overviewRes.data) {
      statsOverview.value = overviewRes.data.stats;
      jobStatusDistribution.value = overviewRes.data.statusDistribution;
      trendData.value = overviewRes.data.executionTrend;
      heatmapData.value = overviewRes.data.executionHeatmap;
    }

    if (perfRes.success && perfRes.data) {
      jobHealthData.value = perfRes.data.jobHealthOverview;
      topSlowData.value = perfRes.data.topSlowJobs;
    }

    renderTrend(getTrendOption(trendData.value));
    renderHealth(getHealthOption(jobHealthData.value)).then(disableHealthBubbleZLift);
    renderHeatmap(getHeatmapOption(heatmapData.value));
    renderOverview(getOverviewOption());
    renderActivity(getActivityOption());
    renderQuality(getQualityOption());
    renderDuration(getDurationOption());
  } catch (error) {
    console.error('Data Fetch Error:', error);
  } finally {
    loading.value = false;
  }
};

const { systemConfig, loadSystemConfig } = useSystemConfig();

const environmentTagMap: Record<string, () => string> = {
  DEV: () => $t('page.quartz.systemConfigPage.envDEV'),
  TEST: () => $t('page.quartz.systemConfigPage.envTEST'),
  UAT: () => $t('page.quartz.systemConfigPage.envUAT'),
  PROD: () => $t('page.quartz.systemConfigPage.envPROD'),
};

const environmentTag = computed(
  () => environmentTagMap[systemConfig.value.environment] ?? environmentTagMap.DEV!,
);

const hasServiceName = computed(() => !!systemConfig.value.serviceName);

onMounted(() => {
  loadSystemConfig();
  fetchData();
});

watch(locale, () => {
  fetchData();
});

// 主题切换时 useEcharts 内部会重建图表实例并异步重渲染，需延时重新应用气泡层级补丁
watch(isDark, () => {
  let retries = 0;
  const applyPatch = () => {
    if (disableHealthBubbleZLift(getHealthInstance()) > 0 || retries >= 5) {
      return;
    }
    retries += 1;
    setTimeout(applyPatch, 120);
  };
  applyPatch();
});
</script>

<template>
  <Page auto-content-height header-class="page-header-compact">
    <template #title>
      <div class="page-title-row">
        <div v-if="hasServiceName" class="service-chip">
          <span class="service-bar"></span>
          <span class="service-name">{{ systemConfig.serviceName }}</span>
          <span class="env-pill" :data-env="systemConfig.environment">
            <i class="env-dot"></i>{{ environmentTag() }}
          </span>
        </div>
      </div>
    </template>
    <template #description>
      <p v-if="hasServiceName && systemConfig.serviceDescription" class="service-desc">
        {{ systemConfig.serviceDescription }}
      </p>
    </template>

    <Row :gutter="[16, 16]" align="stretch">
      <Col :xs="24" :sm="12" :lg="12" :xl="6">
        <Card class="stat-card stat-card--blue" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.jobOverview') }}</span>
              <span class="stat-number">
                {{ statsOverview.totalJobs }}
                <small class="stat-unit">{{ $t('page.quartz.analyticsPage.jobsUnit') }}</small>
              </span>
            </div>
            <div class="stat-mini-chart">
              <EchartsUI ref="overviewChartRef" style="height: 100px; width: 100px" />
            </div>
          </div>
          <div class="stat-footer">
            <span class="stat-footer__item">
              <i class="dot dot--success"></i>{{ $t('page.quartz.analyticsPage.enabledStatus') }} {{ statsOverview.enabledJobs }}
            </span>
            <span class="stat-footer__item">
              <i class="dot dot--muted"></i>{{ $t('page.quartz.analyticsPage.disabledStatus') }} {{ statsOverview.disabledJobs }}
            </span>
            <span class="stat-footer__item">
              <i class="dot dot--blue"></i>{{ $t('page.quartz.analyticsPage.normalStatus') }} {{ normalCount }}
            </span>
            <span class="stat-footer__item">
              <i class="dot dot--warning"></i>{{ $t('page.quartz.analyticsPage.pausedStatus') }} {{ pausedCount }}
            </span>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="12" :xl="6">
        <Card class="stat-card stat-card--green" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.executionActivity') }}</span>
              <div class="stat-number-row">
                <span class="stat-number">
                  {{ recent7DailyAvg }}
                  <small class="stat-unit">{{ $t('page.quartz.analyticsPage.dailyAvgExecution') }}</small>
                </span>
                <span class="stat-badge" :class="trendSummary.changePercent >= 0 ? 'stat-badge--up' : 'stat-badge--down'">
                  {{ trendSummary.changePercent >= 0 ? '↑' : '↓' }}{{ Math.abs(trendSummary.changePercent) }}%
                </span>
              </div>
            </div>
            <div class="stat-mini-chart">
              <EchartsUI ref="activityChartRef" style="height: 85px; width: 85px" />
            </div>
          </div>
          <div class="stat-footer">
            <span class="stat-footer__item">
              {{ $t('page.quartz.analyticsPage.recent7Total') }} {{ recent7TotalExecutions }} {{ $t('page.quartz.analyticsPage.times') }}
            </span>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="12" :xl="6">
        <Card class="stat-card stat-card--orange" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.executionQuality') }}</span>
              <span class="stat-number">
                {{ recent7SuccessRate }}
                <small class="stat-unit">%</small>
              </span>
            </div>
            <div class="stat-mini-chart">
              <EchartsUI ref="qualityChartRef" style="height: 100px; width: 100px" />
            </div>
          </div>
          <div class="stat-footer">
            <span class="stat-footer__item">
              <i class="dot dot--success"></i>{{ $t('page.quartz.analyticsPage.success') }} {{ recent7SuccessCount }}
            </span>
            <span class="stat-footer__item">
              <i class="dot dot--danger"></i>{{ $t('page.quartz.analyticsPage.failed') }} {{ recent7FailedCount }}
            </span>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="12" :xl="6">
        <Card class="stat-card stat-card--purple" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.durationLevel') }}</span>
              <span class="stat-number">
                {{ formatDuration(avgDurationValue) }}
              </span>
            </div>
            <div class="stat-mini-chart">
              <EchartsUI ref="durationChartRef" style="height: 85px; width: 85px" />
            </div>
          </div>
          <div class="stat-footer">
            <span class="stat-footer__item">
              P99 {{ formatDuration(p99Duration) }}
            </span>
            <span class="stat-footer__item">
              {{ $t('page.quartz.analyticsPage.slowestJob') }}: {{ slowestJobName }}
            </span>
          </div>
        </Card>
      </Col>

      <Col :span="24">
        <Card class="chart-card" :bordered="false">
          <template #title>
            <div class="chart-title">
              <span class="chart-title__bar"></span>
              <span class="chart-title__text">{{ $t('page.quartz.analyticsPage.executionTrend') }}</span>
            </div>
          </template>
          <template #extra>
            <div class="trend-summary">
              <span class="trend-summary__item">
                <span class="trend-summary__label">{{ $t('page.quartz.analyticsPage.recent7Avg') }}</span>
                <span class="trend-summary__value">{{ trendSummary.recent7Avg }}</span>
              </span>
              <span class="trend-summary__item">
                <span class="trend-summary__label">{{ $t('page.quartz.analyticsPage.weekOverWeek') }}</span>
                <span
                  class="trend-summary__value"
                  :class="trendSummary.changePercent >= 0 ? 'trend-summary__up' : 'trend-summary__down'"
                >
                  {{ trendSummary.changePercent >= 0 ? '↑' : '↓' }}{{ Math.abs(trendSummary.changePercent) }}%
                </span>
              </span>
              <span v-if="trendSummary.anomalyCount > 0" class="trend-summary__item trend-summary__item--warn">
                <span class="trend-summary__label">{{ $t('page.quartz.analyticsPage.anomalyCount') }}</span>
                <span class="trend-summary__value trend-summary__down">{{ trendSummary.anomalyCount }}</span>
              </span>
            </div>
          </template>
          <Skeleton :loading="loading" active :paragraph="{ rows: 6 }">
            <EchartsUI ref="trendChartRef" style="height: 280px" />
          </Skeleton>
        </Card>
      </Col>

      <Col :xs="24" :lg="12">
        <Card class="chart-card" :bordered="false">
          <template #title>
            <div class="chart-title">
              <span class="chart-title__bar"></span>
              <span class="chart-title__text">{{ $t('page.quartz.analyticsPage.jobOperationStatus') }}</span>
              <span class="chart-title__desc">{{ $t('page.quartz.analyticsPage.jobOperationStatusDesc') }}</span>
            </div>
          </template>
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="healthChartRef" style="height: 340px" />
          </Skeleton>
        </Card>
      </Col>

      <Col :xs="24" :lg="12">
        <Card class="chart-card" :bordered="false">
          <template #title>
            <div class="chart-title">
              <span class="chart-title__bar"></span>
              <span class="chart-title__text">{{ $t('page.quartz.analyticsPage.executionHeatmap') }}</span>
            </div>
          </template>
          <Skeleton :loading="loading" active :paragraph="{ rows: 8 }">
            <EchartsUI ref="heatmapChartRef" style="height: 340px" />
          </Skeleton>
        </Card>
      </Col>

      <Col :span="24">
        <Card class="chart-card" :bordered="false">
          <template #title>
            <div class="chart-title">
              <span class="chart-title__bar"></span>
              <span class="chart-title__text">{{ $t('page.quartz.analyticsPage.topSlowJobs') }}</span>
            </div>
          </template>
          <Skeleton :loading="loading" active :paragraph="{ rows: 6 }">
            <Table
              :columns="topSlowColumns"
              :data-source="topSlowData"
              :pagination="false"
              :scroll="{ x: 680 }"
              size="small"
              row-key="jobName"
              class="top-slow-table"
            />
          </Skeleton>
        </Card>
      </Col>
    </Row>
  </Page>
</template>

<style scoped>
:deep(.page-header-compact) {
  padding-top: 12px !important;
  padding-bottom: 12px !important;
}

.page-title-row {
  display: flex;
  align-items: center;
}

.service-chip {
  display: inline-flex;
  align-items: center;
  gap: 14px;
}

.service-bar {
  flex-shrink: 0;
  width: 4px;
  height: 26px;
  background: linear-gradient(180deg,
      hsl(var(--primary)),
      hsl(var(--primary) / 0.55));
  border-radius: 3px;
  box-shadow: 0 0 8px hsl(var(--primary) / 0.35);
}

.service-name {
  font-size: 20px;
  font-weight: 600;
  color: hsl(var(--foreground));
  line-height: 1.0;
  letter-spacing: 0.01em;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 360px;
}

.env-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 12px;
  font-size: 13px;
  font-weight: 500;
  line-height: 1.6;
  border-radius: 999px;
  border: 1px solid transparent;
  white-space: nowrap;
}

.env-dot {
  display: inline-block;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
}

.env-pill[data-env='DEV'] {
  color: hsl(var(--foreground));
  background: hsl(var(--muted-foreground) / 0.08);
  border-color: hsl(var(--muted-foreground) / 0.2);
}

.env-pill[data-env='DEV'] .env-dot {
  background: hsl(var(--muted-foreground));
}

.env-pill[data-env='TEST'] {
  color: hsl(212 100% 45%);
  background: hsl(212 100% 45% / 0.08);
  border-color: hsl(212 100% 45% / 0.2);
}

.env-pill[data-env='TEST'] .env-dot {
  background: hsl(212 100% 45%);
}

.env-pill[data-env='UAT'] {
  color: hsl(32 95% 44%);
  background: hsl(32 95% 54% / 0.08);
  border-color: hsl(32 95% 54% / 0.2);
}

.env-pill[data-env='UAT'] .env-dot {
  background: hsl(32 95% 54%);
}

.env-pill[data-env='PROD'] {
  color: hsl(0 84% 50%);
  background: hsl(0 84% 50% / 0.08);
  border-color: hsl(0 84% 50% / 0.2);
}

.env-pill[data-env='PROD'] .env-dot {
  background: hsl(0 84% 50%);
}

.service-desc {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  line-height: 1.0;
  margin-top: 10px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.stat-card {
  border-radius: 8px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.03);
  overflow: hidden;
  position: relative;
  height: 100%;
  min-height: 152px;
}

:deep(.stat-card .ant-card-body) {
  padding: 16px 18px;
  display: flex;
  flex-direction: column;
  height: 100%;
}

.stat-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
  height: 88px;
}

.stat-main {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.stat-title {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  margin-bottom: 6px;
  letter-spacing: 0.01em;
}

.stat-number {
  font-size: 28px;
  font-weight: 700;
  color: hsl(var(--foreground));
  line-height: 1.1;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.stat-unit {
  font-size: 12px;
  font-weight: 400;
  color: hsl(var(--muted-foreground));
  margin-left: 4px;
}

.stat-number-row {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-badge {
  display: inline-flex;
  align-items: center;
  font-size: 11px;
  font-weight: 600;
  padding: 0 4px;
  border-radius: 3px;
  line-height: 1.5;
}

.stat-badge--up {
  color: #52c41a;
  background: rgba(82, 196, 26, 0.1);
}

.stat-badge--down {
  color: #ff4d4f;
  background: rgba(255, 77, 79, 0.1);
}

.stat-mini-chart {
  flex-shrink: 0;
  margin-left: 8px;
}

.stat-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  margin-top: auto;
  padding-top: 10px;
  border-top: 1px solid hsl(var(--border) / 0.5);
}

.stat-footer__item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.dot--success {
  background: #52c41a;
}

.dot--muted {
  background: hsl(var(--muted-foreground) / 0.5);
}

.dot--blue {
  background: #1677ff;
}

.dot--warning {
  background: #faad14;
}

.dot--danger {
  background: #ff4d4f;
}

.chart-card {
  border-radius: 8px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.03);
}

:deep(.chart-card .ant-card-head) {
  border-bottom: 1px solid hsl(var(--border));
  min-height: auto;
  padding: 0 20px;
}

:deep(.chart-card .ant-card-head-title) {
  padding: 14px 0;
}

:deep(.chart-card .ant-card-body) {
  padding: 16px 20px 20px;
}

.chart-title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.chart-title__bar {
  width: 3px;
  height: 16px;
  background: hsl(var(--primary));
  border-radius: 2px;
}

.chart-title__text {
  font-size: 15px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

.chart-title__desc {
  font-size: 12px;
  font-weight: 400;
  color: hsl(var(--muted-foreground));
  margin-left: 4px;
}

.trend-summary {
  display: flex;
  align-items: center;
  gap: 16px;
  font-size: 12px;
}

.trend-summary__item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.trend-summary__label {
  color: #8c8c8c;
}

.trend-summary__value {
  font-weight: 600;
  color: #262626;
  font-variant-numeric: tabular-nums;
}

.trend-summary__up {
  color: #52c41a;
}

.trend-summary__down {
  color: #ff4d4f;
}

.trend-summary__item--warn {
  background: rgba(255, 77, 79, 0.08);
  padding: 2px 8px;
  border-radius: 4px;
}

.top-slow-table {
  font-size: 13px;
}

:deep(.top-slow-table .ant-table) {
  background: transparent;
}

:deep(.top-slow-table .ant-table-thead > tr > th) {
  background: hsl(var(--muted) / 0.3);
  font-size: 12px;
  padding: 8px 12px;
}

:deep(.top-slow-table .ant-table-tbody > tr > td) {
  padding: 8px 12px;
  font-variant-numeric: tabular-nums;
}

:deep(.top-slow-table .ant-table-cell) {
  white-space: nowrap;
}

@media (max-width: 576px) {
  .stat-number {
    font-size: 24px;
  }

  .stat-mini-chart {
    display: none;
  }

  .stat-content {
    height: auto;
    min-height: 52px;
  }

  .chart-title__desc {
    display: none;
  }
}
</style>