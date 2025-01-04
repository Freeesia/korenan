import { useContext, useEffect } from "react";
import { SceneContext } from "../App";
import { RoundSummaryInfo } from "../models";

function RoundSummary() {
  const scene = useContext(SceneContext);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("RoundSummary"),
    });
  }, []);

  const sceneInfo = () => {
    if (scene?.scene === "RoundSummary") {
      return scene?.info as RoundSummaryInfo;
    }
    return undefined;
  };

  const getPlayerName = (id: string) => {
    return scene?.players.find((p) => p.id === id)?.name || id;
  };

  const nextScene = async () => {
    await fetch("/api/next", { method: "POST" });
  };

  return (
    <div>
      <h1>Round {scene?.round}</h1>
      <h2>お題</h2>
      <p>{sceneInfo()?.topic}</p>
      <h2>正解者</h2>
      <ul>
        {sceneInfo()?.topicCorrectPlayers.map((player) => (
          <li key={player}>{getPlayerName(player)}</li>
        ))}
      </ul>
      <h2>ライアー正解者</h2>
      <ul>
        {sceneInfo()?.liarCorrectPlayers.map((player) => (
          <li key={player}>{getPlayerName(player)}</li>
        ))}
      </ul>
      <button onClick={nextScene}>次のシーンへ</button>
    </div>
  );
}

export default RoundSummary;
