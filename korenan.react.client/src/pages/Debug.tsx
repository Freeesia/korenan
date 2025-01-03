import { useContext, useState } from "react";
import "./Debug.css";
import { AnswerResponse, Player, QuestionResponse } from "../models";
import { SceneContext, UserContext } from "../App";

function Debug() {
  const scene = useContext(SceneContext);
  const user = useContext(UserContext);
  const [registResponse, setRegistResponse] = useState<Player>();
  const [questionResponse, setQuestionResponse] = useState<QuestionResponse>();
  const [answerResponse, setAnswerResponse] = useState<AnswerResponse>();

  const [playerName, setPlayerName] = useState(user?.name || "");
  const [playerTopic, setPlayerTopic] = useState("");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState("");
  const [guess, setGuess] = useState("");

  const registPlayer = async () => {
    const response = await fetch("/api/regist", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name: playerName, topic: playerTopic }),
    });
    const data: Player = await response.json();
    setRegistResponse(data);
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
    const data: QuestionResponse = await response.json();
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
    const data: AnswerResponse = await response.json();
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

  return (
    <div className="container">
      <h1 className="title">Debug Page</h1>
      <div className="api-container">
        <div className="api-section disabled">
          <input
            type="text"
            placeholder="Player Name"
            value={playerName}
            onChange={(e) => setPlayerName(e.target.value)}
            disabled={scene?.scene !== "WaitRoundStart"}
          />
          <input
            type="text"
            placeholder="Player Topic"
            value={playerTopic}
            onChange={(e) => setPlayerTopic(e.target.value)}
            disabled={scene?.scene !== "WaitRoundStart"}
          />
          <button
            onClick={registPlayer}
            disabled={scene?.scene !== "WaitRoundStart"}
          >
            Register Player
          </button>
          <pre>{JSON.stringify(registResponse, null, 2)}</pre>
        </div>
        <div className="api-section">
          <button
            onClick={startRound}
            disabled={scene?.scene !== "WaitRoundStart"}
          >
            Start Round
          </button>
          <button
            onClick={nextRound}
            disabled={scene?.scene !== "RoundSummary"}
          >
            Next Round
          </button>
        </div>
        <div className="api-section">
          <input
            type="text"
            placeholder="Question"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            disabled={scene?.scene !== "QuestionAnswering"}
          />
          <button
            onClick={askQuestion}
            disabled={scene?.scene !== "QuestionAnswering"}
          >
            Ask Question
          </button>
          <pre>{JSON.stringify(questionResponse, null, 2)}</pre>
        </div>
        <div className="api-section">
          <input
            type="text"
            placeholder="Answer"
            value={answer}
            onChange={(e) => setAnswer(e.target.value)}
            disabled={scene?.scene !== "QuestionAnswering"}
          />
          <button
            onClick={submitAnswer}
            disabled={scene?.scene !== "QuestionAnswering"}
          >
            Submit Answer
          </button>
          <pre>{JSON.stringify(answerResponse, null, 2)}</pre>
        </div>
        <div className="api-section">
          <input
            type="text"
            placeholder="Guess Liar"
            value={guess}
            onChange={(e) => setGuess(e.target.value)}
            disabled={scene?.scene !== "LiarPlayerGuessing"}
          />
          <button
            onClick={guessLiar}
            disabled={scene?.scene !== "LiarPlayerGuessing"}
          >
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
