import { useActionState } from "react";
import { api, sceneInfoOf } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { PlayerAvatar } from "../components/PlayerAvatar";
import { useGame, usePageTitle, usePlayerName } from "../context/game";
import page from "../styles/page.module.css";
import styles from "./GameEnd.module.css";

const MEDALS = ["🥇", "🥈", "🥉"];

function GameEnd() {
  const { scene } = useGame();
  const playerName = usePlayerName();
  usePageTitle("最終結果");

  const results = sceneInfoOf(scene, "GameEnd")?.results ?? [];
  const ranking = [...(scene?.players ?? [])].sort((a, b) => b.points - a.points);
  const names = (ids: string[]) => (ids.length > 0 ? ids.map(playerName).join(", ") : "なし");

  const [, resetAction, isPending] = useActionState(async () => {
    await api.POST("/api/reset");
  }, undefined);

  return (
    <div className={page.page}>
      <h1 className={page.heading}>🏆 最終結果</h1>
      <Card title="ランキング">
        <ol className={styles.ranking}>
          {ranking.map((player, index) => (
            <li key={player.id} className={styles.rank}>
              <span className={styles.medal}>{MEDALS[index] ?? `${index + 1}.`}</span>
              <PlayerAvatar id={player.id} name={player.name} />
              <span className={styles.name}>{player.name}</span>
              <span className={styles.points}>{player.points}pt</span>
            </li>
          ))}
        </ol>
      </Card>
      {results.map((result, index) => (
        <Card key={index} title={`ラウンド ${index + 1}: ${result.topic}`}>
          <dl className={styles.result}>
            <dt>ライアー</dt>
            <dd>{names(result.liarPlayers)}</dd>
            <dt>お題正解者</dt>
            <dd>{names(result.topicCorrectPlayers)}</dd>
            <dt>ライアー正解者</dt>
            <dd>{names(result.liarCorrectPlayers)}</dd>
          </dl>
        </Card>
      ))}
      <form action={resetAction}>
        <Button type="submit" variant="primary" block disabled={isPending}>
          ゲームを終了してトップへ
        </Button>
      </form>
    </div>
  );
}

export default GameEnd;
