import { useEffect, useState, useContext } from "react";
import { TitleContext } from "../App";

function TopicSelecting() {
  const [dots, setDots] = useState("");
  const [, setPageTitle] = useContext(TitleContext);

  useEffect(() => {
    const interval = setInterval(() => {
      setDots((prev) => (prev.length < 3 ? prev + "." : ""));
    }, 500);

    setPageTitle("お題抽選中");
    return () => clearInterval(interval);
  }, [setPageTitle]);

  return (
    <div className="container">
      <h1 className="title">お題抽選中{dots}</h1>
    </div>
  );
}

export default TopicSelecting;
