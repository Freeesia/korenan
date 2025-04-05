import { useContext, useState } from "react";
import "./Debug.css";
import { AnswerResult, Player, QuestionResult } from "../models";
import { SceneContext, UserContext } from "../App";

function Debug() {
  const [scene, startFetchingScene] = useContext(SceneContext);
  const [user, setUser] = useContext(UserContext);
  const [registResponse, setRegistResponse] = useState<Player>();
  const [questionResponse, setQuestionResponse] = useState<QuestionResult>();
  const [answerResponse, setAnswerResponse] = useState<AnswerResult>();

  const [name, setName] = useState(user?.name || "");
  const [aikotoba, setAikotoba] = useState("");
  const [topic, setTopic] = useState("");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState("");
  const [guess, setGuess] = useState("");

  const registPlayer = async () => {
    const response = await fetch("/api/regist", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, aikotoba, topic }),
    });
    const data: Player = await response.json();
    setRegistResponse(data);
    setUser(data);
    await startFetchingScene();
  };

  const startRound = async () => {
    fetch("/api/start", { method: "POST" });
  };

  const nextRound = async () => {
    await fetch("/api/next", { method: "POST" });
  };

  const askQuestion = async () => {
    const response = await fetch("/api/question", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(question),
    });
    const data: QuestionResult = await response.json();
    setQuestionResponse(data);
  };

  const submitAnswer = async () => {
    const response = await fetch("/api/answer", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(answer),
    });
    const data: AnswerResult = await response.json();
    setAnswerResponse(data);
  };

  const guessLiar = async () => {
    await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(guess),
    });
  };

  const resetGame = async () => {
    await fetch("/api/reset", { method: "POST" });
  };

  const isFormValid = name && topic && aikotoba;

  return (
    <div className="container">
      <h1 className="title">Debug Page</h1>
      <div className="api-container">
        <div className="api-section disabled">
          <input type="text" id="username" placeholder="プレイヤー名" autoComplete="on" value={name} onChange={(e) => setName(e.target.value)} disabled={scene !== undefined} />
          <input type="text" id="aikotoba" placeholder="あいことば" autoComplete="on" value={aikotoba} onChange={(e) => setAikotoba(e.target.value)} disabled={scene !== undefined} />
          <input type="text" placeholder="Player Topic" value={topic} onChange={(e) => setTopic(e.target.value)} disabled={scene !== undefined} />
          <button onClick={registPlayer} disabled={!isFormValid}>
            Register Player
          </button>
        </div>
        <pre>{JSON.stringify(registResponse, null, 2)}</pre>
        <div className="api-section">
          <button onClick={startRound} disabled={scene?.scene !== "WaitRoundStart"}>
            Start Round
          </button>
          <button onClick={nextRound} disabled={scene?.scene !== "RoundSummary"}>
            Next Round
          </button>
          <button onClick={resetGame}>Reset Game</button>
        </div>
        <div className="api-section">
          <input type="text" placeholder="Question" value={question} onChange={(e) => setQuestion(e.target.value)} disabled={scene?.scene !== "QuestionAnswering"} />
          <button onClick={askQuestion} disabled={scene?.scene !== "QuestionAnswering"}>
            Ask Question
          </button>
          <pre>{JSON.stringify(questionResponse, null, 2)}</pre>
        </div>
        <div className="api-section">
          <input type="text" placeholder="Answer" value={answer} onChange={(e) => setAnswer(e.target.value)} disabled={scene?.scene !== "QuestionAnswering"} />
          <button onClick={submitAnswer} disabled={scene?.scene !== "QuestionAnswering"}>
            Submit Answer
          </button>
          <pre>{JSON.stringify(answerResponse, null, 2)}</pre>
        </div>
        <div className="api-section">
          <input type="text" placeholder="Guess Liar" value={guess} onChange={(e) => setGuess(e.target.value)} disabled={scene?.scene !== "LiarGuess"} />
          <button onClick={guessLiar} disabled={scene?.scene !== "LiarGuess"}>
            Guess Liar
          </button>
        </div>
      </div>
      <div className="scene-container">
        <h2>Current Scene Information</h2>
        <pre>{JSON.stringify(scene, null, 2)}</pre>
      </div>
    </div>
  );
}

export default Debug;
