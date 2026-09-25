import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  server: { port: 5174, host: true },
  preview: { port: 4174, host: true },
  build: { target: 'es2022', outDir: 'dist', sourcemap: true, chunkSizeWarningLimit: 1500 },
  test: { include: ['tests/**/*.test.ts'], environment: 'node' },
});
