import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],

    // Playwright specs live under src/test/playwright and are driven by Playwright,
    // not Vitest. Without this exclusion Vitest tries to run them and fails on an
    // import it cannot resolve.
    exclude: ['src/test/playwright/**', 'node_modules/**'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/**/*.stories.tsx',
        'src/test/**',
        'src/services/generated/**',
        'src/main.tsx',
        'src/vite-env.d.ts',
      ],
    },
  },
})
