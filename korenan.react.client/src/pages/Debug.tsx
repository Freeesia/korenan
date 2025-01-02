import { useState } from "react";
import "./Debug.css";

function Debug() {
  const [weatherForecast, setWeatherForecast] = useState([]);
  const [registResponse, setRegistResponse] = useState(null);
  const [scene, setScene] = useState(null);
  const [startResponse, setStartResponse] = useState(null);
  const [nextResponse, setNextResponse] = useState(null);
  const [questionResponse, setQuestionResponse] = useState(null);
  const [answerResponse, setAnswerResponse] = useState(null);
  const [guessResponse, setGuessResponse] = useState(null);

  const fetchWeatherForecast = async () => {
    const response = await fetch("/api/weatherforecast");
    const data = await response.json();
    setWeatherForecast(data);
  };

  const registPlayer = async (name: string, topic: string) => {
    const response = await fetch("/api/regist", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, topic }),
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

  const askQuestion = async (question: string) => {
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

  const submitAnswer = async (answer: string) => {
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

  const guessLiar = async (target: string) => {
    const response = await fetch("/api/guess", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(target),
    });
    const data = await response.json();
    setGuessResponse(data);
  };

  return (
    <div>
      <h1>Debug Page</h1>
      <div className="api-section">
        <button onClick={fetchWeatherForecast}>Fetch Weather Forecast</button>
        <pre>{JSON.stringify(weatherForecast, null, 2)}</pre>
      </div>
      <div className="api-section">
        <button onClick={() => registPlayer("Player1", "Topic1")}>
          Register Player
        </button>
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
        <button onClick={() => askQuestion("Is it an animal?")}>
          Ask Question
        </button>
        <pre>{JSON.stringify(questionResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <button onClick={() => submitAnswer("Dog")}>Submit Answer</button>
        <pre>{JSON.stringify(answerResponse, null, 2)}</pre>
      </div>
      <div className="api-section">
        <button onClick={() => guessLiar("Player1")}>Guess Liar</button>
        <pre>{JSON.stringify(guessResponse, null, 2)}</pre>
      </div>
    </div>
  );
}

export default Debug;
