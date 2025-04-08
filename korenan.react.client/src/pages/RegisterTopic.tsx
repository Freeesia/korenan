import { useContext, useState, useEffect } from "react";
import { SceneContext, TitleContext } from "../App";
import { useNavigate } from "react-router-dom";

function RegisterTopic() {
  const [scene] = useContext(SceneContext);
  const [, setPageTitle] = useContext(TitleContext);
  const [topic, setTopic] = useState("");
  const navigate = useNavigate();

  useEffect(() => {
    setPageTitle("お題登録");
  }, [setPageTitle]);

  const register = async () => {
    const response = await fetch("/api/topic", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(topic),
    });

    if (!response.ok) {
      alert("お題登録に失敗しました");
      return;
    }

    navigate("/WaitRoundStart");
  };

  const isFormValid = topic;

  return (
    <div>
      {scene?.theme && (
        <div>
          <h2>テーマ: {scene.theme}</h2>
          <p>このテーマに関連するお題を考えてください</p>
        </div>
      )}
      <div>
        <label>お題を入力してください:</label>
        <div>
          <input type="text" placeholder="お題" value={topic} onChange={(e) => setTopic(e.target.value)} />
        </div>
        <p>
          テーマ「{scene?.theme}」に沿ったお題を登録してください
          <br />
          （なるべく正式名称や名前が被らないお題を入れてください）
        </p>
      </div>
      <button onClick={register} disabled={!isFormValid}>
        登録する
      </button>
    </div>
  );
}

export default RegisterTopic;
