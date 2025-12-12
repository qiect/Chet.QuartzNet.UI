import { defineConfig } from '@vben/vite-config';

export default defineConfig(async () => {
  // 检查是否是构建模式
  const isBuild = process.env.NODE_ENV === 'production';
  
  return {
    application: {},
    vite: {
      // 只在构建时设置base
      base: isBuild ? '/quartz-ui/' : './',
      server: {
        proxy: {
          '/api': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            // mock代理目标地址
            target: 'http://localhost:5320/api',
            ws: true,
          },
        },
      },
      build: {
        outDir: '../../../Chet.QuartzNet.UI/wwwroot/quartz-ui',
        // 构建前清空输出目录
        emptyOutDir: true,
      },
    },
  };
});