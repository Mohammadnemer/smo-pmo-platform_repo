import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Dev-only convenience so the SPA can call relative /api/* paths
      // without CORS; production hosting (T2/T3) will route this for real.
      // No path rewrite: module endpoints (e.g. SmoEndpoints, ADR 0002) are
      // themselves mapped under /api/*, so the proxy target must see the
      // same path the SPA sent — matching how a real gateway would route it.
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
