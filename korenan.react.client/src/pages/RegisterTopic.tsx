import { useActionState } from "react";
import { api, errorMessage } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { PlayerList } from "../components/PlayerList";
import { useGame, usePageTitle } from "../context/game";
import form from "../styles/form.module.css";
import page from "../styles/page.module.css";

function RegisterTopic() {
  const { scene } = useGame();
  usePageTitle("お題登録");

  const [error, formAction, isPending] = useActionState(async (_: string | undefined, formData: FormData) => {
    const { error } = await api.POST("/api/topic", { body: String(formData.get("topic") ?? "") });
    return error ? errorMessage(error, "お題登録に失敗しました") : undefined;
  }, undefined);

  return (
    <div className={page.page}>
      <Card variant="translucent">
        <h2 className={page.heading}>テーマ: 「{scene?.theme}」</h2>
        <p className={page.lead}>このテーマに関連するお題を考えてください</p>
      </Card>
      <Card>
        <form action={formAction} className={form.form}>
          <label className={form.field}>
            <span className={form.label}>お題</span>
            <input className={form.input} name="topic" placeholder="お題" autoComplete="off" required />
            <span className={form.hint}>なるべく正式名称や、他の人と名前が被らないお題を入れてください</span>
          </label>
          {error && <p className={form.error}>{error}</p>}
          <Button type="submit" variant="primary" block disabled={isPending}>
            {isPending ? "登録中…" : "登録する"}
          </Button>
        </form>
      </Card>
      <div className={page.mobileOnly}>
        <Card title="参加プレイヤー">
          <PlayerList />
        </Card>
      </div>
    </div>
  );
}

export default RegisterTopic;
