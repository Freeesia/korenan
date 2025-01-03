import { useContext, useState } from "react";
import { UserContext } from "../App";

function NameTopicRegistration() {
  const [user, setUser] = useContext(UserContext);
  const [name, setName] = useState(user?.name || "");
  const [topic, setTopic] = useState("");

  const register = async () => {
    const response = await fetch("/api/regist", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, topic }),
    });
    const data = await response.json();
    setUser(data);
  };

  return (
    <div>
      <h1>Name and Topic Registration</h1>
      <div>
        <label>
          Name:
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
      </div>
      <div>
        <label>
          Topic:
          <input
            type="text"
            value={topic}
            onChange={(e) => setTopic(e.target.value)}
          />
        </label>
      </div>
      <button onClick={register}>Register</button>
    </div>
  );
}

export default NameTopicRegistration;
