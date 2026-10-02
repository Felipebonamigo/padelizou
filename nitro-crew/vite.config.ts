import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  // Carros da arte em glTF (src/assets/cars/*.glb, passo 2.4): o Vite trata como asset e embute com ?inline.
  assetsInclude: ['**/*.glb'],
  server: { port: 5174, host: true },
  preview: { port: 4174, host: true },
  build: { target: 'es2022', outDir: 'dist', sourcemap: true, chunkSizeWarningLimit: 1500 },
  test: { include: ['tests/**/*.test.ts'], environment: 'node' },
});
