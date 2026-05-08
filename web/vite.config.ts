import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const proxyTarget = process.env.VITE_API_PROXY_TARGET ?? `http://127.0.0.1:${process.env.VITE_API_PORT ?? "5001"}`;
const devPort = Number.parseInt(process.env.VITE_PORT ?? "8080", 10);
const previewPort = Number.parseInt(process.env.VITE_PREVIEW_PORT ?? "4173", 10);

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    port: Number.isNaN(devPort) ? 8080 : devPort,
    strictPort: false,
    proxy: {
      "/api": {
        target: proxyTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ""),
      },
    },
  },
  preview: {
    host: true,
    port: Number.isNaN(previewPort) ? 4173 : previewPort,
    strictPort: false,
    proxy: {
      "/api": {
        target: proxyTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ""),
      },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: "./tests/setup.ts",
  },
});
