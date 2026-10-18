import { lazy, Suspense, useEffect, useState } from "react";
import { Route, Routes, useLocation, useNavigate } from "react-router";
import { withSentryReactRouterV7Routing } from "@sentry/react";
import { api } from "./api/client";
import { GameInfoPanel } from "./components/GameInfoPanel";
import { PlayerList } from "./components/PlayerList";
import { useCurrentPlayer, useGame } from "./context/game";
import { transitionTo } from "./navigation";
import Home from "./pages/Home";
import CreateRoom from "./pages/CreateRoom";
import JoinRoom from "./pages/JoinRoom";
import RegisterTopic from "./pages/RegisterTopic";
import WaitRoundStart from "./pages/WaitRoundStart";
import TopicSelecting from "./pages/TopicSelecting";
import QuestionAnswering from "./pages/QuestionAnswering";
import LiarGuess from "./pages/LiarGuess";
import GameEnd from "./pages/GameEnd";
import styles from "./App.module.css";

const SentryRoutes = withSentryReactRouterV7Routing(Routes);
const Debug = import.meta.env.DEV ? lazy(() => import("./pages/Debug")) : undefined;
const PUBLIC_PATHS = ["/", "/createroom", "/joinroom"];

function useSceneNavigation() {
  const { scene, user } = useGame();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  useEffect(() => {
    const path = pathname.toLowerCase();
    if (scene === undefined || path === "/debug") {
      return;
    }
    if (scene === null) {
      if (!PUBLIC_PATHS.includes(path)) {
        transitionTo(navigate, "/");
      }
      return;
    }
    if (!user) {
      return;
    }
    const player = scene.players.find((p) => p.id === user.id);
    const target = `/${scene.scene === "WaitRoundStart" ? (player?.currentScene ?? "RegisterTopic") : scene.scene}`;
    if (path !== target.toLowerCase()) {
      transitionTo(navigate, target);
    }
  }, [scene, user, pathname, navigate]);
}

function Header() {
  const { scene, user, title } = useGame();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);

  const leaveGame = async () => {
    if (!user) {
      return;
    }
    await api.POST("/api/ban", { body: user.id });
    setMenuOpen(false);
  };

  return (
    <header className={styles.header}>
      <button type="button" className={styles.title} onClick={() => transitionTo(navigate, "/")}>
        {title}
      </button>
      <button type="button" className={styles.hamburger} onClick={() => setMenuOpen(!menuOpen)} aria-label="メニュー" aria-expanded={menuOpen}>
        <span />
        <span />
        <span />
      </button>
      <nav className={`${styles.menu} ${menuOpen ? styles.open : ""}`}>
        <ul>
          {import.meta.env.DEV && (
            <>
              <li>
                <button type="button" onClick={() => { setMenuOpen(false); transitionTo(navigate, "/debug"); }}>
                  デバッグ
                </button>
              </li>
              <li>
                <a href="/scalar" target="_blank" rel="noreferrer" onClick={() => setMenuOpen(false)}>
                  API リファレンス
                </a>
              </li>
            </>
          )}
          {scene && (
            <li>
              <button type="button" onClick={leaveGame}>
                ゲームを抜ける
              </button>
            </li>
          )}
        </ul>
      </nav>
    </header>
  );
}

function Footer() {
  const { scene, user } = useGame();
  const player = useCurrentPlayer();
  if (!user) {
    return null;
  }
  return (
    <footer className={styles.footer}>
      プレイヤー: {user.name}
      {scene && player && ` (得点: ${player.points})`}
    </footer>
  );
}

function App() {
  const { scene } = useGame();
  const { pathname } = useLocation();
  useSceneNavigation();
  const inGame = !!scene && pathname.toLowerCase() !== "/debug";

  const routes = (
    <SentryRoutes>
      <Route path="/" element={<Home />} />
      <Route path="/CreateRoom" element={<CreateRoom />} />
      <Route path="/JoinRoom" element={<JoinRoom />} />
      <Route path="/RegisterTopic" element={<RegisterTopic />} />
      <Route path="/WaitRoundStart" element={<WaitRoundStart />} />
      <Route path="/TopicSelecting" element={<TopicSelecting />} />
      <Route path="/QuestionAnswering" element={<QuestionAnswering />} />
      <Route path="/LiarGuess" element={<LiarGuess />} />
      <Route path="/GameEnd" element={<GameEnd />} />
      {Debug && (
        <Route
          path="/debug"
          element={
            <Suspense>
              <Debug />
            </Suspense>
          }
        />
      )}
    </SentryRoutes>
  );

  return (
    <div className={styles.shell}>
      <Header />
      {inGame ? (
        <div className={styles.game}>
          <aside className={`${styles.side} ${styles.left}`}>
            <h2 className={styles.sideTitle}>プレイヤー</h2>
            <PlayerList />
          </aside>
          <main className={styles.center}>{routes}</main>
          <aside className={`${styles.side} ${styles.right}`}>
            <GameInfoPanel />
          </aside>
        </div>
      ) : (
        <main className={styles.main}>{routes}</main>
      )}
      <Footer />
    </div>
  );
}

export default App;
