import type { RouteRecordRaw } from 'vue-router';

import { $t } from '#/locales';

const routes: RouteRecordRaw[] = [
  {
    meta: {
      icon: 'lucide:layout-dashboard',
      order: -1,
      title: $t('page.dashboard.title'),
    },
    name: 'Dashboard',
    path: '/dashboard',
    children: [
      {
        name: 'Analytics',
        path: '/analytics',
        component: () => import('#/views/dashboard/analytics/index.vue'),
        meta: {
          affixTab: true,
          icon: 'lucide:area-chart',
          title: $t('page.dashboard.analytics'),
        },
      },
      {
        name: 'JobManagement',
        path: '/quartz/job-management',
        component: () => import('#/views/quartz/job-management.vue'),
        meta: {
          icon: 'ion:time-outline',
          title: $t('作业管理'),
        },
      },
      {
        name: 'LogManagement',
        path: '/quartz/log-management',
        component: () => import('#/views/quartz/log-management.vue'),
        meta: {
          icon: 'lucide:logs',
          title: $t('日志管理'),
        },
      },
    ],
  },
];

export default routes;
