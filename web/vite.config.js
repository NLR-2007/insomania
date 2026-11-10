import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // Relative base so the built site works from a subfolder
  // (GitHub Pages project sites, a CDN path, or opened from disk).
  base: './',
  build: { outDir: 'dist', assetsInlineLimit: 8192 },
})
