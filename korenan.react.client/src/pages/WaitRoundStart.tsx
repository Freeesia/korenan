import { useRef, useState, type FormEvent } from "react";
import { api, errorMessage, sceneInfoOf } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { Dialog } from "../components/Dialog";
import { InviteCard } from "../components/InviteCard";
import { PlayerList } from "../components/PlayerList";
import { useGame, useIsHost, usePageTitle, usePlayerName } from "../context/game";
import form from "../styles/form.module.css";
import page from "../styles/page.module.css";
import ConfigForm from "./Config";
import styles from "./WaitRoundStart.module.css";

function WaitRoundStart() {
  const { scene } = useGame();
  const isHost = useIsHost();
  const playerName = usePlayerName();
  const [configOpen, setConfigOpen] = useState(false);
  usePageTitle("マッチング中");

  const waiting = sceneInfoOf(scene, "WaitRound")?.waiting ?? 0;
  const total = scene?.players.length ?? 0;

  const [error, setError] = useState<string>();
  const [isPending, setIsPending] = useState(false);
  const submitting = useRef(false);

  // 長い送信をReact Actionにすると、SSEで始まる画面遷移もその完了を待ってしまう。
  const startRound = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (submitting.current) return;
    submitting.current = true;
    setIsPending(true);
    setError(undefined);
    try {
      const { error } = await api.POST("/api/start");
      if (error) setError(errorMessage(error, "ラウンドを開始できませんでした"));
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
        <span className={styles.caption}>テーマ</span>
        <h2 className={page.heading}>「{scene?.theme}」</h2>
      </Card>
      <InviteCard />
      <div className={page.mobileOnly}>
        <Card title={`参加プレイヤー (${waiting}/${total} 準備完了)`}>
          <PlayerList />
        </Card>
      </div>
      {isHost ? (
        <Card>
          <p>全てのプレイヤーがそろったら、「ラウンド開始」ボタンを押してゲームを始めましょう！</p>
          <form onSubmit={startRound}>
            <Button type="submit" variant="primary" size="lg" block disabled={isPending || waiting < total}>
              {isPending ? "準備中…" : "ラウンド開始❗"}
            </Button>
          </form>
          {error && <p className={form.error}>{error}</p>}
          <Button variant="ghost" onClick={() => setConfigOpen(true)}>
            ⚙️ ルール設定
          </Button>
          <Dialog open={configOpen} title="ルール設定" onClose={() => setConfigOpen(false)}>
            <ConfigForm onClose={() => setConfigOpen(false)} />
          </Dialog>
        </Card>
      ) : (
        <Card>
          <p>
            ホストは「{scene ? playerName(scene.hostId) : ""}」さんです。
            <br />
            ホストがラウンド開始するまでしばらくお待ちください。
          </p>
          <p className={form.hint}>
            準備完了: {waiting}/{total}
          </p>
        </Card>
      )}
    </div>
  );
}

export default WaitRoundStart;
