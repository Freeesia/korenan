import { useActionState } from "react";
import { api } from "../api/client";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { useGame, usePageTitle } from "../context/game";
import form from "../styles/form.module.css";
import styles from "./Debug.module.css";

function Debug() {
  const { scene, user, reconnect } = useGame();
  usePageTitle("デバッグ");

  const [result, formAction, isPending] = useActionState(async (_: unknown, formData: FormData) => {
    const text = String(formData.get("text") ?? "");
    switch (formData.get("op")) {
      case "start":
        return await api.POST("/api/start");
      case "question":
        return await api.POST("/api/question", { body: text });
      case "answer":
        return await api.POST("/api/answer", { body: text });
      case "guess":
        return await api.POST("/api/guess", { body: text });
      case "reset":
        return await api.POST("/api/reset");
      default:
        return undefined;
    }
  }, undefined);

  return (
    <div className={styles.container}>
      <Card title="API">
        <form action={formAction} className={form.form}>
          <input className={form.input} name="text" placeholder="質問 / 解答 / 推理対象のプレイヤーID" />
          <div className={form.actions}>
            {["start", "question", "answer", "guess", "reset"].map((op) => (
              <Button key={op} type="submit" name="op" value={op} size="sm" disabled={isPending}>
                {op}
              </Button>
            ))}
            <Button size="sm" onClick={reconnect}>
              reconnect
            </Button>
            <a href="/scalar" target="_blank" rel="noreferrer">
              API リファレンス
            </a>
          </div>
        </form>
        <pre className={styles.pre}>{JSON.stringify(result && { status: result.response.status, data: result.data, error: result.error }, null, 2)}</pre>
      </Card>
      <Card title="User">
        <pre className={styles.pre}>{JSON.stringify(user, null, 2)}</pre>
      </Card>
      <Card title="Scene">
        <pre className={styles.pre}>{JSON.stringify(scene, null, 2)}</pre>
      </Card>
    </div>
  );
}

export default Debug;
