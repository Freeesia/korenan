import { useNavigate } from "react-router-dom";

function Home() {
  const navigate = useNavigate();

  return (
    <div>
      <h1>これなーんだ❓(ライアー)</h1>
      <button onClick={() => navigate("/regist")}>参加！</button>
    </div>
  );
}

export default Home;
