import { useRef, useState, type FormEvent } from "react";
import { api, errorMessage, sceneInfoOf } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { HistoryList } from "../components/HistoryList";
import { PlayerAvatar } from "../components/PlayerAvatar";
import { PlayerList } from "../components/PlayerList";
import { useGame, usePageTitle, usePlayerName } from "../context/game";
import form from "../styles/form.module.css";
import page from "../styles/page.module.css";
import styles from "./LiarGuess.module.css";

function LiarGuess() {
  const { scene, user } = useGame();
  const playerName = usePlayerName();
  usePageTitle("ライアー推理タイム");

  const info = sceneInfoOf(scene, "LiarGuess");
  const guessed = !!user && !!info?.guessedPlayers.includes(user.id);

  const [error, setError] = useState<string>();
  const [isPending, setIsPending] = useState(false);
  const submitting = useRef(false);

  // 最後の推理で次のお題の準備が始まっても、SSEの画面遷移を送信待ちにしない。
  const guess = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (submitting.current || guessed) return;
    const target = String(new FormData(event.currentTarget).get("target") ?? "");
    submitting.current = true;
    setIsPending(true);
    setError(undefined);
    try {
      const { error } = await api.POST("/api/guess", { body: target });
      if (error) setError(errorMessage(error, "推理の送信に失敗しました"));
    } catch {
      setError("通信に失敗しました。接続を確認してください。");
    } finally {
      submitting.current = false;
      setIsPending(false);
    }
  };

  return (
    <div className={page.page}>
      <Card variant="translucent">
        <span className={styles.caption}>正解のお題</span>
        <h2 className={page.heading}>{info?.topic}</h2>
        <div className={styles.correct}>
          <span className={styles.caption}>正解者</span>
          {info && info.topicCorrectPlayers.length > 0 ? (
            <ul className={styles.players}>
              {info.topicCorrectPlayers.map((id) => (
                <li key={id}>
                  <PlayerAvatar id={id} name={playerName(id)} size="sm" />
                  {playerName(id)} 🎉
                </li>
              ))}
            </ul>
          ) : (
            <p>正解者なし…😫</p>
          )}
        </div>
      </Card>
      <Card title="お題を考えた「ライアー」は誰かな？">
        {guessed ? (
          <p>推理済みです✅ 他のプレイヤーを待っています…</p>
        ) : (
          <form onSubmit={guess} className={form.form}>
            <div className={styles.choices}>
              {scene?.players
                .filter((player) => player.id !== user?.id)
                .map((player) => (
                  <label key={player.id} className={styles.choice}>
                    <input type="radio" name="target" value={player.id} required />
                    <PlayerAvatar id={player.id} name={player.name} size="sm" />
                    {player.name}
                  </label>
                ))}
            </div>
            {error && <p className={form.error}>{error}</p>}
            <Button type="submit" variant="primary" block disabled={isPending}>
              ライアー！
            </Button>
          </form>
        )}
      </Card>
      <div className={page.mobileOnly}>
        <Card title="推理状況">
          <PlayerList />
        </Card>
        <Card title="このラウンドの履歴">
          <HistoryList histories={info?.histories ?? []} />
        </Card>
      </div>
    </div>
  );
}

export default LiarGuess;
