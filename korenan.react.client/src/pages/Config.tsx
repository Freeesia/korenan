import { useActionState, useEffect, useState } from "react";
import { api, errorMessage, type Config } from "../api/client";
import { Button } from "../components/Button";
import form from "../styles/form.module.css";

const FIELDS: { key: keyof Config; label: string; step?: number; min?: number; max?: number; scale?: number }[] = [
  { key: "questionLimit", label: "質問回数の上限", min: 0 },
  { key: "answerLimit", label: "解答回数の上限", min: 0 },
  { key: "correctPoint", label: "正解時のポイント" },
  { key: "liarPoint", label: "ライアーを当てたときのポイント" },
  { key: "noCorrectPoint", label: "正解者なしのときのライアーのポイント" },
  { key: "inactivityThresholdMinutes", label: "botが反応するまでの非アクティブ時間(分)", min: 0, step: 0.1 },
  { key: "aiQuestionThreshold", label: "AI質問開始の閾値(%)", min: 0, max: 100, scale: 100 },
];

function ConfigForm({ onClose }: { onClose: () => void }) {
  const [config, setConfig] = useState<Config>();

  useEffect(() => {
    const fetchConfig = async () => {
      const { data } = await api.GET("/api/config");
      setConfig(data);
    };
    void fetchConfig();
  }, []);

  const [error, formAction, isPending] = useActionState(async (_: string | undefined, formData: FormData) => {
    const body = Object.fromEntries(FIELDS.map((f) => [f.key, Number(formData.get(f.key)) / (f.scale ?? 1)])) as unknown as Config;
    const { error } = await api.POST("/api/config", { body });
    if (error) {
      return errorMessage(error, "設定の更新に失敗しました");
    }
    onClose();
    return undefined;
  }, undefined);

  if (!config) {
    return <p>読み込み中…</p>;
  }

  return (
    <form action={formAction} className={form.form}>
      {FIELDS.map((f) => (
        <label key={f.key} className={form.field}>
          <span className={form.label}>{f.label}</span>
          <input
            className={form.input}
            type="number"
            name={f.key}
            min={f.min}
            max={f.max}
            step={f.step ?? 1}
            defaultValue={Math.round(config[f.key] * (f.scale ?? 1) * 100) / 100}
            required
          />
        </label>
      ))}
      {error && <p className={form.error}>{error}</p>}
      <div className={form.actions}>
        <Button onClick={onClose}>キャンセル</Button>
        <Button type="submit" variant="primary" disabled={isPending}>
          保存
        </Button>
      </div>
    </form>
  );
}

export default ConfigForm;
