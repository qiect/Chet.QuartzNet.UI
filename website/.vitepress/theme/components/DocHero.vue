<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useData, withBase } from 'vitepress'

const { page, frontmatter } = useData()

const sections: Record<string, { label: string; overview: string }> = {
  guide: { label: '使用指南', overview: '/guide/introduction' },
  modules: { label: '功能模块', overview: '/modules/overview' },
  articles: { label: '系列教程', overview: '/articles/overview' },
}

const relativePath = computed(() => page.value.relativePath || '')
const dir = computed(() => relativePath.value.split('/')[0] || '')
const fileName = computed(() => relativePath.value.split('/').pop() || '')
const number = computed(() => fileName.value.match(/^(\d{2})-/)?.[1] ?? '')

const current = computed(() => {
  if (relativePath.value === 'changelog.md') {
    return { label: '更新日志', overview: '' }
  }
  return sections[dir.value] ?? { label: '文档', overview: '' }
})

const eyebrow = computed(() => {
  if (relativePath.value === 'changelog.md') return '更新日志'
  if (dir.value === 'articles' && number.value) return `系列教程 · 第 ${number.value} 篇`
  return current.value.label
})

const title = computed(() => page.value.title || 'Chet.QuartzNet.UI 文档')
const lead = computed(() => frontmatter.value.description || '')

const updated = computed(() => {
  const time = page.value.lastUpdated
  if (!time) return ''
  const date = new Date(time)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `更新于 ${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
})

const reading = ref('')
onMounted(() => {
  const text = (document.querySelector('.vp-doc')?.textContent || '').trim()
  if (!text) return
  const cjk = (text.match(/[\u4e00-\u9fa5]/g) || []).length
  const words = (text.match(/[A-Za-z0-9_]+/g) || []).length
  const minutes = Math.max(1, Math.round((cjk + words * 2) / 400))
  reading.value = `约 ${minutes} 分钟阅读`
})

const showOverview = computed(() => {
  const overview = current.value.overview
  if (!overview) return false
  return overview !== `/${relativePath.value.replace(/\.md$/, '')}`
})
</script>

<template>
  <header class="ca-doc-hero">
    <div class="ca-doc-hero-glow" aria-hidden="true" />
    <div class="ca-doc-body">
      <span class="ca-doc-eyebrow">{{ eyebrow }}</span>
      <h1 class="ca-doc-title">{{ title }}</h1>
      <p v-if="lead" class="ca-doc-lead">{{ lead }}</p>
      <div class="ca-doc-chips">
        <span v-if="reading">{{ reading }}</span>
        <span v-if="updated">{{ updated }}</span>
        <span>MIT 开源</span>
      </div>
      <div v-if="showOverview" class="ca-doc-actions">
        <a class="ca-doc-btn" :href="withBase(current.overview)">
          章节总览<span>→</span>
        </a>
      </div>
    </div>
  </header>
</template>
