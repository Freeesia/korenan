import { api, sceneInfoOf, type Player } from "../api/client";
import { useGame, useIsHost } from "../context/game";
import { Button } from "./Button";
import { PlayerAvatar } from "./PlayerAvatar";
import styles from "./PlayerList.module.css";

export function PlayerList() {
  const { scene, user } = useGame();
  const isHost = useIsHost();
  const guessed = sceneInfoOf(scene, "LiarGuess")?.guessedPlayers;

  const status = (player: Player) => {
    if (scene?.scene === "WaitRoundStart") {
      return player.currentScene === "WaitRoundStart" ? "準備完了👍" : "お題考え中…🤔";
    }
    if (guessed) {
      return guessed.includes(player.id) ? "推理済み✅" : "推理中…🕵️";
    }
    return undefined;
  };

  const ban = async (id: string) => {
    await api.POST("/api/ban", { body: id });
  };

  return (
    <ul className={styles.list}>
      {scene?.players.map((player) => (
        <li key={player.id} className={`${styles.item} ${player.id === user?.id ? styles.self : ""}`}>
          <PlayerAvatar id={player.id} name={player.name} />
          <div className={styles.info}>
            <span className={styles.name}>
              {player.id === scene.hostId && <span title="ホスト">👑</span>}
              {player.name}
              {player.id === user?.id && <span className={styles.you}>(あなた)</span>}
            </span>
            {status(player) && <span className={styles.status}>{status(player)}</span>}
          </div>
          <span className={styles.points}>{player.points}pt</span>
          {isHost && player.id !== user?.id && (
            <Button size="sm" variant="danger" onClick={() => ban(player.id)}>
              退出
            </Button>
          )}
        </li>
      ))}
    </ul>
  );
}
