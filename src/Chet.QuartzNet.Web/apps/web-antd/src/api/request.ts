/**
 * 该文件可自行根据业务逻辑进行调整
 */
import type { RequestClientOptions } from '@vben/request';

import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import {
  authenticateResponseInterceptor,
  defaultResponseInterceptor,
  errorMessageResponseInterceptor,
  RequestClient,
} from '@vben/request';
import { useAccessStore } from '@vben/stores';

import { message } from 'ant-design-vue';

import { useAuthStore } from '#/store';

import { refreshTokenApi } from './core';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

function createRequestClient(baseURL: string, options?: RequestClientOptions) {
  const client = new RequestClient({
    ...options,
    baseURL,
  });

/**
   * 重新认证逻辑
   */
  async function doReAuthenticate() {
    const accessStore = useAccessStore();
    const authStore = useAuthStore();

    // 如果已经在处理过期逻辑了，直接返回，不再重复执行登出和标记
    if (accessStore.loginExpired) {
      return;
    }

    console.warn('Access token is invalid. Redirecting to login.');
    
    // 立即清除当前 token，这会让后续并发请求进入逻辑时感知到状态已变
    accessStore.setAccessToken(null);
    
    // 标记为已过期（这在 Vben 内部会被很多逻辑用来做判断锁）
    accessStore.setLoginExpired(true);

    if (preferences.app.loginExpiredMode === 'modal' && accessStore.isAccessChecked) {
       // 模式为弹窗时由 UI 层处理
    } else {
      // 模式为跳转时，执行登出
      await authStore.logout();
    }
  }

  /**
   * 刷新token逻辑
   */
  async function doRefreshToken() {
    const accessStore = useAccessStore();
    const resp = await refreshTokenApi();
    const newToken = resp.data;
    accessStore.setAccessToken(newToken);
    return newToken;
  }

  // Basic认证不需要额外格式化，直接使用凭证
  function formatToken(token: null | string) {
    // 对于Basic认证，直接返回token，因为它已经包含了"Basic "前缀
    return token;
  }

  // 请求头处理
  client.addRequestInterceptor({
    fulfilled: async (config) => {
      const accessStore = useAccessStore();

      // 直接使用Basic认证凭证
      config.headers.Authorization = accessStore.accessToken || '';
      config.headers['Accept-Language'] = preferences.app.locale;
      return config;
    },
  });

  // 处理返回的响应数据格式
  client.addResponseInterceptor({
    fulfilled: (response) => {
      // 直接返回响应数据，不进行额外格式化
      // 这样可以保持API返回的原始格式（包含success、message、data、errorCode等字段）
      return response.data;
    },
    rejected: (error) => {
      // 处理网络错误或其他请求错误
      return Promise.reject(error);
    }
  });
  
  // 移除默认的响应拦截器，使用上面的自定义拦截器
  // client.addResponseInterceptor(
  //   defaultResponseInterceptor({
  //     codeField: 'code',
  //     dataField: 'data',
  //     successCode: 0,
  //   }),
  // );

  // 对于Basic认证，token过期处理通常不需要刷新token
  // 而是直接跳转到登录页面重新输入凭证
  client.addResponseInterceptor(
    authenticateResponseInterceptor({
      client,
      doReAuthenticate,
      // Basic认证通常不需要刷新token
      // doRefreshToken: async () => {
      //   await doReAuthenticate();
      //   return null;
      // },
      enableRefreshToken: false, // 禁用token刷新
      formatToken,
    }),
  );

  // 通用的错误处理
  client.addResponseInterceptor(
    errorMessageResponseInterceptor((msg: string, error) => {
      const accessStore = useAccessStore();

      // 如果状态码是 401，或者 accessStore 已经标记登录过期
      // 静默处理，不调用 message.error
      if (error?.response?.status === 401 || accessStore.loginExpired) {
        return;
      }

      const responseData = error?.response?.data ?? {};
      const errorMessage = responseData?.error ?? responseData?.message ?? '';
      
      message.error(errorMessage || msg);
    }),
  );

  // 【关键修复】最终拦截器：吞掉 401 错误，阻止其传播到页面级 catch 块
  // authenticateResponseInterceptor 已处理跳转逻辑，
  // 但 rejected promise 仍会传播到各页面的 catch 块导致 message.error 弹出
  // 返回一个永远 pending 的 Promise 可以优雅地阻断错误传播
  client.addResponseInterceptor({
    rejected: (error) => {
      const accessStore = useAccessStore();
      if (error?.response?.status === 401 || accessStore.loginExpired) {
        // 吞掉错误，防止页面 catch 块弹出"请求失败"等提示
        // 页面即将因登出而销毁，pending promise 不会造成内存泄漏
        return new Promise(() => {});
      }
      return Promise.reject(error);
    },
  });

  return client;
}

export const requestClient = createRequestClient(apiURL, {
  // 移除responseReturn配置，避免自动提取data字段
  // responseReturn: 'data',
});

export const baseRequestClient = new RequestClient({ baseURL: apiURL });

