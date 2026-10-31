import { useActionState } from "react";
import { api, sceneInfoOf, type RoundResult } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { HistoryList } from "../components/HistoryList";
import { PlayerAvatar } from "../components/PlayerAvatar";
import { useGame, usePageTitle, usePlayerName } from "../context/game";
import page from "../styles/page.module.css";
import styles from "./GameEnd.module.css";

const MEDALS = ["🥇", "🥈", "🥉"];
const points = (value: number) => value > 0 ? `+${value}` : String(value);
type Score = RoundResult["scores"][number];
type ScoreRow = Pick<Score, "topicPoints" | "liarPoints" | "noCorrectPoints"> & { label: string };

function ScoreTable({ rows, caption }: { rows: ScoreRow[]; caption: string }) {
  return (
    <div className={styles.tableScroll} tabIndex={0} role="region" aria-label={caption}>
      <table className={styles.scoreTable}>
        <caption>{caption}</caption>
        <thead><tr><th scope="col">内訳</th><th scope="col">お題正解</th><th scope="col">ライアー的中</th><th scope="col">正解者なし</th><th scope="col">計</th></tr></thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              <th scope="row">{row.label}</th>
              <td>{points(row.topicPoints)}</td>
              <td>{points(row.liarPoints)}</td>
              <td className={row.noCorrectPoints < 0 ? styles.negative : undefined}>{points(row.noCorrectPoints)}</td>
              <td className={styles.total}>{points(row.topicPoints + row.liarPoints + row.noCorrectPoints)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ResultPlayers({ ids, empty = "なし" }: { ids: string[]; empty?: string }) {
  const playerName = usePlayerName();
  return ids.length === 0 ? <span className={styles.empty}>{empty}</span> : (
    <ul className={styles.players}>
      {ids.map((id) => <li key={id}><PlayerAvatar id={id} name={playerName(id)} size="sm" />{playerName(id)}</li>)}
    </ul>
  );
}

function RoundRecap({ result, index }: { result: RoundResult; index: number }) {
  const playerName = usePlayerName();
  return (
    <details className={styles.round}>
      <summary className={styles.roundSummary}>
        <span className={styles.roundNumber}>ROUND<strong>{String(index + 1).padStart(2, "0")}</strong></span>
        <span className={styles.roundTitle}>
          <span className={styles.caption}>正解のお題</span>
          <strong>{result.topic}</strong>
          <span className={styles.roundOutcome}>{result.topicCorrectPlayers.length > 0 ? `🎉 ${result.topicCorrectPlayers.map(playerName).join("・")} が正解！` : "🔍 正解者なし"}</span>
          <span className={styles.caption}>タップして答え合わせ・履歴を見る</span>
        </span>
        <span className={styles.chevron} aria-hidden>⌄</span>
      </summary>
      <div className={styles.roundBody}>
        <dl className={styles.result}>
          <div><dt>🤫 ライアー</dt><dd><ResultPlayers ids={result.liarPlayers} /></dd></div>
          <div><dt>🎯 お題正解者</dt><dd><ResultPlayers ids={result.topicCorrectPlayers} empty="正解者なし" /></dd></div>
          <div><dt>🕵️ ライアー正解者</dt><dd><ResultPlayers ids={result.liarCorrectPlayers} empty="的中者なし" /></dd></div>
        </dl>
        <ScoreTable caption={`ラウンド ${index + 1} の得点`} rows={result.scores.map((score) => ({ ...score, label: playerName(score.player) }))} />
        <section className={styles.section}>
          <h3>🗳️ みんなのライアー推理</h3>
          <ul className={styles.guesses}>
            {result.liarGuesses.map((guess) => (
              <li key={guess.player}>
                <span>{playerName(guess.player)} <span className={styles.empty}>→</span> {playerName(guess.target)}</span>
                <span className={result.liarPlayers.includes(guess.target) ? styles.hit : styles.empty}>{result.liarPlayers.includes(guess.target) ? "✓ 的中" : "はずれ"}</span>
              </li>
            ))}
          </ul>
        </section>
        <section className={styles.section}>
          <h3>💬 質問・解答の履歴</h3>
          <HistoryList histories={result.histories} />
        </section>
      </div>
    </details>
  );
}

function GameEnd() {
  const { scene } = useGame();
  usePageTitle("最終結果");

  const results = sceneInfoOf(scene, "GameEnd")?.results ?? [];
  const ranking = [...(scene?.players ?? [])].sort((a, b) => b.points - a.points);

  const [, resetAction, isPending] = useActionState(async () => {
    await api.POST("/api/reset");
  }, undefined);

  return (
    <div className={page.page}>
      <div className={styles.hero}><span aria-hidden>🏆</span><h1>最終結果</h1><p>{results.length}ラウンドの推理、おつかれさまでした！</p></div>
      <Card title="ランキング・得点内訳">
        <ol className={styles.ranking}>
          {ranking.map((player) => {
            const rank = ranking.findIndex((p) => p.points === player.points);
            const scores = results.map((result, round) => ({ score: result.scores.find((s) => s.player === player.id), round }));
            const totals = scores.reduce((sum, { score }) => ({
              topic: sum.topic + (score?.topicPoints ?? 0),
              liar: sum.liar + (score?.liarPoints ?? 0),
              noCorrect: sum.noCorrect + (score?.noCorrectPoints ?? 0),
            }), { topic: 0, liar: 0, noCorrect: 0 });
            return (
              <li key={player.id}>
                <details className={`${styles.rank} ${rank === 0 ? styles.winner : ""}`}>
                  <summary className={styles.rankSummary}>
                    <span className={styles.medal}>{MEDALS[rank] ?? `${rank + 1}.`}</span>
                    <PlayerAvatar id={player.id} name={player.name} />
                    <span className={styles.name}>{player.name}</span>
                    <span className={styles.points}>{player.points}<small>pt</small></span>
                    <span className={styles.pointSummary}>お題 {points(totals.topic)} ／ ライアー {points(totals.liar)} ／ 正解者なし {points(totals.noCorrect)}</span>
                    <span className={styles.breakdownHint}>ラウンド別の内訳 <span className={styles.chevron} aria-hidden>⌄</span></span>
                  </summary>
                  <ScoreTable caption={`${player.name}の得点内訳（pt）`} rows={scores.map(({ score, round }) => ({
                    label: `R${round + 1}`, topicPoints: score?.topicPoints ?? 0, liarPoints: score?.liarPoints ?? 0, noCorrectPoints: score?.noCorrectPoints ?? 0,
                  }))} />
                </details>
              </li>
            );
          })}
        </ol>
        <p className={styles.caption}>「正解者なし」は、誰もお題を当てられなかったラウンドのライアーへの得点です。</p>
      </Card>
      <h2 className={styles.recapHeading}>✨ ラウンドを振り返る</h2>
      {results.map((result, index) => <RoundRecap key={index} result={result} index={index} />)}
      <form action={resetAction}>
        <Button type="submit" variant="primary" block disabled={isPending}>
          ゲームを終了してトップへ
        </Button>
      </form>
    </div>
  );
}

export default GameEnd;
