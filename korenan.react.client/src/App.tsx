import {
  Routes,
  Route,
  NavLink,
  useNavigate,
  useLocation,
} from "react-router-dom";
import { createContext, useState, useEffect, useRef } from "react";
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
  const navigate = useNavigate();
  const location = useLocation();
  const intervalId = useRef<NodeJS.Timeout>(undefined);

  const fetchScene = async () => {
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
  };

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
  };

  const startFetchingScene = async () => {
    await fetchScene();
    if (!intervalId.current) {
      intervalId.current = setInterval(fetchScene, 1000);
    }
  };

  const stopFetchingScene = () => {
    const id = intervalId.current;
    intervalId.current = undefined;
    clearInterval(id);
  };

  useEffect(() => {
    fetchUser();
    startFetchingScene();
    return stopFetchingScene;
  }, [startFetchingScene]);

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
    <SceneContext value={[scene, startFetchingScene]}>
      <UserContext value={[user, setUser]}>
        <div className="app-container">
          <nav>
            <ul>
              <li>
                <NavLink to="/debug">Debug</NavLink>
              </li>
              {scene && (
                <li style={{ marginLeft: "auto" }}>
                  <button onClick={leaveGame}>ゲームを抜ける</button>
                </li>
              )}
            </ul>
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
      </UserContext>
    </SceneContext>
  );
}

export default App;
