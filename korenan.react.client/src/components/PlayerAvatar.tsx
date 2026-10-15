import { BOT_ID } from "../api/client";
import styles from "./PlayerAvatar.module.css";

const COLORS = ["#646cff", "#e5484d", "#30a46c", "#f5a524", "#8e4ec6", "#0091ff", "#d6409f", "#12a594"];

function colorOf(id: string) {
  let hash = 0;
  for (const c of id) {
    hash = (hash * 31 + c.charCodeAt(0)) | 0;
  }
  return COLORS[Math.abs(hash) % COLORS.length];
}

interface PlayerAvatarProps {
  id: string;
  name: string;
  size?: "sm" | "md";
}

export function PlayerAvatar({ id, name, size = "md" }: PlayerAvatarProps) {
  if (id === BOT_ID) {
    return (
      <span className={`${styles.avatar} ${styles[size]} ${styles.bot}`} aria-label="bot">
        🤖
      </span>
    );
  }
  return (
    <span className={`${styles.avatar} ${styles[size]}`} style={{ backgroundColor: colorOf(id) }} aria-hidden>
      {Array.from(name)[0] ?? "?"}
    </span>
  );
}
