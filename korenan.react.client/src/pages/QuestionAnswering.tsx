import { useContext, useState } from "react";
import { SceneContext } from "../App";

function QuestionAnswering() {
  const scene = useContext(SceneContext);
  const [question, setQuestion] = useState("");
  const [response, setResponse] = useState("");

  const askQuestion = async () => {
    const res = await fetch("/api/question", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(question),
    });
    const data = await res.json();
    setResponse(data);
  };

  return (
    <div>
      <h1>Question Answering</h1>
      <div>
        <input
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
        />
        <button onClick={askQuestion}>Ask Question</button>
      </div>
      <pre>{JSON.stringify(response, null, 2)}</pre>
      <pre>{JSON.stringify(scene, null, 2)}</pre>
    </div>
  );
}

export default QuestionAnswering;
