import { useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { Config } from "../models";

function Home() {
  const navigate = useNavigate();
  const [config, setConfig] = useState<Config>();

  useEffect(() => {
    fetchConfig();
  }, []);

  const fetchConfig = async () => {
    const response = await fetch("/api/config");
    const data: Config = await response.json();
    setConfig(data);
  };

  return (
    <div>
      <h1>これなーんだ❓(ライアー)</h1>
      <button onClick={() => navigate("/regist")}>参加！</button>
      <h2>遊び方</h2>
      <ol>
        <li>各プレイヤーが自分の名前とお題を登録します。</li>
        <li>全員が登録を完了したら、ゲームが開始されます。</li>
        <li>
          プレイヤーは同時に生成AIに「はい」か「いいえ」で答えられる質問を投げかけ、回答を得ます。
        </li>
        <li>質問の途中でも、お題が分かったと思った時点で推測が可能です。</li>
        <li>
          お題を当てた場合、その時点でフェーズが終了し、次の手順へ移行します。
        </li>
        <li>正解者には{config?.correctPoint}ポイントが付与されます。</li>
        <li>さらにお題を考えた「ライアープレイヤー」を推理します。</li>
        <li>
          ライアープレイヤーを当てた場合、推測者に{config?.liarPoint}
          ポイントが付与されます。
        </li>
        <li>
          誰もお題を当てることができなかった場合は、ライアープレイヤーが
          {config?.noCorrectPoint}ポイント。
        </li>
        <li>全員のお題が順番に推測対象になるまで繰り返します。</li>
        <li>
          最終得点を集計し、最もポイントを多く稼いだプレイヤーが勝利します！
        </li>
      </ol>
    </div>
  );
}

export default Home;
