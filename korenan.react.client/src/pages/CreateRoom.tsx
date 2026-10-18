import { useActionState } from "react";
import { api, errorMessage } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { useGame, usePageTitle } from "../context/game";
import form from "../styles/form.module.css";
import page from "../styles/page.module.css";

interface FormState {
  error?: string;
  name: string;
  theme: string;
  aikotoba: string;
}

function CreateRoom() {
  const { user, setUser, reconnect } = useGame();
  usePageTitle("ルーム作成");

  const [state, formAction, isPending] = useActionState(
    async (_: FormState, formData: FormData): Promise<FormState> => {
      const values = {
        name: String(formData.get("name") ?? ""),
        theme: String(formData.get("theme") ?? ""),
        aikotoba: String(formData.get("aikotoba") ?? ""),
      };
      const { data, error } = await api.POST("/api/createRoom", { body: values });
      if (!data) {
        return { ...values, error: errorMessage(error, "ルーム作成に失敗しました") };
      }
      setUser(data);
      reconnect();
      return values;
    },
    { name: user?.name ?? "", theme: "", aikotoba: "" },
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
            <span className={form.label}>テーマ</span>
            <input className={form.input} name="theme" placeholder="テーマ（例: 動物、食べ物、映画）" defaultValue={state.theme} required />
            <span className={form.hint}>テーマの例: 「動物」「食べ物」「スポーツ選手」「映画」など</span>
          </label>
          <label className={form.field}>
            <span className={form.label}>あいことば</span>
            <input className={form.input} name="aikotoba" placeholder="あいことば" autoComplete="off" defaultValue={state.aikotoba} required />
            <span className={form.hint}>このあいことばを友達に教えて、同じゲームに参加してもらいましょう</span>
          </label>
          {state.error && <p className={form.error}>{state.error}</p>}
          <Button type="submit" variant="primary" block disabled={isPending}>
            {isPending ? "作成中…" : "ルーム作成"}
          </Button>
        </form>
      </Card>
    </div>
  );
}

export default CreateRoom;
