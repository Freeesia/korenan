import { StrictMode, useEffect } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, useLocation, useNavigationType, createRoutesFromChildren, matchRoutes } from "react-router";
import * as Sentry from "@sentry/react";
import "./styles/global.css";
import App from "./App.tsx";
import { GameProvider } from "./context/GameProvider.tsx";

const sentryDsn = import.meta.env.VITE_SENTRY_DSN;

// デプロイ環境かつDSNが設定されている場合のみSentryを初期化
if (import.meta.env.PROD && sentryDsn) {
  Sentry.init({
    dsn: sentryDsn,
    integrations: [
      Sentry.browserTracingIntegration(),
      Sentry.replayIntegration(),
      Sentry.feedbackIntegration({
        autoInject: false,
        colorScheme: "system",
        showName: false,
        showEmail: false,
        triggerLabel: "フィードバックを送る",
        formTitle: "フィードバックを送る",
        submitButtonLabel: "送信",
        cancelButtonLabel: "キャンセル",
        confirmButtonLabel: "OK",
        addScreenshotButtonLabel: "スクショを追加",
        removeScreenshotButtonLabel: "スクショを削除",
        isRequiredLabel: "必須",
        messageLabel: "メッセージ",
        messagePlaceholder: "改善するとよりゲームが面白くなりそうな点を詳細に記入よろしくお願いします。",
        successMessageText: "フィードバックありがとうございます。🙇送信が完了しました！💛",
      }),
      Sentry.reactRouterV7BrowserTracingIntegration({
        useEffect,
        useLocation,
        useNavigationType,
        createRoutesFromChildren,
        matchRoutes,
      }),
    ],
    tracesSampleRate: 1.0,
    replaysSessionSampleRate: 0.1,
    replaysOnErrorSampleRate: 1.0,
  });
}

const container = document.getElementById("root");
const root = createRoot(container!, {
  onUncaughtError: Sentry.reactErrorHandler((error, errorInfo) => {
    console.warn("Uncaught error", error, errorInfo.componentStack);
  }),
  onCaughtError: Sentry.reactErrorHandler(),
  onRecoverableError: Sentry.reactErrorHandler(),
});
root.render(
  <StrictMode>
    <BrowserRouter>
      <GameProvider>
        <App />
      </GameProvider>
    </BrowserRouter>
  </StrictMode>,
);
