import { useContext, useEffect, useRef } from "react";
import { SceneContext, UserContext, TitleContext } from "../App";
import Config from "./Config";

function WaitRoundStart() {
  const [scene] = useContext(SceneContext);
  const [user] = useContext(UserContext);
  const [, setPageTitle] = useContext(TitleContext);
  const configDialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("WaitRoundStart"),
    });

    setPageTitle("マッチング中");
  }, [setPageTitle]);

  const startRound = async () => {
    await fetch("/api/start", { method: "POST" });
  };

  const shareAikotoba = () => {
    const url = `${window.location.origin}/regist?aikotoba=${scene?.aikotoba}`;
    if (navigator.share) {
      navigator.share({
        title: "これなんに招待",
        text: `一緒に遊ぼう！合言葉: ${scene?.aikotoba}\nこちらのURLから参加してね:`,
        url,
      });
    } else {
      navigator.clipboard.writeText(url);
      alert("招待URLをクリップボードにコピーしました。");
    }
  };

  const openConfigDialog = () => {
    configDialogRef.current?.showModal();
  };

  const closeConfigDialog = () => {
    configDialogRef.current?.close();
  };

  const banPlayer = async (playerId: string) => {
    await fetch("/api/ban", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(playerId),
    });
  };

  return (
    <div>
      <div>
        <h2>あいことば:</h2>
        <p>
          「{scene?.aikotoba}」 <button onClick={shareAikotoba}>共有</button>
        </p>
        <p>あいことばを共有することで、みんなで同じゲームに参加できますよ！ 「共有」ボタンを押して、あいことばをSNSなどでシェアしてくださいね。</p>
      </div>
      <div>
        <h2>参加プレイヤー:</h2>
        <ul>
          {scene?.players.map((player) => (
            <li key={player.id}>
              {player.name} {scene?.players[0].id === user?.id && player.id !== user?.id && <button onClick={() => banPlayer(player.id)}>BAN</button>}
            </li>
          ))}
        </ul>
      </div>
      <p>全てのプレイヤーがそろったら、「ラウンド開始」ボタンを押してゲームを始めましょう！</p>
      <p>得点設定は「設定」ボタンから変更できます。</p>
      <button onClick={startRound}>ラウンド開始❗</button>
      <button onClick={openConfigDialog}>設定</button>
      <dialog ref={configDialogRef}>
        <Config onClose={closeConfigDialog} />
      </dialog>
    </div>
  );
}

export default WaitRoundStart;
