import type { Recordable, UserInfo } from '@vben/types';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { LOGIN_PATH } from '@vben/constants';
import { preferences } from '@vben/preferences';
import { resetAllStores, useAccessStore, useUserStore } from '@vben/stores';

import { notification } from 'ant-design-vue';
import { defineStore } from 'pinia';

import { getAccessCodesApi, getUserInfoApi, loginApi, logoutApi } from '#/api';
import { $t } from '#/locales';

export const useAuthStore = defineStore('auth', () => {
  const accessStore = useAccessStore();
  const userStore = useUserStore();
  const router = useRouter();

  const loginLoading = ref(false);

  /**
   * 异步处理登录操作 - Basic认证版本
   * Asynchronously handle the login process
   * @param params 登录表单数据
   */
  async function authLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ) {
    // Basic认证登录流程
    let userInfo: null | UserInfo = null;
    try {
      loginLoading.value = true;
      const { accessToken } = await loginApi(params);

      // 如果成功获取到 accessToken
      if (accessToken) {
        accessStore.setAccessToken(accessToken);
        
        // 在Basic认证中，我们可以根据用户名创建一个基本的用户信息对象
        // 实际项目中可能需要从后端获取更详细的用户信息
        const username = params.username || '';
        userInfo = {
          username,
          realName: username, // 使用用户名作为显示名称
          homePath: preferences.app.defaultHomePath
        } as UserInfo;
        
        // 设置用户信息
        userStore.setUserInfo(userInfo);
        
        // 对于Basic认证，可以设置默认的访问权限
        accessStore.setAccessCodes(['*']); // 假设给所有权限

        if (accessStore.loginExpired) {
          accessStore.setLoginExpired(false);
        } else {
          onSuccess
            ? await onSuccess?.()
            : await router.push(
                userInfo.homePath || preferences.app.defaultHomePath,
              );
        }

        notification.success({
          description: `${$t('authentication.loginSuccessDesc')}:${userInfo?.realName}`,
          duration: 3,
          message: $t('authentication.loginSuccess'),
        });
      }
    } finally {
      loginLoading.value = false;
    }

    return {
      userInfo,
    };
  }

  async function logout(redirect: boolean = true) {
    // Basic认证不需要调用logout接口
    // 直接清除本地存储的凭证即可
    resetAllStores();
    accessStore.setLoginExpired(false);

    // 回登录页带上当前路由地址
    await router.replace({
      path: LOGIN_PATH,
      query: redirect
        ? {
            redirect: encodeURIComponent(router.currentRoute.value.fullPath),
          }
        : {},
    });
  }

  async function fetchUserInfo() {
    // 在Basic认证中，如果没有实际的用户信息接口
    // 可以从存储的凭证中提取用户名或使用默认值
    let userInfo: null | UserInfo = {
      username: 'basic-auth-user',
      realName: 'Basic Auth User',
      homePath: preferences.app.defaultHomePath
    } as UserInfo;
    
    // 尝试从accessToken中提取用户名（如果可能）
    try {
      const token = accessStore.accessToken;
      if (token && token.startsWith('Basic ')) {
        const encodedCredentials = token.substring(6);
        const decodedCredentials = atob(encodedCredentials);
        const username = decodedCredentials.split(':')[0];
        if (username) {
          userInfo.username = username;
          userInfo.realName = username;
        }
      }
    } catch (error) {
      console.warn('Failed to extract username from Basic auth token', error);
    }
    
    userStore.setUserInfo(userInfo);
    return userInfo;
  }

  function $reset() {
    loginLoading.value = false;
  }

  return {
    $reset,
    authLogin,
    fetchUserInfo,
    loginLoading,
    logout,
  };
});
