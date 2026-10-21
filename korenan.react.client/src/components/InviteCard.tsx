import { useGame } from "../context/game";
import form from "../styles/form.module.css";
import { Button } from "./Button";
import { Card } from "./Card";
import styles from "./InviteCard.module.css";

export function InviteCard() {
  const { scene } = useGame();

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
    <Card title="あいことば">
      <div className={styles.aikotoba}>
        <span className={styles.code}>{scene?.aikotoba}</span>
        <Button onClick={shareAikotoba}>共有</Button>
      </div>
      <p className={form.hint}>あいことばを共有して、みんなで同じゲームに参加しよう！</p>
    </Card>
  );
}
