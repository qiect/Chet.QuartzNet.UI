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
      text: '作业执行统计',
      left: 'center',
      textStyle: {
        fontSize: 16,
        fontWeight: 'bold',
      },
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
      },
    },
    legend: {
      data: ['作业数量'],
      bottom: 0,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
    },
    xAxis: {
      type: 'category',
      data: ['成功', '失败', '暂停', '阻塞'],
    },
    yAxis: {
      type: 'value',
      name: '执行次数',
    },
    series: [
      {
        name: '作业数量',
        type: 'bar',
        data: seriesData,
        itemStyle: {
          color: ['#52c41a', '#f5222d', '#faad14', '#1890ff'],
        },
      },
    ],
  };
};

// 作业状态分布图表配置
const getStatusDistributionChartOption = (): EChartsOption => {
  // 确保数据存在且为数组
  const chartData = jobStatusDistribution.value || [];
  console.log('chartData:', JSON.stringify(chartData));
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
  console.log('pieData:', JSON.stringify(pieData));
  return {
    title: {
      text: '作业状态分布',
      left: 'center',
      textStyle: {
        fontSize: 16,
        fontWeight: 'bold',
      },
    },
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
    },
    legend: {
      orient: 'vertical',
      left: 'left',
      bottom: 0,
      data: chartData.map(item => {
        const statusNumber = statusStringToNumberMap[item.status] || 0;
        const statusInfo = jobStatusMap[statusNumber] || { text: item.status };
        return statusInfo.text;
      }),
    },
    series: [
      {
        name: '作业状态',
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 10,
          borderColor: '#fff',
          borderWidth: 2,
        },
        label: {
          show: false,
          position: 'center',
        },
        emphasis: {
          label: {
            show: true,
            fontSize: 20,
            fontWeight: 'bold',
          },
        },
        labelLine: {
          show: false,
        },
        data: pieData,
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
        text: '作业类型分布',
        left: 'center',
        textStyle: {
          fontSize: 16,
          fontWeight: 'bold',
        },
      },
      tooltip: {
        trigger: 'item',
        formatter: '{b}: {c} ({d}%)',
      },
      legend: {
        orient: 'vertical',
        left: 'left',
        bottom: 0,
      },
      series: [
        {
          name: '作业类型',
          type: 'pie',
          radius: ['40%', '70%'],
          data: [{ value: 1, name: '暂无数据' }],
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
      text: '作业类型分布',
      left: 'center',
      textStyle: {
        fontSize: 16,
        fontWeight: 'bold',
      },
    },
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
    },
    legend: {
      orient: 'vertical',
      left: 'left',
      bottom: 0,
      data: chartData.map(item => item.type),
    },
    series: [
      {
        name: '作业类型',
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 10,
          borderColor: '#fff',
          borderWidth: 2,
        },
        label: {
          show: false,
          position: 'center',
        },
        emphasis: {
          label: {
            show: true,
            fontSize: 20,
            fontWeight: 'bold',
          },
        },
        labelLine: {
          show: false,
        },
        data: pieData,
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
      text: '作业执行趋势',
      left: 'center',
      textStyle: {
        fontSize: 16,
        fontWeight: 'bold',
      },
    },
    tooltip: {
      trigger: 'axis',
    },
    legend: {
      data: ['成功', '失败', '总数'],
      bottom: 0,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: xAxisData,
    },
    yAxis: {
      type: 'value',
      name: '执行次数',
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
              offset: 0, color: 'rgba(82, 196, 26, 0.3)'
            }, {
              offset: 1, color: 'rgba(82, 196, 26, 0.05)'
            }]
          }
        },
      },
      {
        name: '失败',
        type: 'line',
        stack: 'Total',
        data: hasData ? jobExecutionTrend.value.map(item => item.failedCount) : [0],
        itemStyle: {
          color: '#ff4d4f',
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
              offset: 0, color: 'rgba(255, 77, 79, 0.3)'
            }, {
              offset: 1, color: 'rgba(255, 77, 79, 0.05)'
            }]
          }
        },
      },
      {
        name: '总数',
        type: 'line',
        data: hasData ? jobExecutionTrend.value.map(item => item.totalCount) : [0],
        itemStyle: {
          color: '#1890ff',
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
              offset: 0, color: 'rgba(24, 144, 255, 0.3)'
            }, {
              offset: 1, color: 'rgba(24, 144, 255, 0.05)'
            }]
          }
        },
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
        text: '作业执行耗时统计',
        left: 'center',
        textStyle: {
          fontSize: 16,
          fontWeight: 'bold',
        },
      },
      tooltip: {
        trigger: 'axis',
        axisPointer: {
          type: 'shadow',
        },
      },
      legend: {
        data: ['作业数量'],
        bottom: 0,
      },
      grid: {
        left: '3%',
        right: '4%',
        bottom: '15%',
      },
      xAxis: {
        type: 'category',
        data: ['暂无数据'],
      },
      yAxis: {
        type: 'value',
        name: '作业数量',
      },
      series: [
        {
          name: '作业数量',
          type: 'bar',
          data: [0],
          itemStyle: {
            color: '#1890ff',
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
      text: '作业执行耗时统计',
      left: 'center',
      textStyle: {
        fontSize: 16,
        fontWeight: 'bold',
      },
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
      },
    },
    legend: {
      data: ['作业数量'],
      bottom: 0,
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
    },
    xAxis: {
      type: 'category',
      data: xAxisData,
    },
    yAxis: {
      type: 'value',
      name: '作业数量',
    },
    series: [
      {
        name: '作业数量',
        type: 'bar',
        data: seriesData,
        itemStyle: {
          color: '#1890ff',
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
    <Card class="mb-4 mt-4">
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
    </Card>
    <!-- 图表展示区 -->
    <Row :gutter="[16, 16]">
      <!-- 统计概览卡片 -->
      <Col :xs="24" :sm="12" :md="6" :lg="6">
      <Card hoverable>
        <Statistic title="总作业数" :value="statsOverview.totalJobs" />
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
      <Card title="作业执行统计" :loading="loading">
        <EchartsUI ref="executionStatsChartRef" />
      </Card>
      </Col>

      <!-- 作业状态分布 + 作业类型分布 -->
      <Col :xs="24" :sm="12" :md="12" :lg="12">
      <Card title="作业状态分布" :loading="loading">
        <EchartsUI ref="statusDistributionChartRef" />
      </Card>
      </Col>
      <Col :xs="24" :sm="12" :md="12" :lg="12">
      <Card title="作业类型分布" :loading="loading">
        <EchartsUI ref="typeDistributionChartRef" />
      </Card>
      </Col>

      <!-- 作业执行趋势 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24">
      <Card title="作业执行趋势" :loading="loading">
        <EchartsUI ref="executionTrendChartRef" />
      </Card>
      </Col>

      <!-- 作业执行耗时统计 -->
      <Col :xs="24" :sm="24" :md="24" :lg="24">
      <Card title="作业执行耗时统计" :loading="loading">
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
