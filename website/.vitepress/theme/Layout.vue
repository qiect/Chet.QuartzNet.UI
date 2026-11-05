<script setup lang="ts">
import DefaultTheme from 'vitepress/theme'
import { computed } from 'vue'
import { useData } from 'vitepress'
import DocHero from './components/DocHero.vue'

const { frontmatter, page } = useData()

const isDocPage = computed(
  () => (!frontmatter.value.layout || frontmatter.value.layout === 'doc') && !page.value.isNotFound,
)
</script>

<template>
  <DefaultTheme.Layout>
    <template #doc-before>
      <DocHero v-if="isDocPage" />
    </template>
  </DefaultTheme.Layout>
</template>
