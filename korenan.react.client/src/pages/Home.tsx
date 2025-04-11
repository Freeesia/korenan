import { useNavigate } from "react-router-dom";
import { useEffect, useState, useContext } from "react";
import { Config } from "../models";
import { TitleContext } from "../App";

function Home() {
  const navigate = useNavigate();
  const [config, setConfig] = useState<Config>();
  const [, setPageTitle] = useContext(TitleContext);

  useEffect(() => {
    fetchConfig();
    setPageTitle("これなーんだ❓(ライアー)");
  }, [setPageTitle]);

  const fetchConfig = async () => {
    const response = await fetch("/api/config");
    const data: Config = await response.json();
    setConfig(data);
  };

  const navigateToCreateRoom = () => {
    document.startViewTransition(() => {
      navigate("/createRoom");
    });
  };

  const navigateToJoinRoom = () => {
    document.startViewTransition(() => {
      navigate("/joinRoom");
    });
  };

  return (
    <div>
      <h1>これなーんだ❓(ライアー)</h1>
      <div className="action-buttons">
        <button onClick={navigateToCreateRoom}>ルームを作成</button>
        <button onClick={navigateToJoinRoom}>ルームに参加</button>
      </div>
      <h2>遊び方</h2>
      <ol>
        <li>まずはルームを作成するか、既存のルームに参加します。</li>
        <li>ルーム作成時にはテーマを設定します（例：「動物」「食べ物」など）。</li>
        <li>各プレイヤーはテーマに沿ったお題を1つ登録します。</li>
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
