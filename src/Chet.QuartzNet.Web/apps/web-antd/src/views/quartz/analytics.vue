<script setup lang="ts">
import { ref, shallowRef, onMounted, computed, h } from 'vue';
import { Page } from '@vben/common-ui';
import {
  Card,
  Row,
  Col,
  Skeleton,
  Table,
  Tag,
} from 'ant-design-vue';
import { Activity, CircleCheckBig, Layers, Package } from '@vben/icons';
import type { EChartsOption } from 'echarts';

import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { $t } from '#/locales';

import {
  getJobStats,
  getJobExecutionTrend,
  getJobHealthOverview,
  getJobExecutionHeatmap,
  getTopSlowJobs,
  getJobStatusDistribution,
  getJobTypeDistribution,
} from '../../api/quartz/job';
import type {
  JobStats,
  JobExecutionTrend,
  JobHealth,
  JobExecutionHeatmap,
  TopSlowJob,
  StatsQueryDto,
  JobStatusDistribution,
  JobTypeDistribution,
} from '../../api/quartz/job';
import { useSystemConfig } from '../../composables/use-system-config';

const loading = ref(false);
const trendChartRef = ref<EchartsUIType | null>(null);
const healthChartRef = ref<EchartsUIType | null>(null);
const heatmapChartRef = ref<EchartsUIType | null>(null);

const { renderEcharts: renderTrend } = useEcharts(trendChartRef);
const { renderEcharts: renderHealth } = useEcharts(healthChartRef);
const { renderEcharts: renderHeatmap } = useEcharts(heatmapChartRef);

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
const jobTypeDistribution = shallowRef<JobTypeDistribution[]>([]);

const normalCount = computed(
  () => jobStatusDistribution.value.find((d) => d.status === 'Normal')?.count || 0,
);
const pausedCount = computed(
  () => jobStatusDistribution.value.find((d) => d.status === 'Paused')?.count || 0,
);
const normalPercentage = computed(
  () => jobStatusDistribution.value.find((d) => d.status === 'Normal')?.percentage || 0,
);
const dllCount = computed(
  () => jobTypeDistribution.value.find((d) => d.type === 'DLL')?.count || 0,
);
const apiCount = computed(
  () => jobTypeDistribution.value.find((d) => d.type === 'API')?.count || 0,
);
const dllPercentage = computed(
  () => jobTypeDistribution.value.find((d) => d.type === 'DLL')?.percentage || 0,
);
const apiPercentage = computed(
  () => jobTypeDistribution.value.find((d) => d.type === 'API')?.percentage || 0,
);
const enabledRatio = computed(() =>
  (statsOverview.value.enabledJobs / (statsOverview.value.totalJobs || 1)) * 100,
);
const successRate = computed(() =>
  (
    (statsOverview.value.successCount / (statsOverview.value.totalExecutions || 1)) *
    100
  ).toFixed(1),
);
const successRatio = computed(() =>
  (statsOverview.value.successCount / (statsOverview.value.totalExecutions || 1)) * 100,
);

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

  const recent7 = totalValues.slice(-7);
  const prev7 = totalValues.slice(-14, -7);
  const recent7Avg = recent7.length > 0 ? recent7.reduce((a, b) => a + b, 0) / recent7.length : 0;
  const prev7Avg = prev7.length > 0 ? prev7.reduce((a, b) => a + b, 0) / prev7.length : 0;
  const changePercent = prev7Avg > 0 ? Number((((recent7Avg - prev7Avg) / prev7Avg) * 100).toFixed(1)) : 0;

  trendSummary.value = {
    recent7Avg: Number(recent7Avg.toFixed(1)),
    prev7Avg: Number(prev7Avg.toFixed(1)),
    changePercent,
    anomalyCount: anomalyPoints.length,
  };

  const busyThreshold = avgTotal * 1.2;
  const idleThreshold = avgTotal * 0.6;

  const zoneAreas: { xAxis: string; itemStyle?: { color: string; opacity: number } }[][] = [];
  for (let i = 0; i < n; i++) {
    const val = totalValues[i]!;
    let color: string | undefined;
    if (val >= busyThreshold) {
      color = 'rgba(255,77,79,0.06)';
    } else if (val <= idleThreshold) {
      color = 'rgba(24,144,255,0.06)';
    }
    if (color) {
      zoneAreas.push([
        { xAxis: dates[i]!, itemStyle: { color, opacity: 1 } },
        { xAxis: dates[i]! },
      ]);
    }
  }

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
            html += `<div style="font-size:11px;color:#8c8c8c;margin-top:4px;border-top:1px dashed #e8e8e8;padding-top:4px;">${$t('page.quartz.analyticsPage.dayOverDay')}: <span style="color:${clr};font-weight:500;">${arrow}${Math.abs(Number(pct))}%</span></div>`;
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
        splitLine: { lineStyle: { color: '#f5f5f5', type: 'dashed' } },
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
        markArea: zoneAreas.length > 0 ? {
          silent: true,
          data: zoneAreas,
        } : undefined,
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

  const maxExecCount = Math.max(...data.map((d) => d.executionCount), 1);

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
        return `
          <div style="font-weight:600;color:#262626;font-size:13px;margin-bottom:6px;">${d.jobName}</div>
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
      splitLine: { lineStyle: { color: '#f5f5f5', type: 'dashed' } },
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
      splitLine: { lineStyle: { color: '#f5f5f5', type: 'dashed' } },
    },
    graphic: [
      {
        type: 'group',
        right: 40,
        bottom: 10,
        children: [
          {
            type: 'circle',
            shape: { cx: 0, cy: -4, r: 4 },
            style: { fill: '#1677ff' },
          },
          {
            type: 'text',
            style: {
              text: $t('page.quartz.analyticsPage.quadrantHealthy'),
              x: 10,
              fill: '#1677ff',
              fontSize: 11,
              fontWeight: 500,
            },
          },
        ],
      },
      {
        type: 'group',
        right: 40,
        top: 8,
        children: [
          {
            type: 'circle',
            shape: { cx: 0, cy: -4, r: 4 },
            style: { fill: '#faad14' },
          },
          {
            type: 'text',
            style: {
              text: $t('page.quartz.analyticsPage.quadrantSlow'),
              x: 10,
              fill: '#faad14',
              fontSize: 11,
              fontWeight: 500,
            },
          },
        ],
      },
      {
        type: 'group',
        left: 68,
        bottom: 10,
        children: [
          {
            type: 'circle',
            shape: { cx: 0, cy: -4, r: 4 },
            style: { fill: '#fa8c16' },
          },
          {
            type: 'text',
            style: {
              text: $t('page.quartz.analyticsPage.quadrantUnstable'),
              x: 10,
              fill: '#fa8c16',
              fontSize: 11,
              fontWeight: 500,
            },
          },
        ],
      },
      {
        type: 'group',
        left: 68,
        top: 8,
        children: [
          {
            type: 'circle',
            shape: { cx: 0, cy: -4, r: 4 },
            style: { fill: '#ff4d4f' },
          },
          {
            type: 'text',
            style: {
              text: $t('page.quartz.analyticsPage.quadrantCritical'),
              x: 10,
              fill: '#ff4d4f',
              fontSize: 11,
              fontWeight: 500,
            },
          },
        ],
      },
    ],
    series: [
      {
        type: 'scatter',
        symbolSize: (_val: number[], params: any) => {
          const count = params.data.executionCount;
          return Math.max(8, Math.min(40, (count / maxExecCount) * 40));
        },
        data: data.map((d) => ({
          value: [d.successRate, d.avgDuration],
          jobName: d.jobName,
          jobGroup: d.jobGroup,
          status: d.status,
          isEnabled: d.isEnabled,
          successRate: d.successRate,
          avgDuration: d.avgDuration,
          executionCount: d.executionCount,
          itemStyle: {
            color: statusColorMap[d.status] || '#8c8c8c',
            opacity: d.isEnabled ? 1 : 0.4,
          },
        })),
        emphasis: {
          focus: 'self',
          itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.2)' },
        },
      },
    ],
  };
};

const getHeatmapOption = (data: JobExecutionHeatmap[]): EChartsOption => {
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

  const heatmapValues = data.map((d) => [d.hour, d.dayOfWeek - 1, d.count]);

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
        return `<b style="color:#262626;">${dayName} ${params.value[0]}:00</b><br/>
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
      data: days,
      splitArea: { show: true, areaStyle: { color: ['rgba(0,0,0,0.02)', 'transparent'] } },
      axisLabel: { color: '#8c8c8c', fontSize: 11 },
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

const fetchData = async () => {
  loading.value = true;
  const query: StatsQueryDto = { timeRangeType: 'last30Days' };

  try {
    const [
      statsRes,
      trendRes,
      healthRes,
      heatmapRes,
      slowRes,
      statusDistributionRes,
      typeDistributionRes,
    ] = await Promise.all([
      getJobStats(query),
      getJobExecutionTrend(query),
      getJobHealthOverview(query),
      getJobExecutionHeatmap(query),
      getTopSlowJobs(query, 10),
      getJobStatusDistribution(query),
      getJobTypeDistribution(query),
    ]);

    if (statsRes.success && statsRes.data) {
      statsOverview.value = statsRes.data;
    }

    trendData.value = trendRes?.success && trendRes.data ? trendRes.data : [];
    renderTrend(getTrendOption(trendData.value));

    jobHealthData.value = healthRes?.success && healthRes.data ? healthRes.data : [];
    renderHealth(getHealthOption(jobHealthData.value));

    heatmapData.value = heatmapRes?.success && heatmapRes.data ? heatmapRes.data : [];
    renderHeatmap(getHeatmapOption(heatmapData.value));

    topSlowData.value = slowRes?.success && slowRes.data ? slowRes.data : [];

    jobStatusDistribution.value = statusDistributionRes?.success && statusDistributionRes.data
      ? statusDistributionRes.data
      : [];

    jobTypeDistribution.value = typeDistributionRes?.success && typeDistributionRes.data
      ? typeDistributionRes.data
      : [];
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

    <Row :gutter="[16, 16]">
      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.totalJobs') }}</span>
              <span class="stat-number">
                {{ statsOverview.totalJobs }}
                <small class="stat-unit">{{ $t('page.quartz.analyticsPage.unit') }}</small>
              </span>
            </div>
            <div class="stat-icon stat-icon--blue">
              <Package class="stat-icon__svg" />
            </div>
          </div>
          <div class="stat-sub">
            <div class="stat-sub__label">
              <span class="sub-label">{{ $t('page.quartz.analyticsPage.enabledDisabled') }}</span>
              <span class="sub-value">
                <i class="dot dot--success"></i>{{ statsOverview.enabledJobs }}
                <i class="dot dot--muted"></i>{{ statsOverview.disabledJobs }}
              </span>
            </div>
            <div class="mini-bar">
              <div class="mini-bar__fill mini-bar__fill--blue" :style="{ width: enabledRatio + '%' }"></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.totalExecutions') }}</span>
              <span class="stat-number">
                {{ statsOverview.totalExecutions }}
                <small class="stat-unit">{{ $t('page.quartz.analyticsPage.times') }}</small>
              </span>
            </div>
            <div class="stat-icon stat-icon--green">
              <Activity class="stat-icon__svg" />
            </div>
          </div>
          <div class="stat-sub">
            <div class="stat-sub__label">
              <span class="sub-label">{{ $t('page.quartz.analyticsPage.successRate') }}</span>
              <span class="sub-value sub-value--success">{{ successRate }}%</span>
            </div>
            <div class="mini-bar">
              <div class="mini-bar__fill mini-bar__fill--green" :style="{ width: successRatio + '%' }"></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :loading="loading" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.normalRunning') }}</span>
              <span class="stat-number">
                {{ normalCount }}
                <small class="stat-unit">{{ $t('page.quartz.analyticsPage.unit') }}</small>
              </span>
            </div>
            <div class="stat-icon stat-icon--orange">
              <CircleCheckBig class="stat-icon__svg" />
            </div>
          </div>
          <div class="stat-sub">
            <div class="stat-sub__label">
              <span class="sub-label">{{ $t('page.quartz.analyticsPage.normalPaused') }}</span>
              <span class="sub-value">{{ normalCount }} / {{ pausedCount }}</span>
            </div>
            <div class="mini-bar">
              <div class="mini-bar__fill mini-bar__fill--orange" :style="{ width: normalPercentage + '%' }"></div>
            </div>
          </div>
        </Card>
      </Col>

      <Col :xs="24" :sm="12" :lg="6">
        <Card class="stat-card" :bordered="false">
          <div class="stat-content">
            <div class="stat-main">
              <span class="stat-title">{{ $t('page.quartz.analyticsPage.jobTypeDistribution') }}</span>
              <div class="dual-numbers">
                <span class="dual-item dual-item--dll">
                  <small>DLL</small>
                  <b>{{ dllCount }}</b>
                </span>
                <span class="dual-item dual-item--api">
                  <small>API</small>
                  <b>{{ apiCount }}</b>
                </span>
              </div>
            </div>
            <div class="stat-icon stat-icon--purple">
              <Layers class="stat-icon__svg" />
            </div>
          </div>
          <div class="stat-sub">
            <div class="stat-sub__label">
              <span class="sub-label">DLL {{ dllPercentage.toFixed(0) }}%</span>
              <span class="sub-value">API {{ apiPercentage.toFixed(0) }}%</span>
            </div>
            <div class="mini-bar mini-bar--dual">
              <div class="mini-bar__fill mini-bar__fill--purple" :style="{ width: dllPercentage + '%' }"></div>
              <div class="mini-bar__fill mini-bar__fill--cyan" :style="{ width: apiPercentage + '%' }"></div>
            </div>
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
  border-radius: 10px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.04);
  overflow: hidden;
  min-height: 152px;
}

:deep(.stat-card .ant-card-body) {
  padding: 18px 20px;
  display: flex;
  flex-direction: column;
  height: 100%;
}

.stat-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 14px;
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
  letter-spacing: 0.01em;
}

.stat-number {
  font-size: 30px;
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
  margin-left: 6px;
}

.stat-icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  position: relative;
}

.stat-icon__svg {
  width: 22px;
  height: 22px;
}

.stat-icon--blue {
  background: hsl(212 100% 45% / 0.12);
  color: hsl(212 100% 45%);
  box-shadow: 0 4px 12px hsl(212 100% 45% / 0.15);
}

.stat-icon--green {
  background: hsl(144 57% 58% / 0.15);
  color: hsl(144 57% 45%);
  box-shadow: 0 4px 12px hsl(144 57% 58% / 0.15);
}

.stat-icon--orange {
  background: hsl(42 84% 61% / 0.15);
  color: hsl(42 84% 50%);
  box-shadow: 0 4px 12px hsl(42 84% 61% / 0.15);
}

.stat-icon--purple {
  background: hsl(262 83% 58% / 0.15);
  color: hsl(262 83% 55%);
  box-shadow: 0 4px 12px hsl(262 83% 58% / 0.15);
}

.dual-numbers {
  display: flex;
  gap: 18px;
  align-items: baseline;
}

.dual-item {
  display: inline-flex;
  align-items: baseline;
  gap: 6px;
}

.dual-item small {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  letter-spacing: 0.04em;
  font-weight: 500;
}

.dual-item b {
  font-size: 26px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.02em;
}

.dual-item--dll b {
  color: hsl(262 83% 58%);
}

.dual-item--api b {
  color: hsl(187 100% 42%);
}

.stat-sub {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: auto;
}

.stat-sub__label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
}

.sub-label {
  color: hsl(var(--muted-foreground));
}

.sub-value {
  font-weight: 600;
  color: hsl(var(--foreground));
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-variant-numeric: tabular-nums;
}

.sub-value--success {
  color: hsl(var(--success));
}

.dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-right: 2px;
}

.dot--success {
  background: hsl(var(--success));
}

.dot--muted {
  background: hsl(var(--muted-foreground) / 0.5);
  margin-left: 6px;
}

.mini-bar {
  height: 6px;
  background: hsl(var(--accent));
  border-radius: 3px;
  overflow: hidden;
}

.mini-bar__fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

.mini-bar__fill--blue {
  background: linear-gradient(90deg, #1890ff, #40a9ff);
}

.mini-bar__fill--green {
  background: linear-gradient(90deg, #52c41a, #73d13d);
}

.mini-bar__fill--orange {
  background: linear-gradient(90deg, #faad14, #ffc53d);
}

.mini-bar__fill--purple {
  background: linear-gradient(90deg, #722ed1, #9254de);
}

.mini-bar__fill--cyan {
  background: linear-gradient(90deg, #13c2c2, #36cfc9);
}

.mini-bar--dual {
  display: flex;
}

.mini-bar--dual .mini-bar__fill {
  min-width: 0;
}

.chart-card {
  border-radius: 10px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  box-shadow: 0 1px 2px hsl(var(--foreground) / 0.04);
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
    font-size: 26px;
  }

  .dual-item b {
    font-size: 22px;
  }

  .chart-title__desc {
    display: none;
  }
}
</style>