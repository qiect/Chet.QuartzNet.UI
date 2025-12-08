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
      title: $t('page.quartz.analytics'),
    },
  },
  {
    name: 'JobManagement',
    path: '/job-management',
    component: () => import('#/views/quartz/job-management.vue'),
    meta: {
      icon: 'ion:time-outline',
      title: $t('page.quartz.jobManagement'),
    },
  },
  {
    name: 'LogManagement',
    path: '/log-management',
    component: () => import('#/views/quartz/log-management.vue'),
    meta: {
      icon: 'lucide:logs',
      title: $t('page.quartz.logManagement'),
    },
  },
  {
    name: 'NotificationManagement',
    path: '/notification-management',
    component: () => import('#/views/quartz/notification-management.vue'),
    meta: {
      icon: 'lucide:bell',
      title: $t('page.quartz.notificationManagement'),
    },
  },
];

export default routes;
