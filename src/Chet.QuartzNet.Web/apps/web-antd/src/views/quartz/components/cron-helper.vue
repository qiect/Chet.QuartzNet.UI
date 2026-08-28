<template>
  <Modal
    :open="visible"
    :title="$t('page.quartz.cronHelper.title')"
    width="760px"
    :footer="null"
    :z-index="10000"
    centered
    destroyOnClose
    wrapClassName="quartz-cron-helper-modal"
    @cancel="handleCancel"
  >
    <div class="cron-gen">
      <!-- 表达式输入区 -->
      <div class="expr-bar">
        <Input
          v-model:value="expression"
          :placeholder="$t('page.quartz.cronHelper.placeholder')"
          class="expr-bar__input"
          @input="onExpressionChange"
        >
          <template #suffix>
            <Popover placement="bottom" trigger="hover" :overlay-style="{ zIndex: 10001 }">
              <template #content>
                <div class="format-help">
                  <div class="format-help__pattern">{{ $t('page.quartz.cronHelper.formatPattern') }}</div>
                  <div class="format-help__legend">
                    <span><code>*</code> {{ $t('page.quartz.cronHelper.legendAny') }}</span>
                    <span><code>/</code> {{ $t('page.quartz.cronHelper.legendStep') }}</span>
                    <span><code>,</code> {{ $t('page.quartz.cronHelper.legendList') }}</span>
                    <span><code>-</code> {{ $t('page.quartz.cronHelper.legendRange') }}</span>
                    <span><code>?</code> {{ $t('page.quartz.cronHelper.legendNone') }}</span>
                    <span><code>L</code> {{ $t('page.quartz.cronHelper.legendLast') }}</span>
                  </div>
                </div>
              </template>
              <span class="expr-bar__tip-icon" :title="$t('page.quartz.cronHelper.formatTip')">
                <svg width="14" height="14" viewBox="0 0 16 16" fill="none">
                  <circle cx="8" cy="8" r="7" stroke="currentColor" stroke-width="1.5" />
                  <path d="M6 6.5C6 5.67 6.67 5 7.5 5H8.5C9.33 5 10 5.67 10 6.5C10 7.33 9.5 7.75 8.8 8.2C8.3 8.5 8 8.75 8 9.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
                  <circle cx="8" cy="11.5" r="0.75" fill="currentColor" />
                </svg>
              </span>
            </Popover>
          </template>
        </Input>
        <span class="expr-bar__status" :class="statusClass">
          <template v-if="validationLoading">{{ $t('page.quartz.cronHelper.validating') }}</template>
          <template v-else-if="isValid">{{ $t('page.quartz.cronHelper.valid') }}</template>
          <template v-else-if="isValid === false">{{ $t('page.quartz.cronHelper.invalid') }}</template>
          <template v-else>{{ $t('page.quartz.cronHelper.pending') }}</template>
        </span>
        <Button
          type="primary"
          size="small"
          :disabled="isValid !== true"
          @click="handleApply"
        >
          {{ $t('page.quartz.cronHelper.apply') }}
        </Button>
      </div>

      <!-- 主体：左右分栏 -->
      <div class="main-grid">
        <!-- 左侧：模板 + 生成器 -->
        <div class="left-panel">
          <Segmented v-model:value="leftTab" :options="segOptions" size="small" block />

          <!-- 常用模板 -->
          <div v-show="leftTab === 'preset'" class="preset-list">
            <div
              v-for="item in cronPresets"
              :key="item.expression"
              class="preset-row"
              :class="{ active: expression.trim() === item.expression }"
              @click="applyPreset(item.expression)"
            >
              <span class="preset-row__name">{{ item.name }}</span>
              <code class="preset-row__expr">{{ item.expression }}</code>
            </div>
          </div>

          <!-- 可视化生成：每字段独立 ref，v-model 直接绑定 -->
          <div v-show="leftTab === 'builder'" class="builder-list">
            <div class="bld-row">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldSecond') }}</span>
              <Select v-model:value="secMode" size="small" style="width: 90px" :options="modeOptions" :get-popup-container="getPopupContainer" @change="buildExpression" />
              <InputNumber v-if="secMode === 'interval'" v-model:value="secInterval" size="small" :min="1" :max="59" style="width: 72px" @change="buildExpression" />
              <Input v-if="secMode === 'specified'" v-model:value="secSpecified" size="small" placeholder="0,15,30,45" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-row">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldMinute') }}</span>
              <Select v-model:value="minMode" size="small" style="width: 90px" :options="modeOptions" :get-popup-container="getPopupContainer" @change="buildExpression" />
              <InputNumber v-if="minMode === 'interval'" v-model:value="minInterval" size="small" :min="1" :max="59" style="width: 72px" @change="buildExpression" />
              <Input v-if="minMode === 'specified'" v-model:value="minSpecified" size="small" placeholder="0,15,30,45" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-row">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldHour') }}</span>
              <Select v-model:value="hrMode" size="small" style="width: 90px" :options="modeOptions" :get-popup-container="getPopupContainer" @change="buildExpression" />
              <InputNumber v-if="hrMode === 'interval'" v-model:value="hrInterval" size="small" :min="1" :max="23" style="width: 72px" @change="buildExpression" />
              <Input v-if="hrMode === 'specified'" v-model:value="hrSpecified" size="small" placeholder="0,6,12,18" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-row" :class="{ 'bld-row--auto': autoFlash === 'day' }">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldDay') }}</span>
              <Select v-model:value="dayMode" size="small" style="width: 90px" :options="dayWkModeOptions" :get-popup-container="getPopupContainer" @change="onDayModeChange" />
              <InputNumber v-if="dayMode === 'interval'" v-model:value="dayInterval" size="small" :min="1" :max="31" style="width: 72px" @change="buildExpression" />
              <Input v-if="dayMode === 'specified'" v-model:value="daySpecified" size="small" placeholder="1,15" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-row">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldMonth') }}</span>
              <Select v-model:value="monMode" size="small" style="width: 90px" :options="modeOptions" :get-popup-container="getPopupContainer" @change="buildExpression" />
              <InputNumber v-if="monMode === 'interval'" v-model:value="monInterval" size="small" :min="1" :max="12" style="width: 72px" @change="buildExpression" />
              <Input v-if="monMode === 'specified'" v-model:value="monSpecified" size="small" placeholder="1,4,7,10" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-row" :class="{ 'bld-row--auto': autoFlash === 'week' }">
              <span class="bld-row__label">{{ $t('page.quartz.cronHelper.fieldWeek') }}</span>
              <Select v-model:value="wkMode" size="small" style="width: 90px" :options="dayWkModeOptions" :get-popup-container="getPopupContainer" @change="onWkModeChange" />
              <InputNumber v-if="wkMode === 'interval'" v-model:value="wkInterval" size="small" :min="1" :max="7" style="width: 72px" @change="buildExpression" />
              <Input v-if="wkMode === 'specified'" v-model:value="wkSpecified" size="small" placeholder="MON,WED,FRI" style="flex: 1; min-width: 0" @input="buildExpression" />
            </div>
            <div class="bld-hint">{{ $t('page.quartz.cronHelper.dayWeekHint') }}</div>
          </div>
        </div>

        <!-- 右侧：时间预览（大比重） -->
        <div class="right-panel">
          <div class="preview-head">
            <span class="preview-head__title">{{ $t('page.quartz.cronHelper.nextRuns') }}</span>
            <Button
              type="link"
              size="small"
              :loading="previewLoading"
              :disabled="!isValid"
              @click="loadPreview"
            >
              {{ $t('page.quartz.cronHelper.refresh') }}
            </Button>
          </div>
          <div v-if="previewLoading" class="preview-state">{{ $t('page.quartz.cronHelper.loading') }}</div>
          <div v-else-if="nextRunTimes.length > 0" class="preview-scroll">
            <div
              v-for="(time, idx) in nextRunTimes"
              :key="idx"
              class="preview-row"
            >
              <span class="preview-row__idx">{{ idx + 1 }}</span>
              <div class="preview-row__body">
                <span class="preview-row__date">{{ formatDate(time) }}</span>
                <span class="preview-row__time">{{ formatClock(time) }}</span>
                <span
                  class="preview-row__tag"
                  :class="{ 'is-near': isNearDay(time) }"
                >{{ dayLabel(time) }}</span>
              </div>
              <span class="preview-row__rel">{{ relativeTime(time) }}</span>
            </div>
          </div>
          <div v-else class="preview-state">
            {{ isValid === false ? $t('page.quartz.cronHelper.invalidExpression') : $t('page.quartz.cronHelper.previewEmpty') }}
          </div>
        </div>
      </div>
    </div>
  </Modal>
</template>

<script setup lang="ts">
import { ref, computed, watch, toRef, onUnmounted, type Ref } from 'vue';
import {
  Modal,
  Button,
  Input,
  InputNumber,
  Select,
  Popover,
  Segmented,
} from 'ant-design-vue';
import { useDraggableModal } from '../composables/use-draggable-modal';
import { validateCronExpression, getNextRunTimes } from '#/api/quartz/job';
import { $t } from '#/locales';

const props = defineProps<{ visible: boolean; currentExpression?: string }>();
const emit = defineEmits(['cancel', 'select', 'update:visible']);

useDraggableModal(toRef(props, 'visible'), 'quartz-cron-helper-modal');

// 表达式
const expression = ref(props.currentExpression || '0 0/1 * * * ?');
const leftTab = ref<'preset' | 'builder'>('preset');
const segOptions = computed(() => [
  { label: $t('page.quartz.cronHelper.tabPreset'), value: 'preset' },
  { label: $t('page.quartz.cronHelper.tabBuilder'), value: 'builder' },
]);

// 下拉框选项
const modeOptions = computed(() => [
  { label: $t('page.quartz.cronHelper.modeEvery'), value: 'every' },
  { label: $t('page.quartz.cronHelper.modeInterval'), value: 'interval' },
  { label: $t('page.quartz.cronHelper.modeSpecified'), value: 'specified' },
]);

// 日/周专用选项：Quartz 日周互斥，必须有一个为 ?
const dayWkModeOptions = computed(() => [
  { label: $t('page.quartz.cronHelper.modeEvery'), value: 'every' },
  { label: $t('page.quartz.cronHelper.modeInterval'), value: 'interval' },
  { label: $t('page.quartz.cronHelper.modeSpecified'), value: 'specified' },
  { label: $t('page.quartz.cronHelper.modeNone'), value: 'none' },
]);

// 下拉列表渲染到触发节点的父节点，避免被 Modal z-index 遮挡
const getPopupContainer = (triggerNode: HTMLElement) => triggerNode.parentNode as HTMLElement;

// 每字段独立 ref，v-model 直接绑定（彻底解决 reactive 数组响应式问题）
const secMode = ref('every');
const secInterval = ref(1);
const secSpecified = ref('');
const minMode = ref('every');
const minInterval = ref(1);
const minSpecified = ref('');
const hrMode = ref('every');
const hrInterval = ref(1);
const hrSpecified = ref('');
const dayMode = ref('every');
const dayInterval = ref(1);
const daySpecified = ref('');
const monMode = ref('every');
const monInterval = ref(1);
const monSpecified = ref('');
const wkMode = ref('none');
const wkInterval = ref(1);
const wkSpecified = ref('');

// 验证状态
const isValid = ref<boolean | null>(null);
const validationLoading = ref(false);
const statusClass = ref('');

// 预览状态
const nextRunTimes = ref<string[]>([]);
const previewLoading = ref(false);

let timer: ReturnType<typeof setTimeout> | null = null;

// 常用模板（10 个）
const cronPresets = computed(() => [
  { name: $t('page.quartz.cronHelper.presetEverySecond'), expression: '*/1 * * * * ?' },
  { name: $t('page.quartz.cronHelper.presetEveryMinute'), expression: '0 */1 * * * ?' },
  { name: $t('page.quartz.cronHelper.presetEvery5Minutes'), expression: '0 */5 * * * ?' },
  { name: $t('page.quartz.cronHelper.presetEvery30Minutes'), expression: '0 */30 * * * ?' },
  { name: $t('page.quartz.cronHelper.presetEveryHour'), expression: '0 0 */1 * * ?' },
  { name: $t('page.quartz.cronHelper.presetDailyMidnight'), expression: '0 0 0 * * ?' },
  { name: $t('page.quartz.cronHelper.presetDaily8am'), expression: '0 0 8 * * ?' },
  { name: $t('page.quartz.cronHelper.presetWeekday9am'), expression: '0 0 9 ? * MON-FRI' },
  { name: $t('page.quartz.cronHelper.presetMonday'), expression: '0 0 0 ? * MON' },
  { name: $t('page.quartz.cronHelper.presetMonthFirst'), expression: '0 0 0 1 * ?' },
]);

function fieldPart(mode: string, interval: number, specified: string): string {
  if (mode === 'none') return '?';
  if (mode === 'interval') return `*/${interval || 1}`;
  if (mode === 'specified') return specified || '*';
  return '*';
}

// 日周互斥联动：自动切换时高亮提示对应行
let autoFlashTimer: ReturnType<typeof setTimeout> | null = null;
const autoFlash = ref<'day' | 'week' | null>(null);

function flashAuto(field: 'day' | 'week') {
  autoFlash.value = field;
  if (autoFlashTimer) clearTimeout(autoFlashTimer);
  autoFlashTimer = setTimeout(() => (autoFlash.value = null), 1500);
}

// 日模式切换 → 联动周
function onDayModeChange() {
  if (dayMode.value === 'none') {
    // 日不指定时周必须生效，若周也是不指定则自动恢复为「每」
    if (wkMode.value === 'none') {
      wkMode.value = 'every';
      flashAuto('week');
    }
  } else if (wkMode.value !== 'none') {
    // 日生效时，周自动让位为 ?
    wkMode.value = 'none';
    flashAuto('week');
  }
  buildExpression();
}

// 周模式切换 → 联动日
function onWkModeChange() {
  if (wkMode.value === 'none') {
    // 周不指定时日必须生效，若日也是不指定则自动恢复为「每」
    if (dayMode.value === 'none') {
      dayMode.value = 'every';
      flashAuto('day');
    }
  } else if (dayMode.value !== 'none') {
    // 周生效时，日自动让位为 ?
    dayMode.value = 'none';
    flashAuto('day');
  }
  buildExpression();
}

// 生成表达式
function buildExpression() {
  const parts = [
    fieldPart(secMode.value, secInterval.value, secSpecified.value),
    fieldPart(minMode.value, minInterval.value, minSpecified.value),
    fieldPart(hrMode.value, hrInterval.value, hrSpecified.value),
    fieldPart(dayMode.value, dayInterval.value, daySpecified.value),
    fieldPart(monMode.value, monInterval.value, monSpecified.value),
    fieldPart(wkMode.value, wkInterval.value, wkSpecified.value),
  ];
  // 日周互斥兜底（联动正常时不会触发）
  const dayPart = parts[3] ?? '*';
  const weekPart = parts[5] ?? '?';
  if (dayPart === '?' && weekPart === '?') {
    parts[3] = '*';
  } else if (dayPart !== '?' && weekPart !== '?') {
    parts[5] = '?';
  }
  expression.value = parts.join(' ');
  onExpressionChange();
}

function applyPreset(expr: string) {
  expression.value = expr;
  // 同步生成器 UI 状态，保证两个 tab 一致
  parseExpressionToBuilder(expr);
  onExpressionChange();
}

// 表达式反向解析到生成器（编辑时回显当前配置）
function parseFieldToBuilder(
  part: string | undefined,
  mode: Ref<string>,
  interval: Ref<number>,
  specified: Ref<string>,
  allowNone = false,
) {
  if (!part) return;
  if (part === '?') {
    mode.value = allowNone ? 'none' : 'every';
    return;
  }
  if (part === '*') {
    mode.value = 'every';
    return;
  }
  const m = part.match(/^\*\/(\d+)$/);
  if (m) {
    mode.value = 'interval';
    interval.value = Number(m[1]) || 1;
    return;
  }
  mode.value = 'specified';
  specified.value = part;
}

function parseExpressionToBuilder(expr: string) {
  const parts = expr.trim().split(/\s+/);
  if (parts.length !== 6) return;
  parseFieldToBuilder(parts[0], secMode, secInterval, secSpecified);
  parseFieldToBuilder(parts[1], minMode, minInterval, minSpecified);
  parseFieldToBuilder(parts[2], hrMode, hrInterval, hrSpecified);
  parseFieldToBuilder(parts[3], dayMode, dayInterval, daySpecified, true);
  parseFieldToBuilder(parts[4], monMode, monInterval, monSpecified);
  parseFieldToBuilder(parts[5], wkMode, wkInterval, wkSpecified, true);
}

// 表达式变化 → 防抖验证 → 验证通过自动预览
function onExpressionChange() {
  isValid.value = null;
  statusClass.value = '';
  nextRunTimes.value = [];
  if (timer) clearTimeout(timer);
  if (!expression.value?.trim()) return;
  timer = setTimeout(async () => {
    validationLoading.value = true;
    try {
      const res = await validateCronExpression(expression.value.trim());
      const valid = res.data === true || res.success === true;
      isValid.value = valid;
      statusClass.value = valid ? 'is-ok' : 'is-err';
      if (valid) loadPreview();
    } catch {
      isValid.value = false;
      statusClass.value = 'is-err';
    } finally {
      validationLoading.value = false;
    }
  }, 350);
}

// 加载近10次执行时间
async function loadPreview() {
  if (!expression.value?.trim()) return;
  previewLoading.value = true;
  try {
    const res = await getNextRunTimes(expression.value.trim(), 10);
    if (res.success !== false && res.data) {
      nextRunTimes.value = res.data;
    } else {
      nextRunTimes.value = [];
    }
  } catch {
    nextRunTimes.value = [];
  } finally {
    previewLoading.value = false;
  }
}

// 将后端返回的时间字符串转为Date对象
// 后端可能返回：带偏移的ISO（2026-08-28T13:02:00+00:00）、
// 无偏移的ISO（2026-08-28T13:02:00）、空格分隔（2026-08-28 13:02:00）
// 兜底：无时区偏移时视为UTC，先规范化为标准ISO格式再解析
function parseTime(time: string): Date {
  if (!time) return new Date(NaN);
  // 规范化：空格分隔 → T 分隔（兼容 "2026-08-28 13:02:00" 格式）
  let normalized = time.replace(/^(\d{4}-\d{2}-\d{2})\s/, '$1T');
  // 已含时区偏移（Z / +HH:mm / +HHmm），直接解析
  if (/Z|[+-]\d{2}:\d{2}$|[+-]\d{4}$/.test(normalized)) {
    return new Date(normalized);
  }
  // 无时区偏移，视为UTC追加Z
  return new Date(normalized + 'Z');
}

// 格式化日期
function formatDate(time: string): string {
  if (!time) return '';
  try {
    const d = parseTime(time);
    if (isNaN(d.getTime())) return time;
    const y = d.getFullYear();
    const mo = String(d.getMonth() + 1).padStart(2, '0');
    const da = String(d.getDate()).padStart(2, '0');
    return `${y}-${mo}-${da}`;
  } catch {
    return time;
  }
}

// 格式化时分秒
function formatClock(time: string): string {
  if (!time) return '';
  try {
    const d = parseTime(time);
    if (isNaN(d.getTime())) return '';
    const h = String(d.getHours()).padStart(2, '0');
    const mi = String(d.getMinutes()).padStart(2, '0');
    const s = String(d.getSeconds()).padStart(2, '0');
    return `${h}:${mi}:${s}`;
  } catch {
    return '';
  }
}

// 与今天相差的天数（0=今天，按自然日计算）
function dayDiff(time: string): number {
  const d = parseTime(time);
  if (isNaN(d.getTime())) return Number.NaN;
  const now = new Date();
  const d0 = new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const n0 = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  return Math.round((d0 - n0) / 86400000);
}

// 星期标签：近 3 天显示 今天/明天/后天，其余显示周几
function dayLabel(time: string): string {
  const diff = dayDiff(time);
  if (diff === 0) return $t('page.quartz.cronHelper.today');
  if (diff === 1) return $t('page.quartz.cronHelper.tomorrow');
  if (diff === 2) return $t('page.quartz.cronHelper.dayAfterTomorrow');
  const d = parseTime(time);
  if (isNaN(d.getTime())) return '';
  const weekdays = $t('page.quartz.cronHelper.weekdays').split(',');
  return weekdays[d.getDay()] ?? '';
}

// 是否近 3 天（用于标签高亮）
function isNearDay(time: string): boolean {
  const diff = dayDiff(time);
  return diff >= 0 && diff <= 2;
}

// 相对时间
function relativeTime(time: string): string {
  try {
    const d = parseTime(time);
    if (isNaN(d.getTime())) return '';
    const diff = d.getTime() - Date.now();
    if (diff < 0) return $t('page.quartz.cronHelper.expired');
    const minutes = Math.round(diff / 60000);
    if (minutes < 1) return $t('page.quartz.cronHelper.soon');
    if (minutes < 60) return $t('page.quartz.cronHelper.minutesLater', { n: minutes });
    const hours = Math.round(minutes / 60);
    if (hours < 24) return $t('page.quartz.cronHelper.hoursLater', { n: hours });
    const days = Math.round(hours / 24);
    return $t('page.quartz.cronHelper.daysLater', { n: days });
  } catch {
    return '';
  }
}

function handleApply() {
  emit('select', expression.value);
  emit('update:visible', false);
}

function handleCancel() {
  emit('update:visible', false);
}

// 弹窗打开时初始化
watch(
  () => props.visible,
  (val) => {
    if (val) {
      if (props.currentExpression) {
        expression.value = props.currentExpression;
        parseExpressionToBuilder(props.currentExpression);
      }
      onExpressionChange();
    }
  },
  { immediate: true },
);

// 手动输入表达式后切到可视化 tab 时回填解析
watch(leftTab, (tab) => {
  if (tab === 'builder' && expression.value?.trim()) {
    parseExpressionToBuilder(expression.value);
  }
});

onUnmounted(() => {
  if (timer) clearTimeout(timer);
  if (autoFlashTimer) clearTimeout(autoFlashTimer);
});
</script>

<style scoped lang="less">
.cron-gen {
  --gap: 14px;
  font-size: 13px;
  line-height: 1.5;
}

/* 顶部表达式栏 */
.expr-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-bottom: var(--gap);
  border-bottom: 1px solid hsl(var(--border));

  &__input {
    flex: 1;
    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
    font-weight: 600;
    font-size: 14px;
  }

  &__tip-icon {
    display: flex;
    align-items: center;
    cursor: pointer;
    color: hsl(var(--muted-foreground));
    transition: color 0.15s;
    flex-shrink: 0;

    &:hover {
      color: hsl(var(--primary));
    }
  }

  &__status {
    font-size: 12px;
    white-space: nowrap;
    min-width: 56px;
    color: hsl(var(--muted-foreground));

    &.is-ok {
      color: #52c41a;
      font-weight: 600;
    }
    &.is-err {
      color: #ff4d4f;
      font-weight: 600;
    }
  }
}

/* 格式帮助 Tooltip 内容 */
.format-help {
  &__pattern {
    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
    font-size: 13px;
    font-weight: 600;
    margin-bottom: 8px;
    color: hsl(var(--foreground));
  }

  &__legend {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 14px;

    span {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
      color: hsl(var(--muted-foreground));
    }

    code {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 18px;
      height: 18px;
      padding: 0 4px;
      background: hsl(var(--accent));
      border-radius: 3px;
      font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
      color: hsl(var(--primary));
      font-weight: 600;
    }
  }
}

/* 主体网格 */
.main-grid {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: var(--gap);
  padding-top: var(--gap);
  min-height: 420px;
}

/* 左侧面板 */
.left-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  border-right: 1px solid hsl(var(--border));
  padding-right: var(--gap);
}

/* 预设模板 */
.preset-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.preset-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 7px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.15s;
  border: 1px solid transparent;

  &:hover {
    background: hsl(var(--accent));
  }

  &.active {
    background: hsl(var(--primary) / 0.08);
    border-color: hsl(var(--primary) / 0.3);
  }

  &__name {
    font-size: 13px;
    font-weight: 500;
    color: hsl(var(--foreground));
  }

  &__expr {
    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
    font-size: 11px;
    color: hsl(var(--primary));
    font-weight: 500;
  }
}

/* 可视化生成 */
.builder-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.bld-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 2px 4px;
  margin: -2px -4px;
  border-radius: 5px;

  &__label {
    width: 40px;
    flex-shrink: 0;
    font-size: 13px;
    font-weight: 600;
    color: hsl(var(--foreground));
  }

  /* 日周互斥自动切换时的提示闪烁 */
  &--auto {
    animation: bld-row-flash 1.5s ease;
  }
}

@keyframes bld-row-flash {
  0% {
    background: hsl(var(--primary) / 0.12);
  }
  60% {
    background: hsl(var(--primary) / 0.08);
  }
  100% {
    background: transparent;
  }
}

.bld-hint {
  margin-top: 4px;
  padding: 6px 10px;
  background: hsl(var(--primary) / 0.04);
  border-radius: 5px;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  line-height: 1.5;
}

/* 右侧预览面板 */
.right-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.preview-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;

  &__title {
    font-size: 14px;
    font-weight: 600;
    color: hsl(var(--foreground));
  }
}

.preview-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  color: hsl(var(--muted-foreground));
}

.preview-scroll {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 5px;
  max-height: 380px;
  padding-right: 4px;
}

.preview-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  transition: border-color 0.15s;

  &:hover {
    border-color: hsl(var(--primary) / 0.3);
  }

  &__idx {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border-radius: 50%;
    background: hsl(var(--primary) / 0.1);
    color: hsl(var(--primary));
    font-size: 11px;
    font-weight: 700;
    flex-shrink: 0;
  }

  &__body {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 1;
    min-width: 0;
  }

  &__date {
    font-size: 12px;
    color: hsl(var(--muted-foreground));
    font-variant-numeric: tabular-nums;
    flex-shrink: 0;
  }

  &__time {
    font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
    font-size: 13px;
    color: hsl(var(--foreground));
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    flex-shrink: 0;
  }

  &__tag {
    display: inline-flex;
    align-items: center;
    padding: 1px 6px;
    border-radius: 4px;
    background: hsl(var(--accent));
    color: hsl(var(--muted-foreground));
    font-size: 11px;
    line-height: 1.4;
    flex-shrink: 0;

    &.is-near {
      background: hsl(var(--primary) / 0.1);
      color: hsl(var(--primary));
      font-weight: 500;
    }
  }

  &__rel {
    margin-left: auto;
    font-size: 11px;
    color: hsl(var(--muted-foreground));
    flex-shrink: 0;
  }
}

/* 响应式 */
@media (max-width: 640px) {
  .main-grid {
    grid-template-columns: 1fr;
  }
  .left-panel {
    border-right: none;
    border-bottom: 1px solid hsl(var(--border));
    padding-right: 0;
    padding-bottom: var(--gap);
  }
}
</style>