import { useActionState, useState } from "react";
import { api, errorMessage, sceneInfoOf } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { Dialog } from "../components/Dialog";
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

  const [error, startAction, isPending] = useActionState(async () => {
    const { error } = await api.POST("/api/start");
    return error ? errorMessage(error, "ラウンドを開始できませんでした") : undefined;
  }, undefined);

  const shareAikotoba = async () => {
    const url = `${window.location.origin}/JoinRoom?aikotoba=${encodeURIComponent(scene?.aikotoba ?? "")}`;
    if (navigator.share) {
      await navigator.share({
        title: "これなんに招待",
        text: `一緒に遊ぼう！合言葉: ${scene?.aikotoba}\nこちらのURLから参加してね:`,
        url,
      });
      return;
    }
    await navigator.clipboard.writeText(url);
    alert("招待URLをクリップボードにコピーしました。");
  };

  return (
    <div className={page.page}>
      <Card variant="translucent">
        <span className={styles.caption}>テーマ</span>
        <h2 className={page.heading}>「{scene?.theme}」</h2>
      </Card>
      <Card title="あいことば">
        <div className={styles.aikotoba}>
          <span className={styles.code}>{scene?.aikotoba}</span>
          <Button onClick={shareAikotoba}>共有</Button>
        </div>
        <p className={form.hint}>あいことばを共有して、みんなで同じゲームに参加しよう！</p>
      </Card>
      <div className={page.mobileOnly}>
        <Card title={`参加プレイヤー (${waiting}/${total} 準備完了)`}>
          <PlayerList />
        </Card>
      </div>
      {isHost ? (
        <Card>
          <p>全てのプレイヤーがそろったら、「ラウンド開始」ボタンを押してゲームを始めましょう！</p>
          <form action={startAction}>
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
