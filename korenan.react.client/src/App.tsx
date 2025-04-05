import {
  Routes,
  Route,
  NavLink,
  useNavigate,
  useLocation,
} from "react-router-dom";
import { createContext, useState, useEffect, useRef, useCallback } from "react";
import "./App.css";
import Home from "./pages/Home";
import Debug from "./pages/Debug";
import RegistTopic from "./pages/RegistTopic";
import WaitRoundStart from "./pages/WaitRoundStart";
import TopicSelecting from "./pages/TopicSelecting";
import QuestionAnswering from "./pages/QuestionAnswering";
import LiarGuess from "./pages/LiarGuess";
import RoundSummary from "./pages/RoundSummary";
import GameEnd from "./pages/GameEnd";
import { CurrentScene, User } from "./models";

export const SceneContext = createContext<
  [CurrentScene | undefined, () => Promise<void>]
>([undefined, async () => {}]);
export const UserContext = createContext<[User | undefined, (u: User) => void]>(
  [undefined, () => {}]
);

function App() {
  const [scene, setScene] = useState<CurrentScene>();
  const [user, setUser] = useState<User>();
  const [lastFetchTime, setLastFetchTime] = useState<Date>();
  const [menuOpen, setMenuOpen] = useState(false);
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
    const data = await response.json();
    setScene(data);
    setLastFetchTime(new Date());
  }, [stopFetchingScene]);

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

  useEffect(() => {
    fetchUser();
    startFetchingScene();
    return stopFetchingScene;
  }, [startFetchingScene, stopFetchingScene]);

  useEffect(() => {
    const currentPath = location.pathname.substring(1);
    if (currentPath === "debug") {
      return;
    }
    if (currentPath === "regist" && !scene) {
      return;
    }
    const page = scene?.scene ?? "";
    if (page === currentPath) {
      return;
    }
    document.startViewTransition(() => {
      navigate(`/${page}`, { replace: true });
    });
  }, [scene, location, navigate]);

  return (
    <SceneContext.Provider value={[scene, startFetchingScene]}>
      <UserContext.Provider value={[user, setUser]}>
        <div className="app-container">
          <nav className="mobile-nav">
            <div className="nav-content">
              <div className="app-title">コレナン</div>
              <div className="hamburger-icon" onClick={toggleMenu}>
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
            <div className={`mobile-menu ${menuOpen ? "open" : ""}`}>
              <ul>
                <li>
                  <NavLink to="/debug" onClick={() => setMenuOpen(false)}>デバッグ</NavLink>
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
            <Route path="/regist" element={<RegistTopic />} />
            <Route path="/WaitRoundStart" element={<WaitRoundStart />} />
            <Route path="/TopicSelecting" element={<TopicSelecting />} />
            <Route path="/QuestionAnswering" element={<QuestionAnswering />} />
            <Route path="/LiarGuess" element={<LiarGuess />} />
            <Route path="/RoundSummary" element={<RoundSummary />} />
            <Route path="/GameEnd" element={<GameEnd />} />
          </Routes>
          <footer>
            <div>
              最終更新日時: {lastFetchTime?.toLocaleTimeString()} | プレイヤー:{" "}
              {user?.name}
            </div>
          </footer>
        </div>
      </UserContext.Provider>
    </SceneContext.Provider>
  );
}

export default App;
