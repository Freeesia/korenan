import { sceneInfoOf, type QuestionResult } from "../api/client";
import { useGame } from "../context/game";
import { Card } from "./Card";
import { HistoryList } from "./HistoryList";
import styles from "./GameInfoPanel.module.css";

function QuestionSummary({ questions }: { questions: QuestionResult[] }) {
  const yes = questions.filter((q) => q.result === "Yes");
  const no = questions.filter((q) => q.result === "No");
  return (
    <Card title="ヒント">
      <div className={styles.summary}>
        <h3 className={styles.subtitle}>⭕ はい</h3>
        {yes.length > 0 ? (
          <ul className={styles.questions}>
            {yes.map((q, i) => (
              <li key={i}>{q.question}</li>
            ))}
          </ul>
        ) : (
          <p className={styles.empty}>まだありません</p>
        )}
        <h3 className={styles.subtitle}>❌ いいえ</h3>
        {no.length > 0 ? (
          <ul className={styles.questions}>
            {no.map((q, i) => (
              <li key={i}>{q.question}</li>
            ))}
          </ul>
        ) : (
          <p className={styles.empty}>まだありません</p>
        )}
      </div>
    </Card>
  );
}

export function GameInfoPanel() {
  const { scene } = useGame();
  if (!scene) {
    return null;
  }
  const qa = sceneInfoOf(scene, "QuestionAnswering");
  const liarGuess = sceneInfoOf(scene, "LiarGuess");
  const questions = qa?.histories.filter((h): h is QuestionResult => h.type === "Question");

  return (
    <div className={styles.panel}>
      <Card title="ゲーム情報">
        <dl className={styles.info}>
          <dt>テーマ</dt>
          <dd>{scene.theme}</dd>
          <dt>ラウンド</dt>
          <dd>{scene.round > 0 ? scene.round : "-"}</dd>
          <dt>参加人数</dt>
          <dd>{scene.players.length}人</dd>
        </dl>
      </Card>
      {questions && <QuestionSummary questions={questions} />}
      {liarGuess && (
        <Card title="判断理由">
          <HistoryList histories={liarGuess.histories} />
        </Card>
      )}
    </div>
  );
}
