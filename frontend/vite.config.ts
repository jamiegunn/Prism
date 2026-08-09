import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        // dev.sh moves the API off 5000 when something else already has it —
        // on macOS, AirPlay Receiver holds that port by default. It exports the
        // port it settled on, so the proxy follows rather than silently
        // pointing at whatever else is listening.
        target: process.env.PRISM_API_URL || 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
