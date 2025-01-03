import { Routes, Route, NavLink } from "react-router-dom";
import { createContext, useState, useEffect } from "react";
import "./App.css";
import Home from "./pages/Home";
import Weather from "./pages/Weather";
import Debug from "./pages/Debug";
import Config from "./pages/Config";
import { CurrentScene, User } from "./models";

export const SceneContext = createContext<CurrentScene | undefined>(undefined);
export const UserContext = createContext<[User | undefined, (u: User) => void]>(
  [undefined, () => {}]
);

function App() {
  const [scene, setScene] = useState<CurrentScene>();
  const [user, setUser] = useState<User>();
  const [lastFetchTime, setLastFetchTime] = useState<Date>();

  const fetchScene = async () => {
    const response = await fetch("/api/scene");
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

  useEffect(() => {
    fetchUser();
    const interval = setInterval(fetchScene, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <SceneContext value={scene}>
      <UserContext value={[user, setUser]}>
        <div>
          <nav>
            <ul>
              <li>
                <NavLink to="/">Home</NavLink>
              </li>
              <li>
                <NavLink to="/weather">Weather</NavLink>
              </li>
              <li>
                <NavLink to="/debug">Debug</NavLink>
              </li>
              <li>
                <NavLink to="/config">Config</NavLink>
              </li>
            </ul>
          </nav>
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/weather" element={<Weather />} />
            <Route path="/debug" element={<Debug />} />
            <Route path="/config" element={<Config />} />
          </Routes>
          <footer>
            <div>
              最終更新日時: {lastFetchTime?.toLocaleTimeString()} |
              現在のシーン: {scene?.scene} | プレイヤー: {user?.name}
            </div>
          </footer>
        </div>
      </UserContext>
    </SceneContext>
  );
}

export default App;
