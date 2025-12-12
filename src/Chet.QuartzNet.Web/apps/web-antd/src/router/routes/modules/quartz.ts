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
    name: 'Job',
    path: '/job',
    component: () => import('#/views/quartz/job.vue'),
    meta: {
      icon: 'ion:time-outline',
      title: $t('page.quartz.job'),
    },
  },
  {
    name: 'Log',
    path: '/log',
    component: () => import('#/views/quartz/log.vue'),
    meta: {
      icon: 'lucide:logs',
      title: $t('page.quartz.log'),
    },
  },
  {
    name: 'Notification',
    path: '/notification',
    component: () => import('#/views/quartz/notification.vue'),
    meta: {
      icon: 'lucide:bell',
      title: $t('page.quartz.notification'),
    },
  },
];

export default routes;
