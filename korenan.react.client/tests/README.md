# バックエンドなしの画面テスト

Node.js 22.22以降、または24以降で実行します。

```sh
npm ci
npx playwright install chromium webkit
npm run test:ui
```

フロントエンドのViteだけをループバックアドレスで一時起動します。.NET API、Redis、AI・Tavilyの設定は不要です。
Playwrightが実際のアプリのHTTPリクエストへ応答し、SSEには通常の`event: scene`フレームを返します。
`EventSource`、ReactのContext、画面コンポーネントは差し替えず、通常の画面を操作します。外部通信と想定外のAPI呼び出しはテストを失敗させます。

PC幅のChromium、スマホ幅のChromium、iPhoneサイズのWebKitで、抽選前後の遷移、スクロール追従、回答待ち、通知の閉じる操作、最終結果の内訳・履歴を確認します。
AIの回答品質、バックエンドの得点計算、実機のマイク権限・音声認識は対象外です。WebKitでの画面テストはiPhone実機確認とは別です。

`npm run test:ui:report`でHTMLレポートを開けます。スクリーンショットは`test-results/`に保存されます。
