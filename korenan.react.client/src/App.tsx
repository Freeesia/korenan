import { Routes, Route, NavLink, useNavigate, useLocation } from "react-router-dom";
import { createContext, useState, useEffect, useRef, useCallback } from "react";
import "./App.css";
import Home from "./pages/Home";
import Debug from "./pages/Debug";
import RegisterTopic from "./pages/RegisterTopic";
import WaitRoundStart from "./pages/WaitRoundStart";
import TopicSelecting from "./pages/TopicSelecting";
import QuestionAnswering from "./pages/QuestionAnswering";
import LiarGuess from "./pages/LiarGuess";
import GameEnd from "./pages/GameEnd";
import CreateRoom from "./pages/CreateRoom";
import JoinRoom from "./pages/JoinRoom";
import { CurrentScene, User } from "./models";

const APP_TITLE = "これなーんだ❓(ライアー)";

export const SceneContext = createContext<[CurrentScene | undefined, () => Promise<void>]>([undefined, async () => {}]);
export const UserContext = createContext<[User | undefined, (u: User) => void]>([undefined, () => {}]);
export const TitleContext = createContext<[string, (title: string) => void]>([APP_TITLE, () => {}]);

function App() {
  const [scene, setScene] = useState<CurrentScene>();
  const [user, setUser] = useState<User>();
  const [lastFetchTime, setLastFetchTime] = useState<Date>();
  const [menuOpen, setMenuOpen] = useState(false);
  const [pageTitle, setPageTitle] = useState(APP_TITLE);
  const [points, setPoints] = useState(0);
  const navigate = useNavigate();
  const location = useLocation();
  const intervalId = useRef<NodeJS.Timeout>(undefined);

  const stopFetchingScene = useCallback(() => {
    const id = intervalId.current;
    intervalId.current = undefined;
    clearInterval(id);
  }, []);

  const fetchScene = useCallback(async () => {
    const response = await fetch("/api/scene");
    if (response.status === 404) {
      stopFetchingScene();
      setScene(undefined);
      return;
    }
    if (!response.ok) {
      return;
    }
    const data: CurrentScene = await response.json();
    setScene(data);

    // シーン情報とユーザー情報があれば得点を更新
    if (data && user) {
      const currentPlayer = data.players.find((p) => p.id === user.id);
      if (currentPlayer) {
        setPoints(currentPlayer.points);
      }
    }

    setLastFetchTime(new Date());
  }, [stopFetchingScene, user]);

  const fetchUser = async () => {
    const response = await fetch("/api/me");
    if (response.ok) {
      const data: User = await response.json();
      setUser(data);
    }
  };

  const leaveGame = async () => {
    if (!user) return;
    await fetch("/api/ban", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(user.id),
    });
    setScene(undefined);
    setMenuOpen(false);
  };

  const startFetchingScene = useCallback(async () => {
    await fetchScene();
    if (!intervalId.current) {
      intervalId.current = setInterval(fetchScene, 1000);
    }
  }, [fetchScene]);

  const toggleMenu = () => {
    setMenuOpen(!menuOpen);
  };

  const navigateTo = useCallback(
    (path: string) => {
      if (location.pathname === path) {
        return;
      }
      document.startViewTransition(() => {
        navigate(path);
      });
    },
    [navigate, location.pathname]
  );

  const navigateToHome = () => {
    navigateTo("/");
  };

  useEffect(() => {
    fetchUser();
    startFetchingScene();
    return stopFetchingScene;
  }, [startFetchingScene, stopFetchingScene]);

  useEffect(() => {
    const currentPath = location.pathname.substring(1);
    // デバッグ画面は常に表示可能
    if (currentPath === "debug") {
      return;
    }
    // ルーム作成・参加画面はシーン情報がない場合は表示可能
    if ((currentPath === "createRoom" || currentPath === "joinRoom") && !scene) {
      return;
    }
    // シーン情報かユーザーがない場合はホームに遷移
    if (!scene || !user) {
      navigateToHome();
      return;
    }

    // プレイヤー個別の状態を取得
    const currentPlayer = scene.players.find((p) => p.id === user.id);
    if (!currentPlayer) {
      navigateToHome();
      return;
    }
    const playerScene = currentPlayer.currentScene;
    const gameScene = scene.scene;

    // ゲームシーンに応じて遷移
    if (scene.scene === "WaitRoundStart") {
      navigateTo(`/${playerScene}`);
    } else {
      navigateTo(`/${gameScene}`);
    }
  }, [scene, user, location, navigateToHome]);

  return (
    <SceneContext.Provider value={[scene, startFetchingScene]}>
      <UserContext.Provider value={[user, setUser]}>
        <TitleContext.Provider value={[pageTitle, setPageTitle]}>
          <div className="app-container">
            <nav className="mobile-nav">
              <div className="nav-content">
                <button onClick={navigateToHome} className="app-title-button">
                  {pageTitle}
                </button>
                <div className="hamburger-icon" onClick={toggleMenu}>
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
              <div className={`mobile-menu ${menuOpen ? "open" : ""}`}>
                <ul>
                  <li>
                    <NavLink to="/debug" onClick={() => setMenuOpen(false)}>
                      デバッグ
                    </NavLink>
                  </li>
                  {scene && (
                    <li>
                      <button onClick={leaveGame}>ゲームを抜ける</button>
                    </li>
                  )}
                </ul>
              </div>
            </nav>
            <Routes>
              <Route path="/" element={<Home />} />
              <Route path="/debug" element={<Debug />} />
              <Route path="/RegisterTopic" element={<RegisterTopic />} />
              <Route path="/WaitRoundStart" element={<WaitRoundStart />} />
              <Route path="/TopicSelecting" element={<TopicSelecting />} />
              <Route path="/QuestionAnswering" element={<QuestionAnswering />} />
              <Route path="/LiarGuess" element={<LiarGuess />} />
              <Route path="/GameEnd" element={<GameEnd />} />
              <Route path="/CreateRoom" element={<CreateRoom />} />
              <Route path="/JoinRoom" element={<JoinRoom />} />
            </Routes>
            <footer>
              <div>
                最終更新日時: {lastFetchTime?.toLocaleTimeString()} | プレイヤー: {user?.name}
                {scene && ` (得点: ${points})`}
              </div>
            </footer>
          </div>
        </TitleContext.Provider>
      </UserContext.Provider>
    </SceneContext.Provider>
  );
}

export default App;
