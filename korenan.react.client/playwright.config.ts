import { defineConfig, devices } from "@playwright/test";
import process from "node:process";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  workers: 3,
  forbidOnly: !!process.env.CI,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: "http://127.0.0.1:4173",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    serviceWorkers: "block",
  },
  projects: [
    { name: "chromium-desktop", use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 900 }, colorScheme: "dark" } },
    { name: "chromium-mobile", use: { browserName: "chromium", viewport: { width: 375, height: 667 }, isMobile: true, hasTouch: true, colorScheme: "light" } },
    { name: "webkit-mobile", use: { ...devices["iPhone 13"], browserName: "webkit", colorScheme: "light" } },
  ],
  // 起動するのはフロントエンド配信のみ。API・Redis・AIサービスは起動しない。
  webServer: {
    command: `"${process.execPath}" node_modules/vite/bin/vite.js --config tests/vite.config.ts --port 4173 --strictPort`,
    url: "http://127.0.0.1:4173",
    reuseExistingServer: false,
  },
});
