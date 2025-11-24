import { baseRequestClient, requestClient } from '#/api/request';

export namespace AuthApi {
  /** 登录接口参数 */
  export interface LoginParams {
    password?: string;
    username?: string;
  }

  /** 登录接口返回值 */
  export interface LoginResult {
    accessToken: string;
  }

  export interface RefreshTokenResult {
    data: string;
    status: number;
  }
}

/**
 * 登录 - 生成Basic认证凭证
 */
export async function loginApi(data: AuthApi.LoginParams) {
  // 生成Basic认证凭证: Base64(username:password)
  const { username = '', password = '' } = data;
  const basicCredentials = `Basic ${btoa(`${username}:${password}`)}`;
  
  // 直接返回包含凭证的结果，不需要实际调用登录接口
  // 后续请求拦截器会使用这个凭证进行认证
  return Promise.resolve({
    accessToken: basicCredentials,
    data: { accessToken: basicCredentials }
  });
}

/**
 * 刷新accessToken
 */
export async function refreshTokenApi() {
  return baseRequestClient.post<AuthApi.RefreshTokenResult>('/auth/refresh', {
    withCredentials: true,
  });
}

/**
 * 退出登录
 */
export async function logoutApi() {
  return baseRequestClient.post('/auth/logout', {
    withCredentials: true,
  });
}

/**
 * 获取用户权限码
 */
export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/codes');
}
