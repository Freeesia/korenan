import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export const api = createClient<paths>({ baseUrl: "/" });

type Schemas = components["schemas"];

export type User = Schemas["User"];
export type Player = Schemas["Player"];
export type Config = Schemas["Config"];
export type GameScene = Schemas["GameScene"];
export type CurrentScene = Schemas["CurrentScene"];
export type SceneInfo = Schemas["ISceneInfo"];
export type PlayerResult = Schemas["IPlayerResult"];
export type QuestionResult = Schemas["IPlayerResultQuestionResult"];
export type AnswerResult = Schemas["IPlayerResultAnswerResult"];
export type QuestionResultType = Schemas["QuestionResultType"];
export type AnswerResultType = Schemas["AnswerResultType"];
export type HistoryInfo = Schemas["HistoryInfo"];
export type RoundResult = Schemas["RoundResult"];

export const BOT_ID = "00000000-0000-0000-0000-000000000000";

export function errorMessage(error: unknown, fallback: string): string {
  if (typeof error === "object" && error !== null && "detail" in error && typeof error.detail === "string") {
    return error.detail;
  }
  return fallback;
}

export function sceneInfoOf<T extends SceneInfo["type"]>(scene: CurrentScene | null | undefined, type: T): Extract<SceneInfo, { type: T }> | undefined {
  const info = scene?.info;
  return info?.type === type ? (info as Extract<SceneInfo, { type: T }>) : undefined;
}
