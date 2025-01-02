import { useState, useEffect } from "react";
import "./Debug.css";
import { AnswerResponse, GameScene, Player, QuestionResponse } from "../models";

function Debug() {
  const [registResponse, setRegistResponse] = useState<Player>();
  const [scene, setScene] = useState<GameScene>();
  const [questionResponse, setQuestionResponse] = useState<QuestionResponse>();
  const [answerResponse, setAnswerResponse] = useState<AnswerResponse>();
  const [guessResponse, setGuessResponse] = useState(null);
  const [lastFetchTime, setLastFetchTime] = useState<Date>();

  const [playerName, setPlayerName] = useState("");
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
    const data = await response.json();
    setRegistResponse(data);
  };

  const fetchScene = async () => {
    const response = await fetch("/api/scene");
    const data = await response.json();
    setScene(data);
    setLastFetchTime(new Date());
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
    const data = await response.json();
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
    const data = await response.json();
    setAnswerResponse(data);
  };

  const guessLiar = async () => {
    const response = await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(guess),
    });
    const data = await response.json();
    setGuessResponse(data);
  };

  useEffect(() => {
    const interval = setInterval(fetchScene, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div>
      <h1>Debug Page</h1>
      <div className="api-section">
        <input
          type="text"
          placeholder="Player Name"
          value={playerName}
          onChange={(e) => setPlayerName(e.target.value)}
        />
        <input
          type="text"
          placeholder="Player Topic"
          value={playerTopic}
          onChange={(e) => setPlayerTopic(e.target.value)}
        />
        <button onClick={registPlayer}>Register Player</button>
        <pre>{JSON.stringify(registResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <pre>{JSON.stringify(scene, null, 2)}</pre>
        <div>Last fetch time: {lastFetchTime?.toLocaleTimeString()}</div>
      </div>
      <div className="api-section">
        <button onClick={startRound}>Start Round</button>
      </div>
      <div className="api-section">
        <button onClick={nextRound}>Next Round</button>
      </div>
      <div className="api-section">
        <input
          type="text"
          placeholder="Question"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
        />
        <button onClick={askQuestion}>Ask Question</button>
        <pre>{JSON.stringify(questionResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <input
          type="text"
          placeholder="Answer"
          value={answer}
          onChange={(e) => setAnswer(e.target.value)}
        />
        <button onClick={submitAnswer}>Submit Answer</button>
        <pre>{JSON.stringify(answerResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <input
          type="text"
          placeholder="Guess Liar"
          value={guess}
          onChange={(e) => setGuess(e.target.value)}
        />
        <button onClick={guessLiar}>Guess Liar</button>
        <pre>{JSON.stringify(guessResponse, null, 2)}</pre>
      </div>
    </div>
  );
}

export default Debug;
