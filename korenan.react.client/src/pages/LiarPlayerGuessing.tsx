import { useContext, useState } from "react";
import { SceneContext } from "../App";
import { LiarPlayerGuessingSceneInfo } from "../models";

function LiarPlayerGuessing() {
  const scene = useContext(SceneContext);
  const [guess, setGuess] = useState("");

  const sceneInfo = () => {
    if (scene?.scene === "LiarPlayerGuessing") {
      return scene?.info as LiarPlayerGuessingSceneInfo;
    }
    return undefined;
  };

  const guessLiar = async () => {
    await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(guess),
    });
  };

  const getPlayerName = (id: string) => {
    return scene?.players.find((p) => p.id === id)?.name || id;
  };

  return (
    <div>
      <h1>ライアー推測タイム</h1>
      <div>
        <h2>推測結果:</h2>
        <ul>
          {sceneInfo()?.targets.map((target, index) => (
            <li key={index}>
              {getPlayerName(target.player)}: {getPlayerName(target.target)}
            </li>
          ))}
        </ul>
      </div>
      <div>
        <select value={guess} onChange={(e) => setGuess(e.target.value)}>
          <option value="">プレイヤーを選択</option>
          {scene?.players.map((player) => (
            <option key={player.id} value={player.id}>
              {player.name}
            </option>
          ))}
        </select>
        <button onClick={guessLiar}>ライアー！</button>
      </div>
    </div>
  );
}

export default LiarPlayerGuessing;
