import { test, expect, ids, endScene, guessScene, question, questionScene, roundResults, selectingScene, waitingScene } from "./fixtures";
import type { Locator, Page } from "@playwright/test";

const timelineOf = (page: Page) => page.getByRole("list", { name: "質問と解答の履歴" });
const bottomGap = (timeline: Locator) => timeline.evaluate((element) => element.scrollHeight - element.scrollTop - element.clientHeight);

async function expectLatestVisible(page: Page) {
  const timeline = timelineOf(page);
  await expect.poll(() => bottomGap(timeline)).toBeLessThanOrEqual(4);
  const message = await timeline.getByRole("listitem").last().boundingBox();
  const input = await page.getByRole("textbox", { name: "質問", exact: true }).boundingBox();
  expect(message).not.toBeNull();
  expect(input).not.toBeNull();
  expect(message!.y + message!.height).toBeLessThanOrEqual(input!.y);
}

test("開始した本人もAPIの完了を待たずに抽選画面へ移る", async ({ page, api }) => {
  api.setScene(waitingScene());
  const start = await api.holdPost("start");
  await page.goto("/");
  await page.getByRole("button", { name: "ラウンド開始❗" }).click();
  await start.request;
  api.setScene(selectingScene());
  await expect(page).toHaveURL(/\/TopicSelecting$/i);
  await expect(page.getByRole("heading", { name: "お題抽選中" })).toBeVisible();
  api.setScene(questionScene());
  await expect(page).toHaveURL(/\/QuestionAnswering$/i);
  await start.respond();
});

test("最後に推理した本人もAPIの完了を待たずに抽選画面へ移る", async ({ page, api }) => {
  api.setScene(guessScene());
  const guess = await api.holdPost("guess");
  await page.goto("/");
  await page.getByRole("radio", { name: "はる" }).check();
  await page.getByRole("button", { name: "ライアー！", exact: true }).click();
  expect((await guess.request).postDataJSON()).toBe(ids.haru);
  api.setScene(selectingScene(1));
  await expect(page).toHaveURL(/\/TopicSelecting$/i);
  await expect(page.getByRole("heading", { name: "お題抽選中" })).toBeVisible();
  api.setScene(questionScene([], false, 2));
  await expect(page).toHaveURL(/\/QuestionAnswering$/i);
  await guess.respond();
});

test("スクロールのない状態から初めて増えた投稿が入力欄に隠れない", async ({ page, api }) => {
  api.setScene(questionScene());
  await page.goto("/");
  const timeline = timelineOf(page);
  await expect(timeline).toBeVisible();
  expect(await bottomGap(timeline)).toBe(0);
  api.setScene(questionScene([question(1)]));
  await expect(timeline.getByRole("listitem")).toHaveCount(1);
  await expectLatestVisible(page);
});

test("最下部では新着に追従し、履歴を読んでいる間は位置を保つ", async ({ page, api }, testInfo) => {
  const histories = Array.from({ length: 25 }, (_, index) => question(index + 1));
  api.setScene(questionScene(histories));
  await page.goto("/");
  const timeline = timelineOf(page);
  await expect(timeline.getByRole("listitem")).toHaveCount(25);
  await expectLatestVisible(page);
  api.setScene(questionScene([...histories, question(26)]));
  await expect(timeline.getByRole("listitem")).toHaveCount(26);
  await expectLatestVisible(page);
  await page.screenshot({ path: testInfo.outputPath("chat-bottom.png"), animations: "disabled" });

  await timeline.press("Home");
  await expect.poll(() => timeline.evaluate((element) => element.scrollTop)).toBeLessThan(50);
  const before = await timeline.evaluate((element) => element.scrollTop);
  api.setScene(questionScene([...histories, question(26), question(27)]));
  await expect(timeline.getByRole("listitem")).toHaveCount(27);
  await expect.poll(() => timeline.evaluate((element) => element.scrollTop)).toBeCloseTo(before, 0);
  expect(await bottomGap(timeline)).toBeGreaterThan(100);

  await timeline.press("End");
  await expectLatestVisible(page);
  api.setScene(questionScene([...histories, question(26), question(27), question(28)]));
  await expect(timeline.getByRole("listitem")).toHaveCount(28);
  await expectLatestVisible(page);
});

test("質問の回答待ちはアニメーションし、返答後に消える", async ({ page, api }, testInfo) => {
  api.setScene(questionScene());
  const answer = await api.holdPost("question");
  await page.goto("/");
  await page.getByRole("textbox", { name: "質問", exact: true }).fill("動物ですか？");
  await page.getByRole("button", { name: "質問", exact: true }).click();
  expect((await answer.request).postDataJSON()).toBe("動物ですか？");
  const status = page.getByRole("status");
  await expect(status).toContainText("AIが回答中");
  await expect(page.getByRole("button", { name: "質問", exact: true })).toBeDisabled();
  await expect.poll(() => status.evaluate((element) => element.getAnimations({ subtree: true }).filter((animation) => animation.playState === "running").length)).toBe(3);
  await page.screenshot({ path: testInfo.outputPath("answer-pending.png"), animations: "disabled" });
  api.setScene(questionScene([{ type: "Question", player: ids.me, question: "動物ですか？", result: "Yes" }]));
  await answer.respond("Yes");
  await expect(status).toBeHidden();
  await expect(page.getByRole("textbox", { name: "質問", exact: true })).toHaveValue("");
  await expectLatestVisible(page);
});

test("送信失敗時は入力を残し、エラーを表示して再送できる", async ({ page, api }) => {
  api.setScene(questionScene());
  const answer = await api.holdPost("question");
  await page.goto("/");
  const input = page.getByRole("textbox", { name: "質問", exact: true });
  await input.fill("動物ですか？");
  await page.getByRole("button", { name: "質問", exact: true }).click();
  await answer.respond({ detail: "回答サービスに接続できません" }, 503);
  await expect(page.getByRole("alert")).toHaveText("回答サービスに接続できません");
  await expect(input).toHaveValue("動物ですか？");
  await expect(page.getByRole("button", { name: "質問", exact: true })).toBeEnabled();
});

test("解答の送信中でも正解者表示へ切り替わる", async ({ page, api }) => {
  api.setScene(questionScene());
  const answer = await api.holdPost("answer");
  await page.goto("/");
  await page.getByRole("textbox", { name: "解答", exact: true }).fill("ねこ");
  await page.getByRole("button", { name: "解答", exact: true }).click();
  expect((await answer.request).postDataJSON()).toBe("ねこ");
  api.setScene(guessScene());
  await expect(page).toHaveURL(/\/LiarGuess$/i);
  await expect(page.getByRole("heading", { name: "ねこ", exact: true })).toBeVisible();
  await answer.respond("Correct");
});

test("ライアー通知はタップで消え、更新後も復活せず次ラウンドで表示される", async ({ page, api }) => {
  api.setScene(questionScene([], true));
  await page.goto("/");
  const notice = page.getByRole("button", { name: "あなたはこのラウンドのライアーです。通知を閉じる" });
  await expect(notice).toBeVisible();
  await notice.click();
  await expect(notice).toBeHidden();
  api.setScene(questionScene([question(1)], true));
  await expect(timelineOf(page).getByRole("listitem")).toHaveCount(1);
  await expect(notice).toBeHidden();
  api.setScene(selectingScene(1));
  await expect(page).toHaveURL(/\/TopicSelecting$/i);
  api.setScene(questionScene([], true, 2));
  await expect(notice).toBeVisible();
});

test("最終結果で得点内訳と各ラウンドの答え合わせ・履歴を読み返せる", async ({ page, api }, testInfo) => {
  api.setScene(endScene());
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "最終結果", exact: true })).toBeVisible();
  const summary = page.locator("summary").filter({ hasText: "ラウンド別の内訳" }).filter({ hasText: "あおい" });
  await expect(summary).toContainText("40pt");
  await expect(summary).toContainText("お題 +20 ／ ライアー +30 ／ 正解者なし -10");
  await page.screenshot({ path: testInfo.outputPath("final-results.png"), fullPage: true, animations: "disabled" });
  await summary.click();
  const table = page.getByRole("region", { name: "あおいの得点内訳（pt）" });
  await expect(table.getByRole("row").nth(1).getByRole("cell")).toHaveText(["+20", "+30", "0", "+50"]);
  await expect(table.getByRole("row").nth(2).getByRole("cell")).toHaveText(["0", "0", "-10", "-10"]);

  const firstSummary = page.locator("summary").filter({ hasText: "ねこ" });
  const firstRound = firstSummary.locator("..");
  await firstSummary.click();
  await expect(firstRound.getByRole("heading", { name: "💬 質問・解答の履歴" })).toBeVisible();
  await expect(firstRound.getByText("動物ですか？", { exact: true })).toBeVisible();
  await expect(firstRound.getByText("ねこは動物です。", { exact: true })).toBeVisible();
  await expect(firstRound.getByText("完全一致", { exact: true })).toBeVisible();
  await expect(firstRound.getByText("✓ 的中", { exact: true })).toHaveCount(2);
  await firstRound.screenshot({ path: testInfo.outputPath("round-expanded.png"), animations: "disabled" });
  await firstSummary.click();
  await expect(firstRound.getByText("ねこは動物です。", { exact: true })).toBeHidden();
  const secondSummary = page.locator("summary").filter({ hasText: "りんご" });
  await secondSummary.click();
  const secondRound = secondSummary.locator("..");
  await expect(secondRound.getByText("りんごとは異なる果物です。", { exact: true })).toBeVisible();
  await expect(secondRound.getByRole("region", { name: "ラウンド 2 の得点" }).getByRole("row").nth(1).getByRole("cell")).toHaveText(["0", "0", "-10", "-10"]);
});

test("同点のプレイヤーには同じ順位のメダルを表示する", async ({ page, api }) => {
  api.setScene(endScene([roundResults[1]]));
  await page.goto("/");
  const ranks = page.locator("summary").filter({ hasText: "ラウンド別の内訳" });
  await expect(ranks).toHaveCount(3);
  await expect(ranks.nth(0)).toContainText("🥇");
  await expect(ranks.nth(1)).toContainText("🥇");
  await expect(ranks.nth(2)).toContainText("🥉");
});

test("狭い画面でも入力欄と最終結果が画面幅に収まる", async ({ page, api }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  api.setScene(questionScene([question(1)], true));
  await page.goto("/");
  const input = page.getByRole("textbox", { name: "解答", exact: true });
  await expect(input).toBeInViewport();
  await expectLatestVisible(page);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  api.setScene(endScene());
  await expect(page).toHaveURL(/\/GameEnd$/i);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});
