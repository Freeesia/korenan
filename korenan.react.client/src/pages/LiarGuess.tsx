import { useCallback, useContext, useEffect, useState } from "react";
import { SceneContext, UserContext, TitleContext } from "../App";
import { LiarGuessSceneInfo } from "../models";

function LiarGuess() {
  const [scene] = useContext(SceneContext);
  const [user] = useContext(UserContext);
  const [, setPageTitle] = useContext(TitleContext);
  const [guess, setGuess] = useState("");
  const [guessed, setGuessed] = useState(false);

  const sceneInfo = useCallback(() => {
    if (scene?.scene === "LiarGuess") {
      return scene?.info as LiarGuessSceneInfo;
    }
    return undefined;
  }, [scene]);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("LiarGuess"),
    });

    setPageTitle("ライアー推測タイム");
  }, [setPageTitle]);

  const guessLiar = async () => {
    const res = await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(guess),
    });
    if (res.ok) {
      setGuessed(true);
    }
  };

  const banPlayer = async (playerId: string) => {
    await fetch("/api/ban", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(playerId),
    });
  };

  const getPlayerName = (id: string) => scene?.players.find((p) => p.id === id)?.name || id;

  useEffect(() => {
    if (sceneInfo()?.guessedPlayers.includes(user?.id || "")) {
      setGuessed(true);
    }
  }, [scene, sceneInfo, user?.id]);

  return (
    <div>
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
          {sceneInfo()?.guessedPlayers.map((playerId, index) => (
            <li key={index}>{getPlayerName(playerId)}: ✅</li>
          ))}
        </ul>
      </div>
      <div>
        <h2>未回答プレイヤー:</h2>
        <ul>
          {scene?.players
            .filter((player) => !sceneInfo()?.guessedPlayers.includes(player.id))
            .map((player) => (
              <li key={player.id}>
                {player.name} {scene?.players[0].id === user?.id && player.id !== user?.id && <button onClick={() => banPlayer(player.id)}>BAN</button>}
              </li>
            ))}
        </ul>
      </div>
      <div>
        <p>お題を考えた「ライアープレイヤー」は誰かな？</p>
        <select value={guess} onChange={(e) => setGuess(e.target.value)} disabled={guessed}>
          <option value="">プレイヤーを選択</option>
          {scene?.players.map((player) => (
            <option key={player.id} value={player.id}>
              {player.name}
            </option>
          ))}
        </select>
        <button onClick={guessLiar} disabled={guessed || !guess}>
          ライアー！
        </button>
      </div>
    </div>
  );
}

export default LiarGuess;
