import { Routes, Route, NavLink } from "react-router-dom";
import "./App.css";
import Home from "./pages/Home";
import Weather from "./pages/Weather";
import Debug from "./pages/Debug";

function App() {
  return (
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
    </div>
  );
}

export default App;
