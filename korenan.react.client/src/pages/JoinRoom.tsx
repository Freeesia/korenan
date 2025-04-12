import { useContext, useState, useEffect } from "react";
import { UserContext, TitleContext, SceneContext } from "../App";
import { useNavigate, useLocation } from "react-router-dom";

function JoinRoom() {
  const [, startFetchingScene] = useContext(SceneContext);
  const [user, setUser] = useContext(UserContext);
  const [, setPageTitle] = useContext(TitleContext);
  const [name, setName] = useState(user?.name || "");
  const [aikotoba, setAikotoba] = useState("");
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const aikotobaParam = params.get("aikotoba");
    if (aikotobaParam) {
      setAikotoba(aikotobaParam);
    }

    setPageTitle("ルーム参加");

    // ユーザーコンテキストに値がある場合はユーザー名を更新
    if (user?.name && !name) {
      setName(user.name);
    }
  }, [location.search, setPageTitle, user, name]);

  const joinRoom = async () => {
    const response = await fetch("/api/joinRoom", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, aikotoba }),
    });

    if (!response.ok) {
      alert("ルーム参加に失敗しました");
      return;
    }

    const data = await response.json();
    setUser(data);
    await startFetchingScene();
    navigate("/RegisterTopic");
  };

  const isFormValid = name && aikotoba;

  return (
    <div>
      <div>
        <label>プレイヤー名を入力してください:</label>
        <div>
          <input type="text" id="username" placeholder="プレイヤー名" autoComplete="on" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
      </div>

      <div>
        <label>あいことばを入力してください:</label>
        <div>
          <input type="text" id="aikotoba" placeholder="あいことば" autoComplete="on" value={aikotoba} onChange={(e) => setAikotoba(e.target.value)} />
        </div>
        <p>ルーム作成者から教えてもらったあいことばを入力してください</p>
      </div>

      <button onClick={joinRoom} disabled={!isFormValid}>
        ルームに参加
      </button>
    </div>
  );
}

export default JoinRoom;
