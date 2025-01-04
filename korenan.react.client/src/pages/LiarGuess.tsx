import { useContext, useEffect, useState } from "react";
import { SceneContext, UserContext } from "../App";
import { LiarGuessSceneInfo } from "../models";

function LiarGuess() {
  const scene = useContext(SceneContext);
  const [user] = useContext(UserContext);
  const [guess, setGuess] = useState("");
  const [guessed, setGuessed] = useState(false);

  const sceneInfo = () => {
    if (scene?.scene === "LiarGuess") {
      return scene?.info as LiarGuessSceneInfo;
    }
    return undefined;
  };

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("LiarPlayerGuessing"),
    });
  }, []);

  const guessLiar = async () => {
    await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(guess),
    });
    setGuessed(true);
  };

  const getPlayerName = (id: string) =>
    scene?.players.find((p) => p.id === id)?.name || id;

  useEffect(() => {
    if (sceneInfo()?.targets.findIndex((t) => t.player === user?.id) !== -1) {
      setGuessed(true);
    }
  }, [scene]);

  return (
    <div>
      <h1>ライアー推測タイム</h1>
      <div>
        <h2>正解のお題: {sceneInfo()?.topic}</h2>
        <h2>正解者:</h2>
        <ul>
          {sceneInfo()?.topicCorrectPlayers.map((player, index) => (
            <li key={index}>{getPlayerName(player)}</li>
          ))}
        </ul>
      </div>
      <div>
        <h2>推測結果:</h2>
        <ul>
          {sceneInfo()?.targets.map((target, index) => (
            <li key={index}>
              {getPlayerName(target.player)} ➡️ {getPlayerName(target.target)}
            </li>
          ))}
        </ul>
      </div>
      <div>
        <select
          value={guess}
          onChange={(e) => setGuess(e.target.value)}
          disabled={guessed}
        >
          <option value="">プレイヤーを選択</option>
          {scene?.players.map((player) => (
            <option key={player.id} value={player.id}>
              {player.name}
            </option>
          ))}
        </select>
        <button onClick={guessLiar} disabled={guessed}>
          ライアー！
        </button>
      </div>
    </div>
  );
}

export default LiarGuess;
