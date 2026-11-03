import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

// Chet.QuartzNet.UI 官方门户与文档站配置
export default withMermaid(
  defineConfig({
    lang: 'zh-CN',
    title: 'Chet.QuartzNet.UI',
    description: '基于 .NET 8 / 10 与 Quartz.Net 的可视化作业调度管理系统',

    // GitHub Pages 部署：仓库名为 Chet.QuartzNet.UI
    base: '/Chet.QuartzNet.UI/',
    lastUpdated: true,
    cleanUrls: true,
    ignoreDeadLinks: 'localhostLinks',

    // Mermaid 11 的若干依赖（fastdom、dayjs、cytoscape 等）只提供 CommonJS 入口，
    // Vite 开发服务器按 ESM 解析时会报 “does not provide an export named 'default'”。
    // 显式预构建这些依赖，让 esbuild 完成 CJS → ESM 互操作包装。
    vite: {
      optimizeDeps: {
        include: [
          'mermaid',
          'fastdom',
          'dayjs',
          'debug',
          '@braintree/sanitize-url',
          'cytoscape',
          'cytoscape-cose-bilkent',
        ],
      },
    },

    head: [
      ['link', { rel: 'icon', type: 'image/svg+xml', href: '/Chet.QuartzNet.UI/logo.svg' }],
      ['link', { rel: 'apple-touch-icon', href: '/Chet.QuartzNet.UI/logo.png' }],
      ['meta', { name: 'theme-color', content: '#4E73DF' }],
    ],

    themeConfig: {
      logo: '/logo.svg',
      siteTitle: 'Chet.QuartzNet.UI',

      nav: [
        { text: '指南', link: '/guide/introduction', activeMatch: '/guide/' },
        { text: '功能模块', link: '/modules/overview', activeMatch: '/modules/' },
        { text: '系列教程', link: '/articles/overview', activeMatch: '/articles/' },
        { text: '更新日志', link: '/changelog' },
        { text: 'NuGet', link: 'https://www.nuget.org/packages/Chet.QuartzNet.UI' },
      ],

      sidebar: {
        '/guide/': [
          {
            text: '开始',
            collapsed: false,
            items: [
              { text: '项目简介', link: '/guide/introduction' },
              { text: '核心架构', link: '/guide/architecture' },
              { text: '快速开始', link: '/guide/quick-start' },
              { text: '配置说明', link: '/guide/configuration' },
            ],
          },
          {
            text: '存储与安全',
            collapsed: false,
            items: [
              { text: '存储模式', link: '/guide/storage' },
              { text: '数据迁移', link: '/guide/migration' },
            ],
          },
          {
            text: '进阶用法',
            collapsed: false,
            items: [
              { text: 'ClassJob 代码作业', link: '/guide/class-job' },
              { text: '作业依赖', link: '/guide/dependency' },
              { text: '通知配置', link: '/guide/notification' },
            ],
          },
          {
            text: '其他',
            collapsed: false,
            items: [
              { text: '部署与升级', link: '/guide/deployment' },
              { text: '常见问题', link: '/guide/faq' },
            ],
          },
        ],
        '/modules/': [
          {
            text: '功能模块',
            collapsed: false,
            items: [
              { text: '模块总览', link: '/modules/overview' },
              { text: '分析页', link: '/modules/dashboard' },
              { text: '作业管理', link: '/modules/job' },
              { text: '日志管理', link: '/modules/log' },
              { text: '通知管理', link: '/modules/notification' },
              { text: '系统配置', link: '/modules/system-config' },
              { text: '多语言与主题', link: '/modules/i18n' },
            ],
          },
        ],
        '/articles/': [
          {
            text: '系列教程',
            collapsed: false,
            items: [
              { text: '教程总览', link: '/articles/overview' },
              { text: '01 · 为什么选择它', link: '/articles/01-why-chet-quartznet-ui' },
              { text: '02 · 数据库存储详解', link: '/articles/02-database-storage' },
              { text: '03 · 通知功能详解', link: '/articles/03-notification-guide' },
              { text: '04 · VbenAdmin UI 体验', link: '/articles/04-vben-admin-ui' },
              { text: '05 · 分析页重构', link: '/articles/05-dashboard-refactor' },
              { text: '06 · 支持 .NET 10', link: '/articles/06-dotnet10-support' },
              { text: '07 · 新增作业指南', link: '/articles/07-create-job' },
              { text: '08 · 作业管理全攻略', link: '/articles/08-job-management' },
              { text: '09 · 日志管理指南', link: '/articles/09-log-management' },
              { text: '10 · 通知管理攻略', link: '/articles/10-notification-management' },
              { text: '11 · ClassJob 详解', link: '/articles/11-class-job' },
              { text: '12 · v2.2.0 多语言与迁移', link: '/articles/12-v2.2.0' },
              { text: '13 · v2.3.0 表格重构', link: '/articles/13-v2.3.0' },
              { text: '14 · v2.4 ~ v2.5.1 连更', link: '/articles/14-v2.5.1' },
              { text: '15 · v2.6.0 作业依赖', link: '/articles/15-v2.6.0' },
            ],
          },
        ],
      },

      socialLinks: [{ icon: 'github', link: 'https://github.com/qiect/Chet.QuartzNet.UI' }],

      footer: {
        message: '让 Quartz.Net 调度，从未如此简单',
        copyright: 'Copyright © 2026 Chet.QuartzNet.UI · MIT License',
      },

      search: {
        provider: 'local',
        options: {
          translations: {
            button: {
              buttonText: '搜索文档',
              buttonAriaLabel: '搜索文档',
            },
            modal: {
              noResultsText: '无法找到相关结果',
              resetButtonTitle: '清除查询条件',
              footer: {
                selectText: '选择',
                navigateText: '切换',
              },
            },
          },
        },
      },

      outline: {
        label: '本页目录',
        level: [2, 3],
      },

      docFooter: {
        prev: '上一页',
        next: '下一页',
      },

      lastUpdated: {
        text: '最后更新于',
      },

      returnToTopLabel: '回到顶部',
      sidebarMenuLabel: '菜单',
      darkModeSwitchLabel: '主题',
      lightModeSwitchTitle: '切换到浅色模式',
      darkModeSwitchTitle: '切换到深色模式',

      notFound: {
        code: '404',
        title: '页面不存在',
        quote: '你访问的页面可能已被移动、删除，或链接地址有误。',
        linkText: '返回首页',
        linkLabel: '返回首页',
      },
    },
  }),
)
