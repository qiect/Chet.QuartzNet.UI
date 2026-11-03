import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import Layout from './Layout.vue'
import HomeLanding from './components/HomeLanding.vue'
import './theme.css'

export default {
  extends: DefaultTheme,
  Layout,
  enhanceApp({ app }) {
    app.component('HomeLanding', HomeLanding)
  },
} satisfies Theme
