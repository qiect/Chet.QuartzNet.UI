import { nextTick } from 'vue';

/** 表格排序状态（与 vxe-table sortConfig.defaultSort 结构一致） */
export interface GridSortState {
  field: string;
  order: 'asc' | 'desc';
}

/** useVbenVxeGrid 返回的 gridApi 最小依赖 */
interface GridApiLike {
  grid?: any;
}

interface ApplyOptions {
  /** 最大尝试次数（表格实例或渲染可能尚未就绪） */
  maxAttempts?: number;
  /** 每次尝试的间隔（毫秒） */
  interval?: number;
}

/** 读取表格当前排序状态 */
const readSort = (grid: any): GridSortState | undefined => {
  const current = (grid?.getSortColumns?.() ?? [])[0];
  const field = current?.field ?? current?.property;
  if (!field || !current?.order) {
    return undefined;
  }
  return { field, order: current.order };
};

const isSameSort = (a?: GridSortState, b?: GridSortState) =>
  !!a && !!b && a.field === b.field && a.order === b.order;

/**
 * 恢复 vxe-table 表头排序标识（列头箭头）。
 *
 * 页面被 keep-alive 缓存后重新激活、或表格因列配置/主题重建时，
 * `sortConfig.defaultSort` 有时不会反映到列头，导致默认排序标识丢失。
 * 这里通过 `setSort` 主动同步，且仅在当前排序与期望不一致时才调用，
 * 避免触发多余的远程排序请求。
 *
 * @param gridApi useVbenVxeGrid 返回的 gridApi
 * @param sort 期望的排序状态
 * @returns 最终是否与期望排序一致
 */
export async function applyGridSortIndicator(
  gridApi: GridApiLike,
  sort: GridSortState,
  options: ApplyOptions = {},
): Promise<boolean> {
  const { maxAttempts = 10, interval = 80 } = options;

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    if (isSameSort(readSort(gridApi?.grid), sort)) {
      return true;
    }

    const grid: any = gridApi?.grid;
    if (!grid?.setSort) {
      // 表格实例尚未创建完成，等待后重试
      await new Promise((resolve) => setTimeout(resolve, interval));
      continue;
    }

    try {
      grid.setSort({ field: sort.field, order: sort.order });
    } catch {
      // 表格处于隐藏或未渲染完成状态时 setSort 可能抛错，交由下一轮重试
    }

    await nextTick();
    if (isSameSort(readSort(grid), sort)) {
      return true;
    }

    await new Promise((resolve) => setTimeout(resolve, interval));
  }

  return false;
}
