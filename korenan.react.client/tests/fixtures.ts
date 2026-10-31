import { test as base, expect, type Request, type Route } from "@playwright/test";
import { resolve } from "node:path";
import type { Config, CurrentScene, HistoryInfo, PlayerResult, RoundResult } from "../src/api/client";

export const ids = {
  me: "11111111-1111-4111-8111-111111111111",
  haru: "22222222-2222-4222-8222-222222222222",
  midori: "33333333-3333-4333-8333-333333333333",
};
const players = [
  { id: ids.me, name: "あおい", points: 0 },
  { id: ids.haru, name: "はる", points: 0 },
  { id: ids.midori, name: "みどり", points: 0 },
];

export const config: Config = {
  questionLimit: 8, answerLimit: 3, correctPoint: 20, liarPoint: 30,
  noCorrectPoint: -10, inactivityThresholdMinutes: 0.5, aiQuestionThreshold: 0.5, notifyLiar: true,
};

function scene(name: CurrentScene["scene"], info: CurrentScene["info"], round = 1): CurrentScene {
  return {
    id: "screen-test-room", aikotoba: "画面確認", theme: "身近なもの", hostId: ids.me,
    scene: name, round, players: players.map((player) => ({ ...player, currentScene: name })), info,
  };
}

export const waitingScene = () => scene("WaitRoundStart", { type: "WaitRound", waiting: 3 }, 0);
export const selectingScene = (round = 0) => scene("TopicSelecting", { type: "TopicSelecting" }, round);
export const questionScene = (histories: PlayerResult[] = [], isLiar = false, round = 1) =>
  scene("QuestionAnswering", { type: "QuestionAnswering", histories, isLiar }, round);
export const question = (index: number): PlayerResult => ({
  type: "Question", player: ids.haru, question: `質問 ${index}：身近にあるものですか？`, result: "Yes",
});

const histories: HistoryInfo[] = [
  { result: { type: "Question", player: ids.haru, question: "動物ですか？", result: "Yes" }, reason: "ねこは動物です。", postedAt: "2026-10-31T00:00:00Z" },
  { result: { type: "Answer", player: ids.me, answer: "ねこ", result: "Correct" }, reason: "完全一致", postedAt: "2026-10-31T00:00:10Z" },
];

export const guessScene = () => scene("LiarGuess", {
  type: "LiarGuess", topic: "ねこ", topicCorrectPlayers: [ids.me], guessedPlayers: [ids.haru, ids.midori], histories,
});

export const roundResults: RoundResult[] = [
  {
    topic: "ねこ", topicCorrectPlayers: [ids.me], liarPlayers: [ids.haru], liarCorrectPlayers: [ids.me, ids.midori], histories,
    liarGuesses: [{ player: ids.me, target: ids.haru }, { player: ids.haru, target: ids.midori }, { player: ids.midori, target: ids.haru }],
    scores: [
      { player: ids.me, topicPoints: 20, liarPoints: 30, noCorrectPoints: 0, total: 50 },
      { player: ids.haru, topicPoints: 0, liarPoints: 0, noCorrectPoints: 0, total: 0 },
      { player: ids.midori, topicPoints: 0, liarPoints: 30, noCorrectPoints: 0, total: 30 },
    ],
  },
  {
    topic: "りんご", topicCorrectPlayers: [], liarPlayers: [ids.me], liarCorrectPlayers: [ids.haru, ids.midori],
    histories: [{ result: { type: "Answer", player: ids.haru, answer: "みかん", result: "Incorrect" }, reason: "りんごとは異なる果物です。", postedAt: "2026-10-31T00:01:00Z" }],
    liarGuesses: [{ player: ids.me, target: ids.haru }, { player: ids.haru, target: ids.me }, { player: ids.midori, target: ids.me }],
    scores: [
      { player: ids.me, topicPoints: 0, liarPoints: 0, noCorrectPoints: -10, total: -10 },
      { player: ids.haru, topicPoints: 0, liarPoints: 30, noCorrectPoints: 0, total: 30 },
      { player: ids.midori, topicPoints: 0, liarPoints: 30, noCorrectPoints: 0, total: 30 },
    ],
  },
];

export function endScene(results = roundResults): CurrentScene {
  return {
    ...scene("GameEnd", { type: "GameEnd", results }, results.length),
    players: players.map((player) => ({
      ...player, currentScene: "GameEnd",
      points: results.reduce((total, result) => total + (result.scores.find((score) => score.player === player.id)?.total ?? 0), 0),
    })),
  };
}

interface ScreenApi {
  setScene: (value: CurrentScene | null) => void;
  holdPost: (path: string) => Promise<{ request: Promise<Request>; respond: (json?: unknown, status?: number) => Promise<void> }>;
}

export const test = base.extend<{ api: ScreenApi }>({
  api: async ({ page }, provide) => {
    let current: CurrentScene | null = null;
    const unexpected: string[] = [];
    const errors: string[] = [];
    page.on("pageerror", (error) => { errors.push(error.message); });
    await page.route("**/*", async (route) => {
      const url = new URL(route.request().url());
      if (url.origin !== "http://127.0.0.1:4173") {
        unexpected.push(route.request().url());
        return route.abort("blockedbyclient");
      }
      return route.continue();
    });
    await page.route(/^http:\/\/127\.0\.0\.1:4173\/api\//, async (route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      if (request.method() === "GET") {
        if (path === "/api/me") return route.fulfill({ json: { id: players[0].id, name: players[0].name } });
        if (path === "/api/config") return route.fulfill({ json: config });
        if (path === "/api/scene/stream") {
          // EventSource自体はブラウザーの実装を使い、再接続時に通常のSSEフレームを返す。
          const event = current === null ? "none" : "scene";
          return route.fulfill({
            contentType: "text/event-stream", headers: { "cache-control": "no-cache" },
            body: `retry: 50\nevent: ${event}\ndata: ${JSON.stringify(current)}\n\n`,
          });
        }
        if (path.startsWith("/api/image/bot/")) {
          return route.fulfill({ contentType: "image/png", path: resolve(import.meta.dirname, "../../korenan.ApiService/ai_character01_smile.png") });
        }
      }
      unexpected.push(`${request.method()} ${path}`);
      return route.fulfill({ status: 404, json: { detail: "このテストで想定していないリクエストです" } });
    });

    await provide({
      setScene: (value) => { current = value; },
      holdPost: async (path) => {
        let capture!: (route: Route) => void;
        const pending = new Promise<Route>((resolve) => { capture = resolve; });
        await page.route(`http://127.0.0.1:4173/api/${path}`, (route) => { capture(route); }, { times: 1 });
        return {
          request: pending.then((route) => route.request()),
          respond: async (json, status = 200) => {
            const route = await pending;
            await route.fulfill(json === undefined ? { status, body: "" } : { status, json });
          },
        };
      },
    });
    await page.close();
    expect(unexpected, "想定外のAPI呼び出し・外部通信がないこと").toEqual([]);
    expect(errors, "画面で未処理のエラーがないこと").toEqual([]);
  },
});

export { expect };
