import { useContext } from "react";
import { SceneContext } from "../App";

function RoundSummary() {
  const scene = useContext(SceneContext);

  return (
    <div>
      <h1>Answer Check</h1>
      <div>
        Checking answers...
      </div>
      <pre>{JSON.stringify(scene, null, 2)}</pre>
    </div>
  );
}

export default RoundSummary;
