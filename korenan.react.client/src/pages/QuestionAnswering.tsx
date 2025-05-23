import { useContext, useEffect, useState, useRef, useCallback } from "react";
import { SceneContext, UserContext, TitleContext } from "../App";
import { QuestionAnsweringSceneInfo, QuestionResultType, AnswerResultType, IPlayerResult, AnswerResult, QuestionResult, Config } from "../models";
import useSound from "use-sound";
import notificationSound from "../assets/決定ボタンを押す52.mp3";

function QuestionAnswering() {
  const [scene] = useContext(SceneContext);
  const [user] = useContext(UserContext);
  const [, setPageTitle] = useContext(TitleContext);
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState("");
  const [qResult, setQResult] = useState<QuestionResultType>();
  const [aResult, setAResult] = useState<AnswerResultType>();
  const [isWaiting, setIsWaiting] = useState(false);
  const [config, setConfig] = useState<Config>();
  const prevHistoriesLengthRef = useRef<number>(0);
  const [playNotifySound] = useSound(notificationSound);

  const sceneInfo = useCallback(() => {
    if (scene?.scene === "QuestionAnswering") {
      return scene?.info as QuestionAnsweringSceneInfo;
    }
    return undefined;
  }, [scene]);

  // 履歴が変わったら振動させて音を鳴らす
  useEffect(() => {
    const histories = sceneInfo()?.histories || [];
    if (prevHistoriesLengthRef.current > 0 && histories.length > prevHistoriesLengthRef.current) {
      // スマホを振動させる（振動パターン: 100ms振動）
      if ("vibrate" in navigator) {
        navigator.vibrate(100);
      }

      // 効果音を再生
      playNotifySound();
    }
    prevHistoriesLengthRef.current = histories.length;
  }, [scene, sceneInfo, playNotifySound]);

  useEffect(() => {
    fetch("/api/scene", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify("QuestionAnswering"),
    });
    fetchConfig();

    setPageTitle("質問タイム");
  }, [setPageTitle]);

  const fetchConfig = async () => {
    const res = await fetch("/api/config");
    const data: Config = await res.json();
    setConfig(data);
  };

  const askQuestion = async (q?: string) => {
    const final = q ?? question;
    if (final === "" || isWaiting) {
      return;
    }
    setIsWaiting(true);
    const res = await fetch("/api/question", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(final),
    });
    const data: QuestionResultType = await res.json();
    setQResult(data);
    setQuestion("");
    setIsWaiting(false);
  };

  const submitAnswer = async (a?: string) => {
    const final = a ?? answer;
    if (final === "" || isWaiting) {
      return;
    }
    setIsWaiting(true);
    const res = await fetch("/api/answer", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(final),
    });
    const data: AnswerResultType = await res.json();
    setAResult(data);
    setAnswer("");
    setIsWaiting(false);
  };

  const qRecog = new (window.SpeechRecognition || window.webkitSpeechRecognition)();
  qRecog.lang = "ja-JP";
  qRecog.interimResults = true;
  qRecog.continuous = false;
  qRecog.onresult = (ev) => {
    const result = ev.results[0];
    const speechResult = result[0].transcript;
    setQuestion(speechResult);
    if (result.isFinal) {
      qRecog.stop();
      askQuestion(speechResult);
    }
  };

  const aRecog = new (window.SpeechRecognition || window.webkitSpeechRecognition)();
  aRecog.lang = "ja-JP";
  aRecog.interimResults = true;
  aRecog.continuous = false;
  aRecog.onresult = (ev) => {
    const result = ev.results[0];
    const speechResult = result[0].transcript;
    setAnswer(speechResult);
    if (result.isFinal) {
      aRecog.stop();
      submitAnswer(speechResult);
    }
  };

  const getPlayerName = (id: string) => scene?.players.find((p) => p.id === id)?.name || id;
  const getAnswerResult = (result: IPlayerResult) => result as AnswerResult;
  const getQuestionResult = (result: IPlayerResult) => result as QuestionResult;
  const remainingQuestions = (config?.questionLimit ?? 0) - (sceneInfo()?.histories.filter((h) => h.type === "Question" && h.player === user?.id).length ?? 0);
  const remainingAnswers = (config?.answerLimit ?? 0) - (sceneInfo()?.histories.filter((h) => h.type === "Answer" && h.player === user?.id).length ?? 0);

  return (
    <div>
      <p>
        AIに「Yes」か「No」で答えられる質問を投げかけてみよう！
        <br />
        他のプレイヤーが投げた質問も確認できるよ。
        <br />
        お題が分かったら、「解答」ボタンで答えてみてね！
      </p>
      <h3>テーマ: 「{scene?.theme}」</h3>
      <div>
        <ul>
          {sceneInfo()?.histories.map((history, index) => (
            <li key={index}>
              {getPlayerName(history.player)}:{" "}
              {history.type === "Question" ? (
                <span>
                  {getQuestionResult(history).question} - {getQuestionResult(history).result}
                </span>
              ) : (
                <span>
                  {getAnswerResult(history).answer} - {getAnswerResult(history).result}
                </span>
              )}
            </li>
          ))}
        </ul>
      </div>
      <div>
        <input
          type="text"
          placeholder="質問"
          value={question}
          onKeyDown={(e) => e.key === "Enter" && !e.nativeEvent.isComposing && askQuestion()}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={isWaiting || remainingQuestions <= 0}
        />
        <button onClick={() => askQuestion()} disabled={isWaiting || remainingQuestions <= 0 || question === ""}>
          質問
        </button>
        <button onClick={() => qRecog.start()} disabled={isWaiting || remainingQuestions <= 0}>
          🎙️
        </button>
        <pre>{qResult}</pre>
        <p>残りの質問回数: {remainingQuestions}</p>
      </div>
      <div>
        <input type="text" placeholder="解答" value={answer} onKeyDown={(e) => e.key === "Enter" && !e.nativeEvent.isComposing && submitAnswer()} onChange={(e) => setAnswer(e.target.value)} disabled={isWaiting || remainingAnswers <= 0} />
        <button onClick={() => submitAnswer()} disabled={isWaiting || remainingAnswers <= 0 || answer === ""}>
          解答
        </button>
        <button onClick={() => aRecog.start()} disabled={isWaiting || remainingAnswers <= 0}>
          🎙️
        </button>
        <pre>{aResult}</pre>
        <p>残りの解答回数: {remainingAnswers}</p>
      </div>
    </div>
  );
}

export default QuestionAnswering;
