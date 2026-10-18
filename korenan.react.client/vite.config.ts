import { fileURLToPath, URL } from "node:url";
import { env } from "node:process";
import { defineConfig } from "vite";
import react, { reactCompilerPreset } from "@vitejs/plugin-react";
import babel from "@rolldown/plugin-babel";

const target =
  env.APISERVICE_HTTPS ??
  env.APISERVICE_HTTP ??
  env.services__apiservice__https__0 ??
  env.services__apiservice__http__0 ??
  "https://localhost:7013";

const proxy = {
  target,
  secure: false,
  changeOrigin: true,
};

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), babel({ presets: [reactCompilerPreset()] })],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    host: true,
    proxy: {
      "^/api": proxy,
      "^/openapi": proxy,
      "^/scalar": proxy,
    },
  },
});
