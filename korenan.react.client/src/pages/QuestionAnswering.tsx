import { useContext, useEffect, useState } from "react";
import { SceneContext } from "../App";
import {
  QuestionAnsweringSceneInfo,
  QuestionResponse,
  AnswerResponse,
} from "../models";

function QuestionAnswering() {
  const scene = useContext(SceneContext);
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState("");
  const [qResponse, setQResponse] = useState<QuestionResponse>();
  const [aResponse, setAResponse] = useState<AnswerResponse>();
  const [isWaiting, setIsWaiting] = useState(false);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("QuestionAnswering"),
    });
  }, []);

  const askQuestion = async () => {
    setIsWaiting(true);
    const res = await fetch("/api/question", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(question),
    });
    const data: QuestionResponse = await res.json();
    setQResponse(data);
    setIsWaiting(false);
  };

  const submitAnswer = async () => {
    setIsWaiting(true);
    const res = await fetch("/api/answer", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(answer),
    });
    const data: AnswerResponse = await res.json();
    setAResponse(data);
    setIsWaiting(false);
  };

  const sceneInfo = () => {
    if (scene?.scene === "QuestionAnswering") {
      return scene?.info as QuestionAnsweringSceneInfo;
    }
    return undefined;
  };

  const getPlayerName = (id: string) => {
    return scene?.players.find((p) => p.id === id)?.name || id;
  };

  return (
    <div>
      <h1>質問タイム</h1>
      <div>
        <ul>
          {sceneInfo()?.histories.map((history, index) => (
            <li key={index}>
              {getPlayerName(history.player)}:{" "}
              {history.question || history.answer} - {history.result}
            </li>
          ))}
        </ul>
      </div>
      <div>
        <input
          type="text"
          placeholder="質問"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={isWaiting}
        />
        <button onClick={askQuestion} disabled={isWaiting}>
          質問
        </button>
        <pre>{JSON.stringify(qResponse, null, 2)}</pre>
      </div>
      <div>
        <input
          type="text"
          placeholder="解答"
          value={answer}
          onChange={(e) => setAnswer(e.target.value)}
          disabled={isWaiting}
        />
        <button onClick={submitAnswer} disabled={isWaiting}>
          解答
        </button>
        <pre>{JSON.stringify(aResponse, null, 2)}</pre>
      </div>
    </div>
  );
}

export default QuestionAnswering;
