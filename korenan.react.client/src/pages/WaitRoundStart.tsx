import { useContext, useEffect } from "react";
import { SceneContext } from "../App";

function WaitRoundStart() {
  const scene = useContext(SceneContext);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("WaitRoundStart"),
    });
  }, []);

  const startRound = async () => {
    await fetch("/api/start", { method: "POST" });
  };

  return (
    <div>
      <h1>プレイヤー待機中</h1>
      <div>
        <h2>参加プレイヤー:</h2>
        <ul>
          {scene?.players.map((player) => (
            <li key={player.id}>
              {player.name} - {player.currentScene}
            </li>
          ))}
        </ul>
      </div>
      <button onClick={startRound}>ラウンド開始</button>
    </div>
  );
}

export default WaitRoundStart;
