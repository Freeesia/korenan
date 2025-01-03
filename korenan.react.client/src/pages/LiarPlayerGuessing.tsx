import { useContext, useState } from "react";
import { SceneContext } from "../App";

function LiarPlayerGuessing() {
  const scene = useContext(SceneContext);
  const [guess, setGuess] = useState("");

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
    <div>
      <h1>Liar Player Guessing</h1>
      <div>
        <input
          type="text"
          value={guess}
          onChange={(e) => setGuess(e.target.value)}
        />
        <button onClick={guessLiar}>Guess Liar</button>
      </div>
      <pre>{JSON.stringify(scene, null, 2)}</pre>
    </div>
  );
}

export default LiarPlayerGuessing;
