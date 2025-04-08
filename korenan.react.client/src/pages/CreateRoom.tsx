import { useContext, useState, useEffect } from "react";
import { UserContext, TitleContext, SceneContext } from "../App";
import { useNavigate } from "react-router-dom";

function CreateRoom() {
  const [, startFetchingScene] = useContext(SceneContext);
  const [user, setUser] = useContext(UserContext);
  const [, setPageTitle] = useContext(TitleContext);
  const [name, setName] = useState(user?.name || "");
  const [aikotoba, setAikotoba] = useState("");
  const [theme, setTheme] = useState("");
  const navigate = useNavigate();

  useEffect(() => {
    setPageTitle("ルーム作成");
  }, [setPageTitle]);

  const createRoom = async () => {
    const response = await fetch("/api/createRoom", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, aikotoba, theme }),
    });

    if (!response.ok) {
      alert("ルーム作成に失敗しました");
      return;
    }

    const data = await response.json();
    setUser(data.user);
    await startFetchingScene();
    navigate("/RegisterTopic");
  };

  const isFormValid = name && aikotoba && theme;

  return (
    <div>
      <div>
        <label>プレイヤー名を入力してください:</label>
        <div>
          <input type="text" id="username" placeholder="プレイヤー名" autoComplete="on" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
      </div>

      <div>
        <label>テーマを設定してください:</label>
        <div>
          <input type="text" id="theme" placeholder="テーマ（例: 動物、食べ物、映画）" value={theme} onChange={(e) => setTheme(e.target.value)} />
        </div>
        <p>テーマの例: 「動物」「食べ物」「スポーツ選手」「映画」など</p>
      </div>

      <div>
        <label>あいことばを設定してください:</label>
        <div>
          <input type="text" id="aikotoba" placeholder="あいことば" autoComplete="on" value={aikotoba} onChange={(e) => setAikotoba(e.target.value)} />
        </div>
        <p>このあいことばを友達に教えて、同じゲームに参加してもらいましょう</p>
      </div>

      <button onClick={createRoom} disabled={!isFormValid}>
        ルーム作成
      </button>
    </div>
  );
}

export default CreateRoom;
