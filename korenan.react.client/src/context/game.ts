import { createContext, useContext, useEffect } from "react";
import { BOT_ID, type CurrentScene, type User } from "../api/client";

export const APP_TITLE = "これなーんだ❓(ライアー)";

export interface GameContextValue {
  scene: CurrentScene | null | undefined;
  user: User | undefined;
  setUser: (user: User) => void;
  reconnect: () => void;
  title: string;
  setTitle: (title: string) => void;
}

export const GameContext = createContext<GameContextValue | undefined>(undefined);

export function useGame(): GameContextValue {
  const value = useContext(GameContext);
  if (!value) {
    throw new Error("GameProvider が見つかりません");
  }
  return value;
}

export function usePageTitle(title: string) {
  const { setTitle } = useGame();
  useEffect(() => {
    setTitle(title);
  }, [setTitle, title]);
}

export function useCurrentPlayer() {
  const { scene, user } = useGame();
  return scene?.players.find((p) => p.id === user?.id);
}

export function useIsHost() {
  const { scene, user } = useGame();
  return scene !== null && scene !== undefined && scene.hostId === user?.id;
}

export function usePlayerName() {
  const { scene } = useGame();
  return (id: string) => scene?.players.find((p) => p.id === id)?.name ?? (id === BOT_ID ? "bot" : "退出したプレイヤー");
}
