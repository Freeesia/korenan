import { useContext, useEffect } from "react";
import { SceneContext, TitleContext } from "../App";
import { GameEndInfo } from "../models";

function GameEnd() {
  const [scene] = useContext(SceneContext);
  const [, setPageTitle] = useContext(TitleContext);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("GameEnd"),
    });

    setPageTitle("最終結果");
  }, [setPageTitle]);

  const sceneInfo = () => {
    if (scene?.scene === "GameEnd") {
      return scene?.info as GameEndInfo;
    }
    return undefined;
  };

  const getPlayerName = (id: string) => {
    return scene?.players.find((p) => p.id === id)?.name || id;
  };

  const resetGame = async () => {
    await fetch("/api/reset", { method: "POST" });
  };

  return (
    <div>
      <h1>最終結果</h1>
      <h2>プレイヤー一覧と得点</h2>
      <ul>
        {scene?.players.map((player) => (
          <li key={player.id}>
            {player.name}: {player.points} points
          </li>
        ))}
      </ul>
      <h2>ラウンド結果</h2>
      <ul>
        {sceneInfo()?.results.map((result, index) => (
          <li key={index}>
            <h3>ラウンド {index + 1}</h3>
            <p>お題: {result.topic}</p>
            <p>ライアープレイヤー: {result.liarPlayers.map(getPlayerName).join(", ")}</p>
            <p>正解者: {result.topicCorrectPlayers.map(getPlayerName).join(", ")}</p>
            <p>ライアープレイヤー正解者: {result.liarCorrectPlayers.map(getPlayerName).join(", ")}</p>
          </li>
        ))}
      </ul>
      <button onClick={resetGame}>リセット</button>
    </div>
  );
}

export default GameEnd;
