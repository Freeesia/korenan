import { useState } from "react";
import "./Debug.css";
import { AnswerResponse, GameScene, Player, QuestionResponse } from "../models";

function Debug() {
  const [registResponse, setRegistResponse] = useState<Player>();
  const [scene, setScene] = useState<GameScene>();
  const [startResponse, setStartResponse] = useState(null);
  const [nextResponse, setNextResponse] = useState(null);
  const [questionResponse, setQuestionResponse] = useState<QuestionResponse>();
  const [answerResponse, setAnswerResponse] = useState<AnswerResponse>();
  const [guessResponse, setGuessResponse] = useState(null);

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
  };

  const startRound = async () => {
    const response = await fetch("/api/start", {
      method: "POST",
    });
    const data = await response.json();
    setStartResponse(data);
  };

  const nextRound = async () => {
    const response = await fetch("/api/next", {
      method: "POST",
    });
    const data = await response.json();
    setNextResponse(data);
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
        <button onClick={fetchScene}>Fetch Current Scene</button>
        <pre>{JSON.stringify(scene, null, 2)}</pre>
      </div>
      <div className="api-section">
        <button onClick={startRound}>Start Round</button>
        <pre>{JSON.stringify(startResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <button onClick={nextRound}>Next Round</button>
        <pre>{JSON.stringify(nextResponse, null, 2)}</pre>
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
