import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/courses': 'http://localhost:5221',
      '/tee-sheets': 'http://localhost:5221',
      '/health': 'http://localhost:5221',
    },
  },
})
