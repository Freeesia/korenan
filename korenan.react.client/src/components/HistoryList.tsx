import type { HistoryInfo } from "../api/client";
import { usePlayerName } from "../context/game";
import { ResultBadge } from "./ResultBadge";
import styles from "./HistoryList.module.css";

export function HistoryList({ histories }: { histories: HistoryInfo[] }) {
  const playerName = usePlayerName();
  if (histories.length === 0) {
    return <p className={styles.empty}>履歴はありません</p>;
  }
  return (
    <ol className={styles.list}>
      {histories.map((history, index) => (
        <li key={index} className={styles.item}>
          <div className={styles.row}>
            <span aria-hidden>{history.result.type === "Answer" ? "🚀" : "❓"}</span>
            <span className={styles.text}>{history.result.type === "Answer" ? history.result.answer : history.result.question}</span>
            <ResultBadge result={history.result.result} />
          </div>
          <div className={styles.meta}>{playerName(history.result.player)}</div>
          <p className={styles.reason}>{history.reason}</p>
        </li>
      ))}
    </ol>
  );
}
