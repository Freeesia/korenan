import { useContext, useEffect } from "react";
import { SceneContext, TitleContext } from "../App";
import { RoundSummaryInfo } from "../models";

function RoundSummary() {
  const [scene] = useContext(SceneContext);
  const [, setPageTitle] = useContext(TitleContext);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("RoundSummary"),
    });

    setPageTitle("ラウンド結果");
  }, [setPageTitle]);

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
      <button onClick={nextScene}>OK</button>
      <ul>
        {scene?.players.map((player) => (
          <li key={player.id}>
            {player.name}: {player.currentScene == "WaitRoundStart" ? "OK" : "結果表示中"}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default RoundSummary;
