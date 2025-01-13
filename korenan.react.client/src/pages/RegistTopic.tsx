import { useContext, useState, useEffect } from "react";
import { UserContext } from "../App";
import { Player } from "../models";
import { useNavigate, useLocation } from "react-router-dom";

function RegistTopic() {
  const [user, setUser] = useContext(UserContext);
  const [name, setName] = useState(user?.name || "");
  const [topic, setTopic] = useState("");
  const [aikotoba, setAikotoba] = useState("");
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const aikotobaParam = params.get("aikotoba");
    if (aikotobaParam) {
      setAikotoba(aikotobaParam);
    }
  }, [location.search]);

  const register = async () => {
    const response = await fetch("/api/regist", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, topic, aikotoba }),
    });
    const data: Player = await response.json();
    setUser(data);
    navigate("/WaitRoundStart");
  };

  return (
    <div>
      <h1>お題登録</h1>
      <label>プレイヤー名を入力してください:</label>
      <div>
        <input
          type="text"
          placeholder="プレイヤー名"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <label>
        一緒に遊ぶ友達と共有するための「あいことば」を入力してください:
      </label>
      <div>
        <input
          type="text"
          placeholder="あいことば"
          value={aikotoba}
          onChange={(e) => setAikotoba(e.target.value)}
        />
      </div>
      <label>
        お題を入力してください:(なるべく正式名称や名前が被らないお題を入れてください)
      </label>
      <div>
        <input
          type="text"
          placeholder="お題"
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
        />
      </div>
      <button onClick={register}>参加！</button>
    </div>
  );
}

export default RegistTopic;
