import type {
  BarSeriesOption,
  HeatmapSeriesOption,
  LineSeriesOption,
  ScatterSeriesOption,
} from 'echarts/charts';
import type {
  DatasetComponentOption,
  GridComponentOption,
  TitleComponentOption,
  TooltipComponentOption,
  VisualMapComponentOption,
} from 'echarts/components';
import type { ComposeOption } from 'echarts/core';

import {
  BarChart,
  HeatmapChart,
  LineChart,
  ScatterChart,
} from 'echarts/charts';
import {
  DatasetComponent,
  GridComponent,
  GraphicComponent,
  LegendComponent,
  MarkAreaComponent,
  MarkLineComponent,
  MarkPointComponent,
  TitleComponent,
  TooltipComponent,
  TransformComponent,
  VisualMapComponent,
} from 'echarts/components';
import * as echarts from 'echarts/core';
import { LabelLayout, UniversalTransition } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';

export type ECOption = ComposeOption<
  | BarSeriesOption
  | DatasetComponentOption
  | GridComponentOption
  | HeatmapSeriesOption
  | LineSeriesOption
  | ScatterSeriesOption
  | TitleComponentOption
  | TooltipComponentOption
  | VisualMapComponentOption
>;

echarts.use([
  TitleComponent,
  TooltipComponent,
  GridComponent,
  DatasetComponent,
  TransformComponent,
  GraphicComponent,
  VisualMapComponent,
  MarkLineComponent,
  MarkPointComponent,
  MarkAreaComponent,
  BarChart,
  LineChart,
  ScatterChart,
  HeatmapChart,
  LabelLayout,
  UniversalTransition,
  CanvasRenderer,
  LegendComponent,
]);

export default echarts;