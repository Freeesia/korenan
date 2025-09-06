export interface Forecast {
  date: string;
  temperatureC: number;
  summary?: string;
  temperatureF: number;
}

export interface RegistRequest {
  name: string;
  topic: string;
}

export interface IPlayerResult {
  type: "Question" | "Answer";
  player: string;
}

export interface QuestionResult extends IPlayerResult {
  question: string;
  result: QuestionResultType;
}

export const QuestionResultType = ["Yes", "No", "Unanswerable"] as const;

export type QuestionResultType = (typeof QuestionResultType)[number];

export interface AnswerResult extends IPlayerResult {
  answer: string;
  result: AnswerResultType;
}

export const AnswerResultType = [
  "Correct",
  "MoreSpecific",
  "Incorrect",
] as const;

export type AnswerResultType = (typeof AnswerResultType)[number];

export interface CurrentScene {
  id: string;
  aikotoba: string;
  theme: string;
  scene: GameScene;
  round: number;
  players: Player[];
  info: ISceneInfo;
}

export interface User {
  id: string;
  name: string;
}

export interface Player extends User {
  currentScene: GameScene;
  points: number;
}

export type ISceneInfo =
  | WaitRoundSceneInfo
  | QuestionAnsweringSceneInfo
  | LiarGuessSceneInfo
  | GameEndInfo;

export interface WaitRoundSceneInfo {
  waiting: number;
}

export interface QuestionAnsweringSceneInfo {
  histories: IPlayerResult[];
}

export interface HistoryInfo {
  result: IPlayerResult;
  reason: string;
  postedAt: string;
}

export interface LiarGuessSceneInfo {
  topic: string;
  topicCorrectPlayers: string[];
  guessedPlayers: string[];
  histories: HistoryInfo[];
}

export interface LiarGuess {
  player: string;
  target: string;
}

export interface RoundResult {
  topic: string;
  topicCorrectPlayers: string[];
  liarPlayers: string[];
  liarCorrectPlayers: string[];
}

export interface GameEndInfo {
  results: RoundResult[];
}

export const GameScene = [
  "RegisterTopic",
  "WaitRoundStart",
  "TopicSelecting",
  "QuestionAnswering",
  "LiarGuess",
  "GameEnd",
] as const;

export type GameScene = (typeof GameScene)[number];

export interface Config {
  questionLimit: number;
  answerLimit: number;
  correctPoint: number;
  liarPoint: number;
  noCorrectPoint: number;
  inactivityThresholdMinutes: number;
}
