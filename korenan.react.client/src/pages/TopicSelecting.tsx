import { usePageTitle } from "../context/game";
import page from "../styles/page.module.css";
import styles from "./TopicSelecting.module.css";

function TopicSelecting() {
  usePageTitle("お題抽選中");

  return (
    <div className={`${page.page} ${styles.container}`}>
      <div className={styles.spinner} aria-hidden>
        🎲
      </div>
      <h1 className={styles.title}>
        お題抽選中<span className={styles.dots} />
      </h1>
      <p className={page.lead}>AIがお題について調べています。しばらくお待ちください。</p>
    </div>
  );
}

export default TopicSelecting;
