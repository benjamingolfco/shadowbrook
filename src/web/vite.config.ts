import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/courses': 'http://localhost:5221',
      '/tee-sheets': 'http://localhost:5221',
      '/tenants': 'http://localhost:5221',
      '/health': 'http://localhost:5221',
      '/walkup': 'http://localhost:5221',
      '/waitlist': 'http://localhost:5221',
      '/dev': 'http://localhost:5221',
    },
  },
})
