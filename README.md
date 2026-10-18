# これなーんだ❓(ライアー)

AI にお題について質問しながら正解を当て、紛れ込んだライアーを見破るパーティーゲームです。
ゲーム仕様は [specification.md](specification.md) を参照してください。

## 構成

| プロジェクト | 内容 |
| --- | --- |
| `korenan.AppHost` | Aspire 13 の AppHost（API・Redis・Vite を起動） |
| `korenan.ApiService` | ASP.NET Core (.NET 10) の API。エンドポイントとゲーム進行は `Program.cs` に集約 |
| `korenan.ServiceDefaults` | OpenTelemetry・ヘルスチェックなどの共通設定 |
| `korenan.react.client` | React 19 + Vite + CSS Modules のフロントエンド |

- シーン更新は SSE (`GET /api/scene/stream`) で配信します。
- AI 呼び出しは Microsoft.Extensions.AI の `IChatClient` / `IImageGenerator` で抽象化し、Gemini と OpenAI 系を設定で切り替えます。
- 検索は Tavily と Wikipedia を使います。
- 状態は Redis に保存します（揮発）。

## 設定

`korenan.ApiService` のユーザーシークレットまたは環境変数で設定します。

| キー | 説明 |
| --- | --- |
| `AI:Provider` | `Gemini` または `OpenAI` |
| `AI:ApiKey` | プロバイダの API キー |
| `AI:Endpoint` | OpenAI 系のみ。Azure OpenAI や OpenAI 互換 API のエンドポイント（省略時は OpenAI） |
| `AI:Models:Judge` | 回答・正解判定用モデル |
| `AI:Models:Light` | キーワード抽出・要約用の軽量モデル |
| `AI:Models:Bot` | Bot の質問生成用モデル |
| `AI:Models:Image` | キャラクター画像生成用モデル。空文字で画像生成を無効化し、デフォルト画像を使用 |
| `Tavily:ApiKey` | Tavily の API キー |

モデルを省略した場合はプロバイダごとの既定値を使用します。

```bash
cd korenan.ApiService
dotnet user-secrets set "AI:Provider" "Gemini"
dotnet user-secrets set "AI:ApiKey" "<api key>"
dotnet user-secrets set "Tavily:ApiKey" "<api key>"
```

旧設定の `SemanticKernelOptions:ApiKey` / `SemanticKernelOptions:TavilyApiKey` / `SemanticKernelOptions:ModelId` は、それぞれ `AI:ApiKey` / `Tavily:ApiKey` / `AI:Models:Judge` に移行してください。

フロントエンドの Sentry はビルド時の環境変数 `VITE_SENTRY_DSN` を設定した本番ビルドでのみ有効になります。

## 開発

```bash
cd korenan.react.client && npm ci && cd ..
dotnet run --project korenan.AppHost
```

Aspire Dashboard から API と Vite の開発サーバを開けます。API ドキュメントは `/scalar` で確認できます。

API を変更した場合はクライアントの型を再生成します。

```bash
cd korenan.react.client
npm run gen:openapi
npm run gen:api
```

## Docker イメージ

```bash
docker buildx build -f korenan.ApiService/Dockerfile --build-arg VITE_SENTRY_DSN=<dsn> -t ghcr.io/freeesia/korenan:latest .
```

`main` ブランチへの push で GitHub Actions が `ghcr.io/freeesia/korenan` に `latest` とコミット SHA のタグでイメージを公開します。
