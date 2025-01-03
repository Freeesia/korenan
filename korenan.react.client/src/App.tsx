import { Routes, Route, NavLink } from "react-router-dom";
import { createContext, useState, useEffect } from "react";
import "./App.css";
import Home from "./pages/Home";
import Weather from "./pages/Weather";
import Debug from "./pages/Debug";
import { CurrentScene } from "./models";

export const SceneContext = createContext<CurrentScene | undefined>(undefined);

function App() {
  const [scene, setScene] = useState<CurrentScene>();
  const [lastFetchTime, setLastFetchTime] = useState<Date>();

  const fetchScene = async () => {
    const response = await fetch("/api/scene");
    const data = await response.json();
    setScene(data);
    setLastFetchTime(new Date());
  };

  useEffect(() => {
    const interval = setInterval(fetchScene, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <SceneContext value={scene}>
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
          </ul>
        </nav>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/weather" element={<Weather />} />
          <Route path="/debug" element={<Debug />} />
        </Routes>
        <footer>
          <div>Last fetch time: {lastFetchTime?.toLocaleTimeString()}</div>
        </footer>
      </div>
    </SceneContext>
  );
}

export default App;
