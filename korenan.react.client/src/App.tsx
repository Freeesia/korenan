import { Routes, Route } from "react-router-dom";
import "./App.css";
import Home from "./pages/Home";
import Weather from "./pages/Weather";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/weather" element={<Weather />} />
    </Routes>
  );
}

export default App;
