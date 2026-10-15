import type { ReactNode } from "react";
import { PlayerAvatar } from "./PlayerAvatar";
import styles from "./ChatBubble.module.css";

interface ChatBubbleProps {
  playerId: string;
  playerName: string;
  own: boolean;
  kind: "question" | "answer";
  badge: ReactNode;
  children: ReactNode;
}

export function ChatBubble({ playerId, playerName, own, kind, badge, children }: ChatBubbleProps) {
  return (
    <li className={`${styles.wrapper} ${own ? styles.own : ""}`}>
      {!own && (
        <div className={styles.name}>
          <PlayerAvatar id={playerId} name={playerName} size="sm" />
          {playerName}
        </div>
      )}
      <div className={styles.message}>
        <div className={styles.bubble}>
          {kind === "answer" && <span className={styles.kind}>解答</span>}
          {children}
        </div>
        {badge}
      </div>
    </li>
  );
}
