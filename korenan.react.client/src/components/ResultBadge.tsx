import type { AnswerResultType, QuestionResultType } from "../api/client";
import styles from "./ResultBadge.module.css";

const RESULTS: Record<QuestionResultType | AnswerResultType, { emoji: string; label: string }> = {
  Yes: { emoji: "⭕", label: "はい" },
  No: { emoji: "❌", label: "いいえ" },
  Unanswerable: { emoji: "🤔", label: "答えられない" },
  Correct: { emoji: "🎉", label: "正解" },
  MoreSpecific: { emoji: "⚠️", label: "もっと具体的に" },
  Incorrect: { emoji: "❌", label: "不正解" },
};

interface ResultBadgeProps {
  result: QuestionResultType | AnswerResultType;
  variant?: "floating" | "inline";
}

export function ResultBadge({ result, variant = "inline" }: ResultBadgeProps) {
  const { emoji, label } = RESULTS[result];
  return (
    <span className={`${styles.badge} ${styles[variant]}`} title={label} aria-label={label}>
      {emoji}
    </span>
  );
}
