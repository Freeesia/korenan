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

  const regist = () => {
    document.startViewTransition(() => {
      navigate("/regist");
    });
  };

  return (
    <div>
      <h1>これなーんだ❓(ライアー)</h1>
      <button onClick={regist}>参加！</button>
      <h2>遊び方</h2>
      <ol>
        <li>まずは各プレイヤーが自分の名前とお題を登録します。</li>
        <li>全員が登録を完了したら、ゲームがスタートします。</li>
        <li>
          プレイヤーは生成AIに「はい」か「いいえ」で答えられる質問を投げかけ、回答を得ます。
        </li>
        <li>質問の途中でも、お題が分かったと思ったら推測が可能です。</li>
        <li>お題を当てた場合、{config?.correctPoint}ポイントがもらえます。</li>
        <li>さらに、お題を考えた「ライアープレイヤー」を推理します。</li>
        <li>
          ライアープレイヤーを当てた場合、推測者に{config?.liarPoint}
          ポイントがもらえます。
        </li>
        <li>
          誰もお題を当てることができなかった場合、ライアープレイヤーが
          {config?.noCorrectPoint}ポイントになります。
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
