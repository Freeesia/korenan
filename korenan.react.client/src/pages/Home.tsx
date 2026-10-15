import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { api, type Config } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { APP_TITLE, usePageTitle } from "../context/game";
import { transitionTo } from "../navigation";
import page from "../styles/page.module.css";
import styles from "./Home.module.css";

function Home() {
  const navigate = useNavigate();
  const [config, setConfig] = useState<Config>();
  usePageTitle(APP_TITLE);

  useEffect(() => {
    const fetchConfig = async () => {
      const { data } = await api.GET("/api/config");
      setConfig(data);
    };
    void fetchConfig();
  }, []);

  return (
    <div className={page.page}>
      <div className={styles.hero}>
        <h1 className={styles.title}>{APP_TITLE}</h1>
        <p className={page.lead}>AIに質問してお題を当てよう。お題を考えた「ライアー」は誰だ？</p>
        <div className={styles.actions}>
          <Button variant="primary" size="lg" onClick={() => transitionTo(navigate, "/CreateRoom")}>
            ルームを作成
          </Button>
          <Button size="lg" onClick={() => transitionTo(navigate, "/JoinRoom")}>
            ルームに参加
          </Button>
        </div>
      </div>
      <Card title="遊び方">
        <ol className={styles.rules}>
          <li>まずはルームを作成するか、既存のルームに参加します。</li>
          <li>ルーム作成時にはテーマを設定します（例：「動物」「食べ物」など）。</li>
          <li>各プレイヤーはテーマに沿ったお題を1つ登録します。</li>
          <li>全員が登録を完了したら、ゲームがスタートします。</li>
          <li>プレイヤーは生成AIに「はい」か「いいえ」で答えられる質問を投げかけ、回答を得ます。</li>
          <li>質問の途中でも、お題が分かったと思ったら推測が可能です。</li>
          <li>お題を当てた場合、{config?.correctPoint}ポイントがもらえます。</li>
          <li>さらに、お題を考えた「ライアープレイヤー」を推理します。</li>
          <li>ライアープレイヤーを当てた場合、推測者に{config?.liarPoint}ポイントがもらえます。</li>
          <li>誰もお題を当てることができなかった場合、ライアープレイヤーが{config?.noCorrectPoint}ポイントになります。</li>
          <li>全員のお題が順番に推測対象になるまで繰り返します。</li>
          <li>最終得点を集計し、最もポイントを多く稼いだプレイヤーが勝利します！</li>
        </ol>
      </Card>
    </div>
  );
}

export default Home;
