<script setup lang="ts">
interface Props {
  disabled?: boolean;
  /** 就地错误提示，显示在控件下方 */
  error?: string;
  /** 常驻说明，代替 hover 才能看到的 Tooltip */
  hint?: string;
  label: string;
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false,
  error: '',
  hint: '',
});
</script>

<template>
  <div class="setting-row" :class="{ 'is-disabled': props.disabled }">
    <div class="setting-row__label">{{ props.label }}</div>
    <div class="setting-row__control">
      <slot></slot>
    </div>
    <div class="setting-row__hint">{{ props.hint }}</div>
    <div v-if="props.error" class="setting-row__error">{{ props.error }}</div>
  </div>
</template>

<style scoped>
/* 控件紧跟标签，剩余空间交给说明文字，宽屏下不会出现「控件被推到最右」的空洞 */
.setting-row {
  display: grid;
  grid-template-columns: 148px 176px minmax(0, 1fr);
  align-items: center;
  column-gap: 20px;
  row-gap: 4px;
  padding: 11px 0;
}

/* 只在行与行之间画分隔线，避免第一行上方多出一条 */
.setting-row + .setting-row {
  border-top: 1px solid hsl(var(--border) / 0.6);
}

.setting-row__label {
  font-size: 13px;
  line-height: 20px;
  color: hsl(var(--foreground));
}

.setting-row__control {
  width: 100%;
  min-width: 0;
}

.setting-row__hint {
  font-size: 12px;
  line-height: 18px;
  color: hsl(var(--muted-foreground));
}

.setting-row__error {
  grid-column: 2 / -1;
  font-size: 12px;
  line-height: 18px;
  color: hsl(var(--destructive));
}

.is-disabled .setting-row__label {
  color: hsl(var(--muted-foreground));
}

@media (max-width: 900px) {
  .setting-row {
    grid-template-columns: 1fr;
  }
}
</style>
