import { Routes, Route, Link } from "react-router-dom";
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
            <Link to="/">Home</Link>
          </li>
          <li>
            <Link to="/weather">Weather</Link>
          </li>
          <li>
            <Link to="/debug">Debug</Link>
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
