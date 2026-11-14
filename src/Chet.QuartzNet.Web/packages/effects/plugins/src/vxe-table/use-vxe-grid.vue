<script lang="ts" setup>
import type {
  VxeGridDefines,
  VxeGridInstance,
  VxeGridListeners,
  VxeGridPropTypes,
  VxeGridProps as VxeTableGridProps,
  VxeToolbarPropTypes,
} from 'vxe-table';

import type { SetupContext } from 'vue';

import type { VbenFormProps } from '@vben-core/form-ui';

import type { ExtendedVxeGridApi, VxeGridProps } from './types';

import {
  computed,
  nextTick,
  onMounted,
  onUnmounted,
  toRaw,
  useSlots,
  useTemplateRef,
  watch,
} from 'vue';

import { usePriorityValues } from '@vben/hooks';
import { EmptyIcon } from '@vben/icons';
import { $t } from '@vben/locales';
import { usePreferences } from '@vben/preferences';
import {
  cloneDeep,
  cn,
  isBoolean,
  isEqual,
  mergeWithArrayOverride,
} from '@vben/utils';

import { VbenHelpTooltip, VbenLoading } from '@vben-core/shadcn-ui';

import { VxeButton } from 'vxe-pc-ui';
import { VxeGrid, VxeUI } from 'vxe-table';

import { extendProxyOptions } from './extends';
import { useTableForm } from './init';

import 'vxe-table/styles/cssvar.scss';
import 'vxe-pc-ui/styles/cssvar.scss';
import './style.css';

interface Props extends VxeGridProps {
  api: ExtendedVxeGridApi;
}

const props = withDefaults(defineProps<Props>(), {});

const FORM_SLOT_PREFIX = 'form-';

const TOOLBAR_ACTIONS = 'toolbar-actions';
const TOOLBAR_TOOLS = 'toolbar-tools';
const TABLE_TITLE = 'table-title';

const gridRef = useTemplateRef<VxeGridInstance>('gridRef');

const state = props.api?.useStore?.();

const {
  gridOptions,
  class: className,
  gridClass,
  gridEvents,
  formOptions,
  tableTitle,
  tableTitleHelp,
  showSearchForm,
  separator,
} = usePriorityValues(props, state);

/** 表格 id，用于关联列设置相关的本地存储 */
const gridId = computed(
  () => (toRaw(gridOptions.value) as undefined | VxeTableGridProps)?.id,
);

const { isMobile } = usePreferences();
const isSeparator = computed(() => {
  if (
    !formOptions.value ||
    showSearchForm.value === false ||
    separator.value === false
  ) {
    return false;
  }
  if (separator.value === true || separator.value === undefined) {
    return true;
  }
  return separator.value.show !== false;
});
const separatorBg = computed(() => {
  return !separator.value ||
    isBoolean(separator.value) ||
    !separator.value.backgroundColor
    ? undefined
    : separator.value.backgroundColor;
});
const slots: SetupContext['slots'] = useSlots();

const [Form, formApi] = useTableForm({
  compact: true,
  handleSubmit: async () => {
    const formValues = await formApi.getValues();
    formApi.setLatestSubmissionValues(toRaw(formValues));
    props.api.reload(formValues);
  },
  handleReset: async () => {
    const prevValues = await formApi.getValues();
    await formApi.resetForm();
    const formValues = await formApi.getValues();
    formApi.setLatestSubmissionValues(formValues);
    // 如果值发生了变化，submitOnChange会触发刷新。所以只在submitOnChange为false或者值没有发生变化时，手动刷新
    if (isEqual(prevValues, formValues) || !formOptions.value?.submitOnChange) {
      props.api.reload(formValues);
    }
  },
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
  },
  showCollapseButton: true,
  submitButtonOptions: {
    content: computed(() => $t('common.search')),
  },
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

const showTableTitle = computed(() => {
  return !!slots[TABLE_TITLE]?.() || tableTitle.value;
});

const showToolbar = computed(() => {
  return (
    !!slots[TOOLBAR_ACTIONS]?.() ||
    !!slots[TOOLBAR_TOOLS]?.() ||
    showTableTitle.value
  );
});

const toolbarOptions = computed(() => {
  const slotActions = slots[TOOLBAR_ACTIONS]?.();
  const slotTools = slots[TOOLBAR_TOOLS]?.();
  const searchBtn: VxeToolbarPropTypes.ToolConfig = {
    code: 'search',
    icon: 'vxe-icon-search',
    circle: true,
    status: showSearchForm.value ? 'primary' : undefined,
    title: showSearchForm.value
      ? $t('common.hideSearchPanel')
      : $t('common.showSearchPanel'),
  };
  // 将搜索按钮合并到用户配置的toolbarConfig.tools中
  const toolbarConfig: VxeGridPropTypes.ToolbarConfig = {
    tools: (gridOptions.value?.toolbarConfig?.tools ??
      []) as VxeToolbarPropTypes.ToolConfig[],
  };
  if (gridOptions.value?.toolbarConfig?.search && !!formOptions.value) {
    toolbarConfig.tools = Array.isArray(toolbarConfig.tools)
      ? [...toolbarConfig.tools, searchBtn]
      : [searchBtn];
  }

  if (!showToolbar.value) {
    return { toolbarConfig };
  }

  // 强制使用固定的toolbar配置，不允许用户自定义
  // 减少配置的复杂度，以及后续维护的成本
  toolbarConfig.slots = {
    ...(slotActions || showTableTitle.value
      ? { buttons: TOOLBAR_ACTIONS }
      : {}),
    ...(slotTools ? { tools: TOOLBAR_TOOLS } : {}),
  };
  return { toolbarConfig };
});

/**
 * 列定义引用缓存。
 *
 * 父组件任意一次渲染都会写一次 state，从而让下面的 options 重算并整体 cloneDeep，
 * 产生一份全新的 columns 数组。vxe 收到新的 columns 会重新加载列，
 * 这会把「列设置」面板里已拖拽的顺序重置回默认值，用户点「确认」时保存的其实是默认顺序
 * （表现为列顺序调整后确认无效）。这里按源引用缓存克隆结果：源数组没变就复用同一份，
 * 只有页面真正替换了列定义（如切换语言）时才让 vxe 重新加载列。
 */
let columnsSource: unknown;
let columnsClone: unknown;

const UNPIN_STORAGE_PREFIX = 'vben-vxe-unpinned:';

function columnKeyOf(column: any): string {
  return column?.field || (column?.type ? `type=${column.type}` : '');
}

function readUnpinnedKeys(gridId?: string): string[] {
  if (!gridId) {
    return [];
  }
  try {
    const raw = localStorage.getItem(`${UNPIN_STORAGE_PREFIX}${gridId}`);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed)
      ? parsed.filter((key) => typeof key === 'string')
      : [];
  } catch {
    return [];
  }
}

function writeUnpinnedKeys(gridId: string | undefined, keys: string[]) {
  if (!gridId) {
    return;
  }
  try {
    const storageKey = `${UNPIN_STORAGE_PREFIX}${gridId}`;
    if (keys.length === 0) {
      localStorage.removeItem(storageKey);
    } else {
      localStorage.setItem(storageKey, JSON.stringify(keys));
    }
  } catch {
    // 忽略隐私模式等场景下的存储异常
  }
}

/** 按记录取消列定义上的固定（含子列） */
function applyUnpinnedFixed(columns: any, keys: string[]) {
  if (!keys.length || !Array.isArray(columns)) {
    return;
  }
  const walk = (list: any[]) => {
    list.forEach((column) => {
      if (!column || typeof column !== 'object') {
        return;
      }
      if (column.fixed && keys.includes(columnKeyOf(column))) {
        column.fixed = undefined;
      }
      if (Array.isArray(column.children)) {
        walk(column.children);
      }
    });
  };
  walk(columns);
}

/**
 * vxe 的固定列始终渲染在表格两端，因此把固定列拖到中间位置时排序不会生效。
 * 这里把「已经不在固定端」的固定列收集起来，视为用户想取消该列的固定。
 */
function collectOutOfEdgeFixedKeys(columns: any[]): string[] {
  const list = columns.filter(
    (column) => column && column.type !== 'checkbox',
  );
  if (list.length === 0) {
    return [];
  }
  let head = 0;
  while (head < list.length && list[head]?.fixed === 'left') {
    head += 1;
  }
  let tail = list.length - 1;
  while (tail >= 0 && list[tail]?.fixed === 'right') {
    tail -= 1;
  }
  const keys: string[] = [];
  list.forEach((column, index) => {
    const key = columnKeyOf(column);
    if (!key) {
      return;
    }
    const outOfEdge =
      (column.fixed === 'left' && index >= head) ||
      (column.fixed === 'right' && index <= tail);
    if (outOfEdge) {
      keys.push(key);
    }
  });
  return keys;
}

function getStableColumns(columns: unknown) {
  if (!columns) {
    return columns;
  }
  if (columns !== columnsSource) {
    columnsSource = columns;
    columnsClone = cloneDeep(columns);
    // 用户曾把固定列拖出固定端，重新加载列定义时要继续沿用「取消固定」的结果
    applyUnpinnedFixed(columnsClone, readUnpinnedKeys(gridId.value));
  }
  return columnsClone;
}

const options = computed(() => {
  const globalGridConfig = VxeUI?.getConfig()?.grid ?? {};

  const mergedOptions: VxeTableGridProps = cloneDeep(
    mergeWithArrayOverride(
      {},
      toRaw(toolbarOptions.value),
      toRaw(gridOptions.value),
      globalGridConfig,
    ),
  );

  const rawColumns = (toRaw(gridOptions.value) as undefined | VxeTableGridProps)
    ?.columns;
  if (rawColumns) {
    mergedOptions.columns = getStableColumns(
      rawColumns,
    ) as VxeTableGridProps['columns'];
  }

  if (mergedOptions.proxyConfig) {
    const { ajax } = mergedOptions.proxyConfig;
    mergedOptions.proxyConfig.enabled = !!ajax;
    // 不自动加载数据, 由组件控制
    mergedOptions.proxyConfig.autoLoad = false;
  }

  if (mergedOptions.pagerConfig) {
    const mobileLayouts = [
      'PrevJump',
      'PrevPage',
      'Number',
      'NextPage',
      'NextJump',
    ] as any;
    const layouts = [
      'Total',
      'Sizes',
      'Home',
      ...mobileLayouts,
      'End',
    ] as readonly string[];
    mergedOptions.pagerConfig = mergeWithArrayOverride(
      {},
      mergedOptions.pagerConfig,
      {
        pageSize: 20,
        background: true,
        pageSizes: [10, 20, 30, 50, 100, 200],
        className: 'mt-2 w-full',
        layouts: isMobile.value ? mobileLayouts : layouts,
        size: 'mini' as const,
      },
    );
  }
  if (mergedOptions.formConfig) {
    mergedOptions.formConfig.enabled = false;
  }
  return mergedOptions;
});

function onToolbarToolClick(event: VxeGridDefines.ToolbarToolClickEventParams) {
  if (event.code === 'search') {
    onSearchBtnClick();
  }
  (
    gridEvents.value?.toolbarToolClick as VxeGridListeners['toolbarToolClick']
  )?.(event);
}

/**
 * 「列设置」确认后，把被拖离固定端的固定列记为「取消固定」。
 * 固定列只会渲染在表格两端，不取消固定的话对其拖动排序是无效的。
 */
function syncFixedColumnsAfterCustom() {
  const grid = gridRef.value;
  const id = gridId.value;
  if (!grid || !id) {
    return;
  }
  const keys = collectOutOfEdgeFixedKeys(
    (grid as any).getFullColumns?.() ?? [],
  );
  const previousKeys = readUnpinnedKeys(id);
  writeUnpinnedKeys(id, keys);
  if (isEqual([...keys].sort(), [...previousKeys].sort())) {
    return;
  }
  const rawOptions = toRaw(gridOptions.value) as
    | undefined
    | VxeTableGridProps;
  const rawColumns = rawOptions?.columns;
  if (!rawOptions || !Array.isArray(rawColumns)) {
    return;
  }
  // 换一份新的列定义触发 vxe 重新加载列，让本次取消固定立即生效，
  // 列顺序仍由 vxe 的列设置存储恢复
  props.api?.setState?.({
    gridOptions: { ...rawOptions, columns: [...rawColumns] },
  } as any);
}

function onCustomChange(event: { type: string }) {
  if (event?.type === 'confirm') {
    syncFixedColumnsAfterCustom();
  }
  (gridEvents.value?.custom as ((evt: any) => void) | undefined)?.(event);
}

function onSearchBtnClick() {
  props.api?.toggleSearchForm?.();
}

const events = computed(() => {
  return {
    ...gridEvents.value,
    toolbarToolClick: onToolbarToolClick,
    custom: onCustomChange,
  };
});

const delegatedSlots = computed(() => {
  const resultSlots: string[] = [];

  for (const key of Object.keys(slots)) {
    if (
      !['empty', 'form', 'loading', TOOLBAR_ACTIONS, TOOLBAR_TOOLS].includes(
        key,
      )
    ) {
      resultSlots.push(key);
    }
  }
  return resultSlots;
});

const delegatedFormSlots = computed(() => {
  const resultSlots: string[] = [];

  for (const key of Object.keys(slots)) {
    if (key.startsWith(FORM_SLOT_PREFIX)) {
      resultSlots.push(key);
    }
  }
  return resultSlots.map((key) => key.replace(FORM_SLOT_PREFIX, ''));
});

const showDefaultEmpty = computed(() => {
  // 检查是否有原生的 VXE Table 空状态配置
  const hasEmptyText = options.value.emptyText !== undefined;
  const hasEmptyRender = options.value.emptyRender !== undefined;

  // 如果有原生配置，就不显示默认的空状态
  return !hasEmptyText && !hasEmptyRender;
});

async function init() {
  await nextTick();
  const globalGridConfig = VxeUI?.getConfig()?.grid ?? {};
  const defaultGridOptions: VxeTableGridProps = mergeWithArrayOverride(
    {},
    toRaw(gridOptions.value),
    toRaw(globalGridConfig),
  );
  // 内部主动加载数据，防止form的默认值影响
  const autoLoad = defaultGridOptions.proxyConfig?.autoLoad;
  const enableProxyConfig = options.value.proxyConfig?.enabled;
  if (enableProxyConfig && autoLoad) {
    props.api.grid.commitProxy?.(
      'query',
      formOptions.value ? ((await formApi.getValues()) ?? {}) : {},
    );
    // props.api.reload(formApi.form?.values ?? {});
  }

  // form 由 vben-form代替，所以不适配formConfig，这里给出警告
  const formConfig = gridOptions.value?.formConfig;
  // 处理某个页面加载多个Table时，第2个之后的Table初始化报出警告
  // 因为第一次初始化之后会把defaultGridOptions和gridOptions合并后缓存进State
  if (formConfig && formConfig.enabled) {
    console.warn(
      '[Vben Vxe Table]: The formConfig in the grid is not supported, please use the `formOptions` props',
    );
  }
  props.api?.setState?.({ gridOptions: defaultGridOptions });
  // form 由 vben-form 代替，所以需要保证query相关事件可以拿到参数
  extendProxyOptions(props.api, defaultGridOptions, () =>
    formApi.getLatestSubmissionValues(),
  );
}

// formOptions支持响应式
watch(
  formOptions,
  () => {
    formApi.setState((prev) => {
      const finalFormOptions: VbenFormProps = mergeWithArrayOverride(
        {},
        formOptions.value,
        prev,
      );
      return {
        ...finalFormOptions,
        collapseTriggerResize: !!finalFormOptions.showCollapseButton,
      };
    });
  },
  {
    immediate: true,
  },
);

const isCompactForm = computed(() => {
  return formApi.getState()?.compact;
});

onMounted(() => {
  props.api?.mount?.(gridRef.value, formApi);
  init();
});

onUnmounted(() => {
  formApi?.unmount?.();
  props.api?.unmount?.();
});
</script>

<template>
  <div :class="cn('bg-card h-full rounded-md', className)">
    <VxeGrid
      ref="gridRef"
      :class="
        cn(
          'p-2',
          {
            'pt-0': showToolbar && !formOptions,
          },
          gridClass,
        )
      "
      v-bind="options"
      v-on="events"
    >
      <!-- 左侧操作区域或者title -->
      <template v-if="showToolbar" #toolbar-actions="slotProps">
        <slot v-if="showTableTitle" name="table-title">
          <div class="mr-1 pl-1 text-[1rem]">
            {{ tableTitle }}
            <VbenHelpTooltip v-if="tableTitleHelp" trigger-class="pb-1">
              {{ tableTitleHelp }}
            </VbenHelpTooltip>
          </div>
        </slot>
        <slot name="toolbar-actions" v-bind="slotProps"> </slot>
      </template>

      <!-- 继承默认的slot -->
      <template
        v-for="slotName in delegatedSlots"
        :key="slotName"
        #[slotName]="slotProps"
      >
        <slot :name="slotName" v-bind="slotProps"></slot>
      </template>
      <template #toolbar-tools="slotProps">
        <slot name="toolbar-tools" v-bind="slotProps"></slot>
        <VxeButton
          icon="vxe-icon-search"
          circle
          class="ml-2"
          v-if="gridOptions?.toolbarConfig?.search && !!formOptions"
          :status="showSearchForm ? 'primary' : undefined"
          :title="$t('common.search')"
          @click="onSearchBtnClick"
        />
      </template>

      <!-- form表单 -->
      <template #form>
        <div
          v-if="formOptions || !!slots.form"
          v-show="showSearchForm !== false"
          :class="
            cn(
              'relative rounded py-3',
              isCompactForm
                ? isSeparator
                  ? 'pb-8'
                  : 'pb-4'
                : isSeparator
                  ? 'pb-4'
                  : 'pb-0',
            )
          "
        >
          <slot name="form">
            <Form>
              <template
                v-for="slotName in delegatedFormSlots"
                :key="slotName"
                #[slotName]="slotProps"
              >
                <slot
                  :name="`${FORM_SLOT_PREFIX}${slotName}`"
                  v-bind="slotProps"
                ></slot>
              </template>
              <template #reset-before="slotProps">
                <slot name="reset-before" v-bind="slotProps"></slot>
              </template>
              <template #submit-before="slotProps">
                <slot name="submit-before" v-bind="slotProps"></slot>
              </template>
              <template #expand-before="slotProps">
                <slot name="expand-before" v-bind="slotProps"></slot>
              </template>
              <template #expand-after="slotProps">
                <slot name="expand-after" v-bind="slotProps"></slot>
              </template>
            </Form>
          </slot>
          <div
            v-if="isSeparator"
            :style="{
              ...(separatorBg ? { backgroundColor: separatorBg } : undefined),
            }"
            class="bg-background-deep z-100 absolute -left-2 bottom-1 h-2 w-[calc(100%+1rem)] overflow-hidden md:bottom-2 md:h-3"
          ></div>
        </div>
      </template>
      <!-- loading -->
      <template #loading>
        <slot name="loading">
          <VbenLoading :spinning="true" />
        </slot>
      </template>
      <!-- 统一控状态 -->
      <template v-if="showDefaultEmpty" #empty>
        <slot name="empty">
          <EmptyIcon class="mx-auto" />
          <div class="mt-2">{{ $t('common.noData') }}</div>
        </slot>
      </template>
    </VxeGrid>
  </div>
</template>
