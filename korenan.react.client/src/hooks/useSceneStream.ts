import { useEffect, useState } from "react";
import type { CurrentScene } from "../api/client";

/**
 * SSEでシーン情報を購読する。
 * 未取得の場合はundefined、参加中のルームがない場合はnullを返す。
 */
export function useSceneStream(): [CurrentScene | null | undefined, () => void] {
  const [scene, setScene] = useState<CurrentScene | null>();
  const [connection, setConnection] = useState(0);

  useEffect(() => {
    const source = new EventSource("/api/scene/stream");
    source.addEventListener("scene", (e) => setScene(JSON.parse(e.data) as CurrentScene));
    source.addEventListener("none", () => {
      setScene(null);
      source.close();
    });
    return () => source.close();
  }, [connection]);

  const reconnect = () => setConnection((c) => c + 1);
  return [scene, reconnect];
}
