import { useActionState } from "react";
import { useSearchParams } from "react-router";
import { api, errorMessage } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { useGame, usePageTitle } from "../context/game";
import form from "../styles/form.module.css";
import page from "../styles/page.module.css";

interface FormState {
  error?: string;
  name: string;
  aikotoba: string;
}

function JoinRoom() {
  const { user, setUser, reconnect } = useGame();
  const [searchParams] = useSearchParams();
  usePageTitle("ルーム参加");

  const [state, formAction, isPending] = useActionState(
    async (_: FormState, formData: FormData): Promise<FormState> => {
      const values = {
        name: String(formData.get("name") ?? ""),
        aikotoba: String(formData.get("aikotoba") ?? ""),
      };
      const { data, error } = await api.POST("/api/joinRoom", { body: values });
      if (!data) {
        return { ...values, error: errorMessage(error, "ルーム参加に失敗しました") };
      }
      setUser(data);
      reconnect();
      return values;
    },
    { name: user?.name ?? "", aikotoba: searchParams.get("aikotoba") ?? "" },
  );

  return (
    <div className={page.page}>
      <Card>
        <form key={user?.id} action={formAction} className={form.form}>
          <label className={form.field}>
            <span className={form.label}>プレイヤー名</span>
            <input className={form.input} name="name" placeholder="プレイヤー名" autoComplete="nickname" defaultValue={state.name || user?.name} required />
          </label>
          <label className={form.field}>
            <span className={form.label}>あいことば</span>
            <input className={form.input} name="aikotoba" placeholder="あいことば" autoComplete="off" defaultValue={state.aikotoba} required />
            <span className={form.hint}>ルーム作成者から教えてもらったあいことばを入力してください</span>
          </label>
          {state.error && <p className={form.error}>{state.error}</p>}
          <Button type="submit" variant="primary" block disabled={isPending}>
            {isPending ? "参加中…" : "ルームに参加"}
          </Button>
        </form>
      </Card>
    </div>
  );
}

export default JoinRoom;
