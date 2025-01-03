import { useContext } from "react";
import { SceneContext } from "../App";

function WaitRoundStart() {
  const scene = useContext(SceneContext);

  return (
    <div>
      <h1>Wait Round Start</h1>
      <div>
        Waiting for players to be ready...
      </div>
      <pre>{JSON.stringify(scene, null, 2)}</pre>
    </div>
  );
}

export default WaitRoundStart;
