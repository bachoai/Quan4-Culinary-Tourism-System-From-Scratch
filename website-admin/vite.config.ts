import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/antd/es/')) {
            const name = id.split('node_modules/antd/es/')[1]?.split('/')[0];
            return name ? `antd-${name}` : 'antd';
          }

          if (id.includes('node_modules/@ant-design/')) {
            const name = id.split('node_modules/@ant-design/')[1]?.split('/')[0];
            return name ? `antd-core-${name}` : 'antd-core';
          }

          if (id.includes('node_modules/rc-')) {
            const name = id.split('node_modules/')[1]?.split('/')[0];
            return name ? `antd-rc-${name}` : 'antd-rc';
          }

          if (id.includes('node_modules/recharts') || id.includes('node_modules/framer-motion')) {
            return 'charts';
          }

          if (
            id.includes('node_modules/react/') ||
            id.includes('node_modules/react-dom/') ||
            id.includes('node_modules/react-router-dom/')
          ) {
            return 'react';
          }

          if (
            id.includes('node_modules/@tanstack/react-query') ||
            id.includes('node_modules/axios') ||
            id.includes('node_modules/zustand')
          ) {
            return 'data';
          }

          if (
            id.includes('node_modules/@hookform/resolvers') ||
            id.includes('node_modules/react-hook-form') ||
            id.includes('node_modules/zod') ||
            id.includes('node_modules/lucide-react') ||
            id.includes('node_modules/dayjs')
          ) {
            return 'shared';
          }

          return undefined;
        },
      },
    },
  },
})
