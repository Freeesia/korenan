import {
  Routes,
  Route,
  NavLink,
  useNavigate,
  useLocation,
} from "react-router-dom";
import { createContext, useState, useEffect } from "react";
import "./App.css";
import Home from "./pages/Home";
import Debug from "./pages/Debug";
import Config from "./pages/Config";
import RegistTopic from "./pages/RegistTopic";
import WaitRoundStart from "./pages/WaitRoundStart";
import QuestionAnswering from "./pages/QuestionAnswering";
import LiarGuess from "./pages/LiarGuess";
import RoundSummary from "./pages/RoundSummary";
import GameEnd from "./pages/GameEnd";
import { CurrentScene, User } from "./models";

export const SceneContext = createContext<CurrentScene | undefined>(undefined);
export const UserContext = createContext<[User | undefined, (u: User) => void]>(
  [undefined, () => {}]
);

function App() {
  const [scene, setScene] = useState<CurrentScene>();
  const [user, setUser] = useState<User>();
  const [lastFetchTime, setLastFetchTime] = useState<Date>();
  const navigate = useNavigate();
  const location = useLocation();

  const fetchScene = async () => {
    const response = await fetch("/api/scene");
    if (!response.ok) {
      setScene(undefined);
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

  useEffect(() => {
    fetchUser();
    const interval = setInterval(fetchScene, 1000);
    return () => clearInterval(interval);
  }, []);

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
    navigate(`/${page}`, { replace: true });
  }, [scene, location]);

  return (
    <SceneContext value={scene}>
      <UserContext value={[user, setUser]}>
        <div className="app-container">
          <nav>
            <ul>
              <li>
                <NavLink to="/">Home</NavLink>
              </li>
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
            <Route path="/config" element={<Config />} />
            <Route path="/regist" element={<RegistTopic />} />
            <Route path="/WaitRoundStart" element={<WaitRoundStart />} />
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
