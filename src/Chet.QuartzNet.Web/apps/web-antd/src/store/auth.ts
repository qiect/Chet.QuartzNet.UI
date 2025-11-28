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
   * 异步处理登录操作 - JWT认证版本
   * Asynchronously handle the login process
   * @param params 登录表单数据
   */
  async function authLogin(
    params: Recordable<any>,
    onSuccess?: () => Promise<void> | void,
  ) {
    // JWT认证登录流程
    let userInfo: null | UserInfo = null;
    try {
      loginLoading.value = true;
      const response = await loginApi(params);
      const accessToken = response.data?.accessToken || response.accessToken;

      // 如果成功获取到 accessToken
      if (accessToken) {
        // 设置JWT token，格式为 "Bearer {token}"
        accessStore.setAccessToken(`Bearer ${accessToken}`);
        
        // 根据用户名创建用户信息对象
        const username = params.username || '';
        userInfo = {
          username,
          realName: username, // 使用用户名作为显示名称
          homePath: preferences.app.defaultHomePath
        } as UserInfo;
        
        // 设置用户信息
        userStore.setUserInfo(userInfo);
        
        // 设置默认的访问权限
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
      } else {
        // 登录失败，没有获取到accessToken
        notification.error({
          description: '用户名或密码错误，请重试',
          duration: 3,
          message: '登录失败',
        });
      }
    } catch (error: any) {
      // 捕获登录API异常
      notification.error({
        description: error.message || '用户名或密码错误，请重试',
        duration: 3,
        message: '登录失败',
      });
    } finally {
      loginLoading.value = false;
    }

    return {
      userInfo,
    };
  }

  async function logout(redirect: boolean = true) {
    // JWT认证登出流程
    // 清除所有store数据
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
    // 在JWT认证中，可以从token中提取用户信息
    let userInfo: null | UserInfo = {
      username: 'jwt-auth-user',
      realName: 'JWT Auth User',
      homePath: preferences.app.defaultHomePath
    } as UserInfo;
    
    // 尝试从accessToken中提取信息（如果可能）
    try {
      const token = accessStore.accessToken;
      if (token && token.startsWith('Bearer ')) {
        // 这里可以解析JWT token获取用户信息
        // 简单实现：使用默认用户名
        const username = userStore.userInfo?.username || 'admin';
        userInfo.username = username;
        userInfo.realName = username;
      }
    } catch (error) {
      console.warn('Failed to extract user info from JWT token', error);
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
