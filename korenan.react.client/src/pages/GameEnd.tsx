import { useContext } from "react";
import { SceneContext } from "../App";

function GameEnd() {
  const scene = useContext(SceneContext);

  return (
    <div>
      <h1>Final Result</h1>
      <div>
        Final results are in!
      </div>
      <pre>{JSON.stringify(scene, null, 2)}</pre>
    </div>
  );
}

export default GameEnd;
