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
  player: string;
}

export interface QuestionResponse extends IPlayerResult {
  question: string;
  result: QuestionResultType;
}

export const QuestionResultType = ["Yes", "No", "Unanswerable"] as const;

export type QuestionResultType = (typeof QuestionResultType)[number];

export interface AnswerResponse extends IPlayerResult {
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
  scene: GameScene;
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
  | LiarPlayerGuessingSceneInfo
  | RoundSummaryInfo
  | GameEndInfo;

export interface WaitRoundSceneInfo {
  waiting: number;
  nextRound: number;
}

export interface QuestionAnsweringSceneInfo {
  round: number;
  histories: IPlayerResult[];
}

export interface LiarPlayerGuessingSceneInfo {
  round: number;
  targets: LiarGuess[];
}

export interface LiarGuess {
  player: string;
  target: string;
}

export interface RoundSummaryInfo {
  round: number;
  topic: string;
  topicCorrectPlayers: string[];
  liarCorrectPlayers: string[];
}

export interface RoundResult {
  topic: string;
  correctPlayer: string;
  liarPlayers: string[];
  liarCorrectPlayers: string[];
}

export interface GameEndInfo {
  results: RoundResult[];
}

export const GameScene = [
  "WaitRoundStart",
  "QuestionAnswering",
  "LiarPlayerGuessing",
  "RoundSummary",
  "GameEnd",
] as const;

export type GameScene = (typeof GameScene)[number];

export interface Config {
  questionLimit: number;
  answerLimit: number;
  correctPoint: number;
  liarPoint: number;
}
