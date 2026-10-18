import { startTransition, useActionState, useEffect, useRef, useState } from "react";
import useSound from "use-sound";
import { api, errorMessage, sceneInfoOf, type Config } from "../api/client";
import notificationSound from "../assets/決定ボタンを押す52.mp3";
import { Button } from "../components/Button";
import { Card } from "../components/Card";
import { ChatBubble } from "../components/ChatBubble";
import { ResultBadge } from "../components/ResultBadge";
import { useGame, usePageTitle, usePlayerName } from "../context/game";
import { useSpeechRecognition } from "../hooks/useSpeechRecognition";
import styles from "./QuestionAnswering.module.css";

interface InputRowProps {
  name: string;
  placeholder: string;
  label: string;
  remaining: number;
  submit: (text: string) => Promise<string | undefined>;
}

function InputRow({ name, placeholder, label, remaining, submit }: InputRowProps) {
  const [text, setText] = useState("");
  const [error, dispatch, isPending] = useActionState(async (_: string | undefined, value: string) => {
    const result = await submit(value);
    if (!result) {
      setText("");
    }
    return result;
  }, undefined);
  const disabled = isPending || remaining <= 0;

  const send = (value: string) => {
    if (value.trim() === "" || disabled) {
      return;
    }
    startTransition(() => dispatch(value));
  };

  const speech = useSpeechRecognition((transcript, isFinal) => {
    setText(transcript);
    if (isFinal) {
      send(transcript);
    }
  });

  return (
    <form
      className={styles.row}
      onSubmit={(e) => {
        e.preventDefault();
        send(text);
      }}
    >
      <input
        className={styles.input}
        name={name}
        type="text"
        placeholder={isPending ? "AIが考え中…" : placeholder}
        autoComplete="off"
        value={text}
        onChange={(e) => setText(e.target.value)}
        disabled={disabled}
      />
      <Button type="submit" size="sm" variant="primary" disabled={disabled || text.trim() === ""}>
        {label}
      </Button>
      {speech.supported && (
        <Button size="sm" variant="ghost" onClick={speech.start} disabled={disabled || speech.listening} aria-label="音声入力">
          {speech.listening ? "👂" : "🎙️"}
        </Button>
      )}
      <span className={styles.remaining}>残り{Math.max(remaining, 0)}回</span>
      {error && <p className={styles.error}>{error}</p>}
    </form>
  );
}

function QuestionAnswering() {
  const { scene, user } = useGame();
  const playerName = usePlayerName();
  const [config, setConfig] = useState<Config>();
  const [playNotifySound] = useSound(notificationSound);
  const prevLengthRef = useRef(0);
  const endRef = useRef<HTMLDivElement>(null);
  usePageTitle("質問タイム");

  const histories = sceneInfoOf(scene, "QuestionAnswering")?.histories ?? [];

  useEffect(() => {
    const fetchConfig = async () => {
      const { data } = await api.GET("/api/config");
      setConfig(data);
    };
    void fetchConfig();
  }, []);

  // 履歴が増えたら振動・効果音・スクロールで知らせる
  useEffect(() => {
    if (prevLengthRef.current > 0 && histories.length > prevLengthRef.current) {
      navigator.vibrate?.(100);
      playNotifySound();
      endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
    }
    prevLengthRef.current = histories.length;
  }, [histories.length, playNotifySound]);

  const myQuestions = histories.filter((h) => h.type === "Question" && h.player === user?.id).length;
  const myAnswers = histories.filter((h) => h.type === "Answer" && h.player === user?.id).length;

  const askQuestion = async (input: string) => {
    const { error } = await api.POST("/api/question", { body: input });
    return error ? errorMessage(error, "質問に失敗しました") : undefined;
  };

  const submitAnswer = async (input: string) => {
    const { error } = await api.POST("/api/answer", { body: input });
    return error ? errorMessage(error, "解答に失敗しました") : undefined;
  };

  return (
    <div className={styles.page}>
      {scene && <div className={styles.backdrop} style={{ backgroundImage: `url("/api/image/bot/${scene.id}")` }} aria-hidden />}
      <Card variant="translucent" className={styles.header}>
        <p>
          AIに「はい」か「いいえ」で答えられる質問を投げかけてみよう！
          <br />
          お題が分かったら、「解答」で答えてみてね！
        </p>
        <h3>テーマ: 「{scene?.theme}」</h3>
      </Card>
      <ul className={styles.timeline}>
        {histories.map((history, index) => (
          <ChatBubble
            key={index}
            playerId={history.player}
            playerName={playerName(history.player)}
            own={history.player === user?.id}
            kind={history.type === "Answer" ? "answer" : "question"}
            badge={<ResultBadge result={history.result} variant="floating" />}
          >
            {history.type === "Answer" ? history.answer : history.question}
          </ChatBubble>
        ))}
      </ul>
      <div ref={endRef} />
      <div className={styles.inputArea}>
        <InputRow name="question" placeholder="質問（例: 生き物ですか？）" label="質問" remaining={(config?.questionLimit ?? 0) - myQuestions} submit={askQuestion} />
        <InputRow name="answer" placeholder="解答" label="解答" remaining={(config?.answerLimit ?? 0) - myAnswers} submit={submitAnswer} />
      </div>
    </div>
  );
}

export default QuestionAnswering;
