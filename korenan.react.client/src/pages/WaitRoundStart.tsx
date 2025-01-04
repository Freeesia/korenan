import { useContext, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { SceneContext, UserContext } from "../App";

function WaitRoundStart() {
  const scene = useContext(SceneContext);
  const [user] = useContext(UserContext);
  const navigate = useNavigate();

  useEffect(() => {
    if (scene && !scene.players.some((player) => player.id === user?.id)) {
      navigate("/regist");
    }
  }, [scene, user, navigate]);

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
