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
  Select,
  Space,
  DatePicker,
  Statistic,
} from 'ant-design-vue';
import type { EChartsOption } from 'echarts';

// 导入Vben集成的ECharts组件
import type { EchartsUIType } from '@vben/plugins/echarts';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

// 导入作业API服务
import {
  getSchedulerStatus,
  getJobStats,
  getJobStatusDistribution,
  getJobExecutionTrend,
  getJobTypeDistribution,
  getJobExecutionTime,
} from '../../api/quartz/job';
import type {
  JobStats,
  JobStatusDistribution,
  JobExecutionTrend,
  JobTypeDistribution,
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

// 统计数据
const jobStats = ref<JobStats>({
  totalJobs: 0,
  enabledJobs: 0,
  disabledJobs: 0,
  executingJobs: 0,
  successCount: 0,
  failedCount: 0,
  pausedCount: 0,
  blockedCount: 0,
});

const jobStatusDistribution = ref<JobStatusDistribution[]>([]);
const jobExecutionTrend = ref<JobExecutionTrend[]>([]);
const jobTypeDistribution = ref<JobTypeDistribution[]>([]);
const jobExecutionTimeData = ref<JobExecutionTime[]>([]);

// 时间范围选择
const timeRangeOptions = [
  { label: '今日', value: 'today' },
  { label: '昨日', value: 'yesterday' },
  { label: '本周', value: 'thisWeek' },
  { label: '本月', value: 'thisMonth' },
  { label: '自定义', value: 'custom' },
];

const selectedTimeRange = ref('thisMonth');
const customDateRange = ref<[Date | null, Date | null]>([null, null]);

// Vben ECharts组件引用
const executionStatsChartRef = ref<EchartsUIType | null>(null);
const statusDistributionChartRef = ref<EchartsUIType | null>(null);
const typeDistributionChartRef = ref<EchartsUIType | null>(null);
const executionTrendChartRef = ref<EchartsUIType | null>(null);
const executionTimeChartRef = ref<EchartsUIType | null>(null);

// 使用Vben ECharts组合式函数
const { renderEcharts: renderExecutionStats } = useEcharts(executionStatsChartRef);
const { renderEcharts: renderStatusDistribution } = useEcharts(statusDistributionChartRef);
const { renderEcharts: renderTypeDistribution } = useEcharts(typeDistributionChartRef);
const { renderEcharts: renderExecutionTrend } = useEcharts(executionTrendChartRef);
const { renderEcharts: renderExecutionTime } = useEcharts(executionTimeChartRef);


// 获取统计数据
const fetchStatsData = async () => {
  loading.value = true;
  try {
    // 构建查询参数
    const query: StatsQueryDto = {
      timeRangeType: selectedTimeRange.value,
    };

    // 如果是自定义时间范围，添加开始时间和结束时间
    if (selectedTimeRange.value === 'custom' && customDateRange.value[0] && customDateRange.value[1]) {
      query.startTime = customDateRange.value[0].toISOString();
      query.endTime = customDateRange.value[1].toISOString();
    }

    // 获取作业统计数据
    const statsResponse = await getJobStats(query);
    if (statsResponse.success && statsResponse.data) {
      jobStats.value = statsResponse.data as JobStats;
      statsOverview.value = statsResponse.data as JobStats;
    }

    // 获取作业状态分布数据
    const statusDistributionResponse = await getJobStatusDistribution(query);
    if (statusDistributionResponse && statusDistributionResponse.success && statusDistributionResponse.data) {
      jobStatusDistribution.value = statusDistributionResponse.data as JobStatusDistribution[];
      console.log('jobStatusDistribution:', JSON.stringify(jobStatusDistribution.value));
    } else {
      jobStatusDistribution.value = [];
    }

    // 获取作业执行趋势数据
    const executionTrendResponse = await getJobExecutionTrend(query);
    if (executionTrendResponse && executionTrendResponse.success && executionTrendResponse.data) {
      jobExecutionTrend.value = executionTrendResponse.data as JobExecutionTrend[];
    } else {
      jobExecutionTrend.value = [];
    }

    // 获取作业类型分布数据
    const typeDistributionResponse = await getJobTypeDistribution(query);
    if (typeDistributionResponse && typeDistributionResponse.success && typeDistributionResponse.data) {
      jobTypeDistribution.value = typeDistributionResponse.data as JobTypeDistribution[];
    } else {
      jobTypeDistribution.value = [];
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

// 作业执行统计图表配置
const getExecutionStatsChartOption = (): EChartsOption => {
  // 确保jobStats存在
  const statsData = jobStats.value || {
    successCount: 0,
    failedCount: 0,
    pausedCount: 0,
    blockedCount: 0
  };

  // 构建图表数据
  const seriesData = [
    statsData.successCount,
    statsData.failedCount,
    statsData.pausedCount,
    statsData.blockedCount
  ];

  return {
    title: {
      left: 'center',
      textStyle: {
        fontSize: 18,
        fontWeight: '600',
        color: '#262626',
      },
      padding: [10, 0, 20, 0],
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
        shadowStyle: {
          color: 'rgba(0, 0, 0, 0.05)',
          blur: 10,
        },
      },
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e8e8e8',
      borderWidth: 1,
      borderRadius: 8,
      textStyle: {
        color: '#262626',
        fontSize: 14,
      },
      formatter: '{b}: {c} 次',
      padding: 12,
    },
    legend: {
      data: ['作业数量'],
      bottom: 0,
      textStyle: {
        color: '#595959',
        fontSize: 14,
      },
      itemWidth: 12,
      itemHeight: 12,
      itemGap: 20,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
      top: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: ['成功', '失败', '暂停', '阻塞'],
      axisLine: {
        lineStyle: {
          color: '#e8e8e8',
        },
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 14,
        fontWeight: '500',
        margin: 15,
      },
    },
    yAxis: {
      type: 'value',
      name: '执行次数',
      nameTextStyle: {
        color: '#595959',
        fontSize: 14,
        padding: [0, 0, 0, 20],
      },
      axisLine: {
        show: false,
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 14,
      },
      splitLine: {
        lineStyle: {
          color: '#f0f0f0',
          type: 'dashed',
        },
      },
    },
    series: [
      {
        name: '作业数量',
        type: 'bar',
        data: seriesData,
        barWidth: '60%',
        itemStyle: {
          color: function (params) {
            // 使用更协调的配色方案
            const colorList = ['#52c41a', '#ff4d4f', '#faad14', '#1890ff'];
            return colorList[params.dataIndex];
          },
          borderRadius: [8, 8, 0, 0],
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowOffsetX: 0,
            shadowColor: 'rgba(0, 0, 0, 0.15)',
          },
        },
        animation: true,
        animationDuration: 1000,
        animationEasing: 'cubicOut',
        animationDelay: function (idx) {
          return idx * 100;
        },
      },
    ],
  };
};

// 作业状态分布图表配置
const getStatusDistributionChartOption = (): EChartsOption => {
  // 确保数据存在且为数组
  const chartData = jobStatusDistribution.value || [];
  // 状态映射：将API返回的字符串状态转换为数字
  const statusStringToNumberMap: Record<string, number> = {
    'Normal': 0,
    'Paused': 1,
    'Completed': 2,
    'Error': 3,
    'Blocked': 4
  };

  // 构建图表数据，使用状态映射转换为中文名称
  const pieData = chartData.map(item => {
    const statusNumber = statusStringToNumberMap[item.status] || 0;
    const statusInfo = jobStatusMap[statusNumber] || { text: item.status };
    return {
      value: item.count,
      name: statusInfo.text,
    };
  });
  return {
    title: {
      left: 'center',
      textStyle: {
        fontSize: 18,
        fontWeight: '600',
        color: '#262626',
      },
      padding: [10, 0, 20, 0],
    },
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e8e8e8',
      borderWidth: 1,
      borderRadius: 8,
      textStyle: {
        color: '#262626',
        fontSize: 14,
      },
      padding: 12,
    },
    legend: {
      orient: 'vertical',
      left: 'left',
      bottom: 0,
      top: 'center',
      data: chartData.map(item => {
        const statusNumber = statusStringToNumberMap[item.status] || 0;
        const statusInfo = jobStatusMap[statusNumber] || { text: item.status };
        return statusInfo.text;
      }),
      textStyle: {
        color: '#595959',
        fontSize: 14,
      },
      itemWidth: 12,
      itemHeight: 12,
      itemGap: 20,
    },
    series: [
      {
        name: '作业状态',
        type: 'pie',
        radius: ['45%', '70%'],
        center: ['65%', '50%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 12,
          borderColor: '#fff',
          borderWidth: 3,
          color: function (params) {
            // 根据状态名称分配颜色，而不是根据数据索引
            const colorMap: Record<string, string> = {
              '正常': '#52c41a',  // 正常使用绿色
              '已暂停': '#faad14',  // 已暂停使用橙色
              '已完成': '#1890ff',  // 已完成使用蓝色
              '错误': '#ff4d4f',  // 错误使用红色
              '阻塞': '#722ed1',  // 阻塞使用紫色
            };
            return colorMap[params.name] || '#faad14'; // 默认使用橙色
          },
        },
        label: {
          show: false,
          position: 'center',
        },
        emphasis: {
          label: {
            show: true,
            fontSize: 22,
            fontWeight: '600',
            color: '#262626',
          },
          itemStyle: {
            shadowBlur: 15,
            shadowOffsetX: 0,
            shadowColor: 'rgba(0, 0, 0, 0.2)',
          },
        },
        labelLine: {
          show: false,
        },
        data: pieData,
        animation: true,
        animationDuration: 1200,
        animationEasing: 'cubicOut',
        animationDelay: function (idx) {
          return idx * 150;
        },
      },
    ],
  };
};

// 作业类型分布图表配置
const getTypeDistributionChartOption = (): EChartsOption => {
  // 确保数据存在且为数组
  const chartData = jobTypeDistribution.value || [];

  // 处理空数据情况
  if (chartData.length === 0) {
    return {
      title: {
        left: 'center',
        textStyle: {
          fontSize: 18,
          fontWeight: '600',
          color: '#262626',
        },
        padding: [10, 0, 20, 0],
      },
      tooltip: {
        trigger: 'item',
        formatter: '{b}: {c} ({d}%)',
        backgroundColor: 'rgba(255, 255, 255, 0.95)',
        borderColor: '#e8e8e8',
        borderWidth: 1,
        borderRadius: 8,
        textStyle: {
          color: '#262626',
          fontSize: 14,
        },
        padding: 12,
      },
      legend: {
        orient: 'vertical',
        left: 'left',
        bottom: 0,
        top: 'center',
        textStyle: {
          color: '#595959',
          fontSize: 14,
        },
        itemWidth: 12,
        itemHeight: 12,
        itemGap: 20,
      },
      series: [
        {
          name: '作业类型',
          type: 'pie',
          radius: ['45%', '70%'],
          center: ['65%', '50%'],
          data: [{ value: 1, name: '暂无数据' }],
          itemStyle: {
            color: '#f0f0f0',
            borderRadius: 12,
            borderColor: '#fff',
            borderWidth: 3,
          },
          label: {
            show: true,
            position: 'center',
            formatter: '暂无数据',
            fontSize: 18,
            color: '#bfbfbf',
          },
        },
      ],
    };
  }

  // 构建图表数据
  const pieData = chartData.map(item => ({
    value: item.count,
    name: item.type,
  }));

  return {
    title: {
      left: 'center',
      textStyle: {
        fontSize: 18,
        fontWeight: '600',
        color: '#262626',
      },
      padding: [10, 0, 20, 0],
    },
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e8e8e8',
      borderWidth: 1,
      borderRadius: 8,
      textStyle: {
        color: '#262626',
        fontSize: 14,
      },
      padding: 12,
    },
    legend: {
      orient: 'vertical',
      left: 'left',
      bottom: 0,
      top: 'center',
      data: chartData.map(item => item.type),
      textStyle: {
        color: '#595959',
        fontSize: 14,
      },
      itemWidth: 12,
      itemHeight: 12,
      itemGap: 20,
    },
    series: [
      {
        name: '作业类型',
        type: 'pie',
        radius: ['45%', '70%'],
        center: ['65%', '50%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 12,
          borderColor: '#fff',
          borderWidth: 3,
          color: function (params) {
            const colorList = ['#1890ff', '#52c41a', '#ff4d4f', '#faad14', '#722ed1', '#eb2f96', '#fa8c16', '#a0d911'];
            return colorList[params.dataIndex % colorList.length];
          },
        },
        label: {
          show: false,
          position: 'center',
        },
        emphasis: {
          label: {
            show: true,
            fontSize: 22,
            fontWeight: '600',
            color: '#262626',
          },
          itemStyle: {
            shadowBlur: 15,
            shadowOffsetX: 0,
            shadowColor: 'rgba(0, 0, 0, 0.2)',
          },
        },
        labelLine: {
          show: false,
        },
        data: pieData,
        animation: true,
        animationDuration: 1200,
        animationEasing: 'cubicOut',
        animationDelay: function (idx) {
          return idx * 150;
        },
      },
    ],
  };
};

// 作业执行趋势图表配置
const getExecutionTrendChartOption = (): EChartsOption => {
  // 处理空数据情况
  const hasData = jobExecutionTrend.value.length > 0;
  const xAxisData = hasData ? jobExecutionTrend.value.map(item => item.time) : ['暂无数据'];

  return {
    title: {
      left: 'center',
      textStyle: {
        fontSize: 18,
        fontWeight: '600',
        color: '#262626',
      },
      padding: [10, 0, 20, 0],
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'cross',
        label: {
          backgroundColor: 'rgba(255, 255, 255, 0.95)',
          borderColor: '#e8e8e8',
          borderWidth: 1,
          borderRadius: 6,
          color: '#262626',
          fontSize: 12,
        },
      },
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e8e8e8',
      borderWidth: 1,
      borderRadius: 8,
      textStyle: {
        color: '#262626',
        fontSize: 14,
      },
      padding: 12,
      formatter: function (params: any) {
        let result = `${params[0].name}<br/>`;
        params.forEach((item: any) => {
          result += `${item.marker} ${item.seriesName}: ${item.value} 次<br/>`;
        });
        return result;
      },
    },
    legend: {
      data: ['成功', '失败', '总数'],
      bottom: 0,
      textStyle: {
        color: '#595959',
        fontSize: 14,
      },
      itemWidth: 12,
      itemHeight: 12,
      itemGap: 20,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
      top: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: xAxisData,
      axisLine: {
        lineStyle: {
          color: '#e8e8e8',
        },
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 13,
        margin: 15,
      },
      splitLine: {
        show: false,
      },
    },
    yAxis: {
      type: 'value',
      name: '执行次数',
      nameTextStyle: {
        color: '#595959',
        fontSize: 14,
        padding: [0, 0, 0, 20],
      },
      axisLine: {
        show: false,
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 13,
      },
      splitLine: {
        lineStyle: {
          color: '#f0f0f0',
          type: 'dashed',
        },
      },
    },
    series: [
      {
        name: '成功',
        type: 'line',
        stack: 'Total',
        data: hasData ? jobExecutionTrend.value.map(item => item.successCount) : [0],
        itemStyle: {
          color: '#52c41a',
        },
        lineStyle: {
          width: 3,
        },
        symbol: 'circle',
        symbolSize: 6,
        emphasis: {
          symbolSize: 10,
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(82, 196, 26, 0.5)',
          },
        },
        // 添加平滑曲线和填充效果
        smooth: true,
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(82, 196, 26, 0.35)'
            }, {
              offset: 1, color: 'rgba(82, 196, 26, 0.08)'
            }]
          }
        },
        animation: true,
        animationDuration: 1500,
        animationEasing: 'cubicOut',
      },
      {
        name: '失败',
        type: 'line',
        stack: 'Total',
        data: hasData ? jobExecutionTrend.value.map(item => item.failedCount) : [0],
        itemStyle: {
          color: '#ff4d4f',
        },
        lineStyle: {
          width: 3,
        },
        symbol: 'circle',
        symbolSize: 6,
        emphasis: {
          symbolSize: 10,
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(255, 77, 79, 0.5)',
          },
        },
        // 添加平滑曲线和填充效果
        smooth: true,
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(255, 77, 79, 0.35)'
            }, {
              offset: 1, color: 'rgba(255, 77, 79, 0.08)'
            }]
          }
        },
        animation: true,
        animationDuration: 1500,
        animationEasing: 'cubicOut',
        animationDelay: 200,
      },
      {
        name: '总数',
        type: 'line',
        data: hasData ? jobExecutionTrend.value.map(item => item.totalCount) : [0],
        itemStyle: {
          color: '#1890ff',
        },
        lineStyle: {
          width: 3,
          type: 'dashed',
        },
        symbol: 'circle',
        symbolSize: 6,
        emphasis: {
          symbolSize: 10,
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(24, 144, 255, 0.5)',
          },
        },
        // 添加平滑曲线和填充效果
        smooth: true,
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [{
              offset: 0, color: 'rgba(24, 144, 255, 0.35)'
            }, {
              offset: 1, color: 'rgba(24, 144, 255, 0.08)'
            }]
          }
        },
        animation: true,
        animationDuration: 1500,
        animationEasing: 'cubicOut',
        animationDelay: 400,
      },
    ],
  };
};
// 作业执行耗时统计图表配置
const getExecutionTimeChartOption = (): EChartsOption => {
  // 确保数据存在且为数组
  const chartData = jobExecutionTimeData.value || [];

  // 处理空数据情况
  if (chartData.length === 0) {
    return {
      title: {

        left: 'center',
        textStyle: {
          fontSize: 18,
          fontWeight: '600',
          color: '#262626',
        },
        padding: [10, 0, 20, 0],
      },
      tooltip: {
        trigger: 'axis',
        axisPointer: {
          type: 'shadow',
          shadowStyle: {
            color: 'rgba(0, 0, 0, 0.05)',
            blur: 10,
          },
        },
        backgroundColor: 'rgba(255, 255, 255, 0.95)',
        borderColor: '#e8e8e8',
        borderWidth: 1,
        borderRadius: 8,
        textStyle: {
          color: '#262626',
          fontSize: 14,
        },
        padding: 12,
      },
      legend: {
        data: ['作业数量'],
        bottom: 0,
        textStyle: {
          color: '#595959',
          fontSize: 14,
        },
        itemWidth: 12,
        itemHeight: 12,
        itemGap: 20,
      },
      grid: {
        left: '3%',
        right: '4%',
        bottom: '15%',
        top: '15%',
        containLabel: true,
      },
      xAxis: {
        type: 'category',
        data: ['暂无数据'],
        axisLine: {
          lineStyle: {
            color: '#e8e8e8',
          },
        },
        axisTick: {
          show: false,
        },
        axisLabel: {
          color: '#595959',
          fontSize: 14,
          fontWeight: '500',
          margin: 15,
        },
      },
      yAxis: {
        type: 'value',
        name: '作业数量',
        nameTextStyle: {
          color: '#595959',
          fontSize: 14,
          padding: [0, 0, 0, 20],
        },
        axisLine: {
          show: false,
        },
        axisTick: {
          show: false,
        },
        axisLabel: {
          color: '#595959',
          fontSize: 14,
        },
        splitLine: {
          lineStyle: {
            color: '#f0f0f0',
            type: 'dashed',
          },
        },
      },
      series: [
        {
          name: '作业数量',
          type: 'bar',
          data: [0],
          barWidth: '60%',
          itemStyle: {
            color: '#1890ff',
            borderRadius: [8, 8, 0, 0],
          },
        },
      ],
    };
  }

  // 构建图表数据
  const xAxisData = chartData.map(item => item.timeRange);
  const seriesData = chartData.map(item => item.count);

  return {
    title: {
      left: 'center',
      textStyle: {
        fontSize: 18,
        fontWeight: '600',
        color: '#262626',
      },
      padding: [10, 0, 20, 0],
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
        shadowStyle: {
          color: 'rgba(0, 0, 0, 0.05)',
          blur: 10,
        },
      },
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#e8e8e8',
      borderWidth: 1,
      borderRadius: 8,
      textStyle: {
        color: '#262626',
        fontSize: 14,
      },
      padding: 12,
      formatter: '{b}: {c} 个作业',
    },
    legend: {
      data: ['作业数量'],
      bottom: 0,
      textStyle: {
        color: '#595959',
        fontSize: 14,
      },
      itemWidth: 12,
      itemHeight: 12,
      itemGap: 20,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
      top: '15%',
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: xAxisData,
      axisLine: {
        lineStyle: {
          color: '#e8e8e8',
        },
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 13,
        fontWeight: '500',
        margin: 15,
        rotate: xAxisData.length > 5 ? 30 : 0,
      },
    },
    yAxis: {
      type: 'value',
      name: '作业数量',
      nameTextStyle: {
        color: '#595959',
        fontSize: 14,
        padding: [0, 0, 0, 20],
      },
      axisLine: {
        show: false,
      },
      axisTick: {
        show: false,
      },
      axisLabel: {
        color: '#595959',
        fontSize: 14,
      },
      splitLine: {
        lineStyle: {
          color: '#f0f0f0',
          type: 'dashed',
        },
      },
    },
    series: [
      {
        name: '作业数量',
        type: 'bar',
        data: seriesData,
        barWidth: '60%',
        itemStyle: {
          color: function (params) {
            const colorList = ['#1890ff', '#52c41a', '#ff4d4f', '#faad14', '#722ed1', '#eb2f96', '#fa8c16', '#a0d911'];
            return colorList[params.dataIndex % colorList.length];
          },
          borderRadius: [8, 8, 0, 0],
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowOffsetX: 0,
            shadowColor: 'rgba(0, 0, 0, 0.15)',
          },
        },
        animation: true,
        animationDuration: 1000,
        animationEasing: 'cubicOut',
        animationDelay: function (idx) {
          return idx * 100;
        },
      },
    ],
  };
};

// 渲染所有图表
const renderAllCharts = () => {
  try {
    renderExecutionStats(getExecutionStatsChartOption());
    renderStatusDistribution(getStatusDistributionChartOption());
    renderTypeDistribution(getTypeDistributionChartOption());
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

// 时间范围变化处理
const handleTimeRangeChange = () => {
  // 根据时间范围获取数据
  fetchStatsData();
};

// 自定义日期范围变化处理
const handleDateRangeChange = () => {
  if (customDateRange.value[0] && customDateRange.value[1]) {
    selectedTimeRange.value = 'custom';
    // 根据自定义时间范围获取数据
    fetchStatsData();
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
  <Page>
    <!-- 数据筛选区 -->
    <!-- <Card class="mb-4 mt-4">
      <Row :gutter="[16, 16]" align="middle">
        <Col :xs="24" :sm="12" :md="8" :lg="8">
        <Space>
          <Select v-model:value="selectedTimeRange" :options="timeRangeOptions" style="width: 100px"
            @change="handleTimeRangeChange" />
          <DatePicker.RangePicker v-if="selectedTimeRange === 'custom'" v-model:value="customDateRange"
            style="width: 300px" @change="handleDateRangeChange" />
        </Space>
        </Col>
        <Col :xs="24" :sm="12" :md="16" :lg="16" class="text-right">
        <Button type="primary" @click="handleRefresh" :loading="loading">
          刷新数据
        </Button>
        </Col>
      </Row>
    </Card> -->
    <!-- 图表展示区 -->
    <Row :gutter="[16, 16]">
      <!-- 统计概览卡片 -->
      <Col :xs="24" :sm="12" :md="6" :lg="6">
      <Card hoverable>
        <Statistic title="总作业数" :value="statsOverview.totalJobs" prefix="📊" />
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="6" :lg="6">
      <Card hoverable>
        <Statistic title="启用作业数" :value="statsOverview.enabledJobs" prefix="✅" />
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="6" :lg="6">
      <Card hoverable>
        <Statistic title="禁用作业数" :value="statsOverview.disabledJobs" prefix="❌" />
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="6" :lg="6">
      <Card hoverable>
        <Statistic title="正在执行" :value="statsOverview.executingJobs" prefix="⏳" />
      </Card>
      </Col>

      <!-- 作业执行统计 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24">
      <Card title="本月作业执行统计" :loading="loading">
        <EchartsUI ref="executionStatsChartRef" />
      </Card>
      </Col>

      <!-- 作业状态分布 + 作业类型分布 -->
      <Col :xs="24" :sm="12" :md="12" :lg="12">
      <Card title="本月作业状态分布" :loading="loading">
        <EchartsUI ref="statusDistributionChartRef" />
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="12" :lg="12">
      <Card title="本月作业类型分布" :loading="loading">
        <EchartsUI ref="typeDistributionChartRef" />
      </Card>
      </Col>

      <!-- 作业执行趋势 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24">
      <Card title="本月作业执行趋势" :loading="loading">
        <EchartsUI ref="executionTrendChartRef" />
      </Card>
      </Col>

      <!-- 作业执行耗时统计 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24">
      <Card title="本月作业执行耗时统计" :loading="loading">
        <EchartsUI ref="executionTimeChartRef" />
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
</style>
