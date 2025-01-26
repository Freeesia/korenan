import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./App.tsx";
import * as Sentry from "@sentry/react";

Sentry.init({
  dsn: "https://21dc6375520a0183f914419ad766f153@o351180.ingest.us.sentry.io/4508710538117120",
  integrations: [
    Sentry.browserTracingIntegration(),
    Sentry.replayIntegration(),
    Sentry.feedbackIntegration({
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
      successMessageText:
        "フィードバックありがとうございます。🙇送信が完了しました！💛",
    }),
  ],
  // Tracing
  tracesSampleRate: 1.0, //  Capture 100% of the transactions
  // Session Replay
  replaysSessionSampleRate: 0.1, // This sets the sample rate at 10%. You may want to change it to 100% while in development and then sample at a lower rate in production.
  replaysOnErrorSampleRate: 1.0, // If you're not already sampling the entire session, change the sample rate to 100% when sampling sessions where errors occur.
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>
);
