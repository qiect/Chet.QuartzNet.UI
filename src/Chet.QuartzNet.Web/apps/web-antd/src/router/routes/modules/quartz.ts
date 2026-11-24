import type { RouteRecordRaw } from 'vue-router';

import { $t } from '#/locales';

const routes: RouteRecordRaw[] = [
  {
    name: 'Analytics',
    path: '/analytics',
    component: () => import('#/views/quartz/analytics.vue'),
    meta: {
      affixTab: true,
      icon: 'lucide:area-chart',
      // 页签缓存 + onActivated 静默刷新，与其他页签行为一致
      keepAlive: true,
      title: $t('page.quartz.analytics'),
    },
  },
  {
    name: 'Job',
    path: '/job',
    component: () => import('#/views/quartz/job.vue'),
    meta: {
      icon: 'ion:time-outline',
      keepAlive: true,
      title: $t('page.quartz.job'),
    },
  },
  {
    name: 'Log',
    path: '/log',
    component: () => import('#/views/quartz/log.vue'),
    meta: {
      icon: 'lucide:logs',
      keepAlive: true,
      // 带筛选 query 的深链与菜单入口复用同一个标签页，避免出现两个日志管理
      fullPathKey: false,
      title: $t('page.quartz.log'),
    },
  },
  {
    name: 'Notification',
    path: '/notification',
    component: () => import('#/views/quartz/notification.vue'),
    meta: {
      icon: 'lucide:bell',
      keepAlive: true,
      title: $t('page.quartz.notification'),
    },
  },
  {
    name: 'SystemConfig',
    path: '/system-config',
    component: () => import('#/views/quartz/system-config.vue'),
    meta: {
      icon: 'lucide:settings',
      keepAlive: true,
      title: $t('page.quartz.systemConfig'),
    },
  },
];

export default routes;
