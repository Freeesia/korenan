import { useEffect, useState } from "react";

function TopicSelecting() {
  const [dots, setDots] = useState("");

  useEffect(() => {
    const interval = setInterval(() => {
      setDots((prev) => (prev.length < 3 ? prev + "." : ""));
    }, 500);

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="container">
      <h1 className="title">お題抽選中{dots}</h1>
    </div>
  );
}

export default TopicSelecting;
