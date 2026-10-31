import { defineConfig } from "vite";
import appConfig from "../vite.config.ts";

// 通常と同じフロントエンドを配信するが、APIへの転送は設定しない。
// ブラウザー終了時のSSE再接続もバックエンドへ到達させない。
export default defineConfig({
  ...appConfig,
  server: { ...appConfig.server, host: "127.0.0.1", proxy: {} },
});
