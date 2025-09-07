import { useState, useEffect } from "react";
import { Config } from "../models";

function ConfigPage({ onClose }: { onClose: () => void }) {
  const [config, setConfig] = useState<Config>({ questionLimit: 0, answerLimit: 0, correctPoint: 0, liarPoint: 0, noCorrectPoint: 0, inactivityThresholdMinutes: 0, aiQuestionThreshold: 0 });

  useEffect(() => {
    fetchConfig();
  }, []);

  const fetchConfig = async () => {
    const response = await fetch("/api/config");
    const data: Config = await response.json();
    setConfig(data);
  };

  const updateConfig = async () => {
    const response = await fetch("/api/config", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(config),
    });
    if (response.ok) {
      onClose();
    }
  };

  return (
    <div>
      <h1>Config</h1>
      <div>
        <label>
          Question Limit:
          <input
            type="number"
            disabled={!config}
            value={config?.questionLimit}
            onChange={(e) =>
              setConfig({ ...config!, questionLimit: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          Answer Limit:
          <input
            type="number"
            disabled={!config}
            value={config?.answerLimit}
            onChange={(e) =>
              setConfig({ ...config!, answerLimit: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          Correct Point:
          <input
            type="number"
            disabled={!config}
            value={config?.correctPoint}
            onChange={(e) =>
              setConfig({ ...config!, correctPoint: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          Liar Point:
          <input
            type="number"
            disabled={!config}
            value={config?.liarPoint}
            onChange={(e) =>
              setConfig({ ...config!, liarPoint: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          No Correct Point:
          <input
            type="number"
            disabled={!config}
            value={config?.noCorrectPoint}
            onChange={(e) =>
              setConfig({ ...config!, noCorrectPoint: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          botが反応するまでの非アクティブ時間(分):
          <input
            type="number"
            disabled={!config}
            value={config?.inactivityThresholdMinutes}
            onChange={(e) =>
              setConfig({ ...config!, inactivityThresholdMinutes: Number(e.target.value) })
            }
          />
        </label>
      </div>
      <div>
        <label>
          AI質問開始の閾値(%):
          <input
            type="number"
            disabled={!config}
            min="0"
            max="100"
            step="1"
            value={config ? Math.round(config.aiQuestionThreshold * 100) : 0}
            onChange={(e) =>
              setConfig({ ...config!, aiQuestionThreshold: Number(e.target.value) / 100 })
            }
          />
        </label>
      </div>
      <button onClick={updateConfig}>Update Config</button>
      <button onClick={onClose}>閉じる</button>
    </div>
  );
}

export default ConfigPage;
