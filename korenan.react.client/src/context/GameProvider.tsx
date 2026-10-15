import { useEffect, useState, type ReactNode } from "react";
import { api, type User } from "../api/client";
import { useSceneStream } from "../hooks/useSceneStream";
import { APP_TITLE, GameContext } from "./game";

export function GameProvider({ children }: { children: ReactNode }) {
  const [scene, reconnect] = useSceneStream();
  const [user, setUser] = useState<User>();
  const [title, setTitle] = useState(APP_TITLE);

  useEffect(() => {
    const fetchUser = async () => {
      const { data } = await api.GET("/api/me");
      if (data) {
        setUser(data);
      }
    };
    void fetchUser();
  }, []);

  return <GameContext value={{ scene, user, setUser, reconnect, title, setTitle }}>{children}</GameContext>;
}
