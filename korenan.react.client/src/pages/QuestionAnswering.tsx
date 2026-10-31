import { useEffect, useLayoutEffect, useRef, useState } from "react";
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
  const [error, setError] = useState<string>();
  const [isPending, setIsPending] = useState(false);
  const submitting = useRef(false);
  const disabled = isPending || remaining <= 0;

  const send = async (value: string) => {
    if (value.trim() === "" || disabled || submitting.current) {
      return;
    }
    submitting.current = true;
    setIsPending(true);
    setError(undefined);
    try {
      const result = await submit(value);
      setError(result);
      if (!result) setText("");
    } catch {
      setError("通信に失敗しました。接続を確認して再送してください。");
    } finally {
      submitting.current = false;
      setIsPending(false);
    }
  };

  const speech = useSpeechRecognition((transcript, isFinal) => {
    setText(transcript);
    if (isFinal) {
      void send(transcript);
    }
  });

  return (
    <form
      className={styles.row}
      onSubmit={(e) => {
        e.preventDefault();
        speech.cancel();
        void send(text);
      }}
    >
      <input
        className={styles.input}
        name={name}
        aria-label={label}
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
        <Button size="sm" variant="ghost" onClick={speech.listening ? speech.stop : speech.start} disabled={disabled} aria-label={speech.listening ? "音声入力を停止" : `${label}を音声入力`} aria-pressed={speech.listening}>
          {speech.listening ? "👂" : "🎙️"}
        </Button>
      )}
      <span className={styles.remaining}>残り{Math.max(remaining, 0)}回</span>
      {isPending && (
        <p className={styles.pending} role="status">
          AIが{label === "質問" ? "回答" : "判定"}中
          <span className={styles.dots} aria-hidden><span>●</span><span>●</span><span>●</span></span>
        </p>
      )}
      {!speech.supported && name === "question" && <p className={styles.hint}>このブラウザーは音声認識に対応していません。iPhoneではSafariかキーボードの音声入力をご利用ください。</p>}
      {(error || speech.error) && <p className={styles.error} role="alert">{error ?? speech.error}</p>}
    </form>
  );
}

function LiarNotice() {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;
  return (
    <button type="button" className={styles.liar} onClick={() => setDismissed(true)} aria-label="あなたはこのラウンドのライアーです。通知を閉じる">
      🤫 このラウンドのお題はあなたが登録したものです。ライアーだとバレないように振る舞おう！
      <span className={styles.dismissHint}>タップで閉じる ×</span>
    </button>
  );
}

function QuestionAnswering() {
  const { scene, user } = useGame();
  const playerName = usePlayerName();
  const [config, setConfig] = useState<Config>();
  const [playNotifySound] = useSound(notificationSound);
  const prevLengthRef = useRef(0);
  const timelineRef = useRef<HTMLUListElement>(null);
  const followLatest = useRef(true);
  usePageTitle("質問タイム");

  const info = sceneInfoOf(scene, "QuestionAnswering");
  const histories = info?.histories ?? [];

  useEffect(() => {
    const fetchConfig = async () => {
      const { data } = await api.GET("/api/config");
      setConfig(data);
    };
    void fetchConfig();
  }, []);

  // 投稿前に最下部にいたときだけ追従する。入力欄とは別のスクロール領域にする。
  useLayoutEffect(() => {
    const timeline = timelineRef.current;
    if (timeline && followLatest.current) timeline.scrollTop = timeline.scrollHeight;
  }, [histories.length]);

  useEffect(() => {
    const timeline = timelineRef.current;
    if (!timeline) return;
    const observer = new ResizeObserver(() => {
      if (followLatest.current) timeline.scrollTop = timeline.scrollHeight;
    });
    observer.observe(timeline);
    return () => observer.disconnect();
  }, []);

  // 履歴が増えたら振動・効果音で知らせる
  useEffect(() => {
    if (prevLengthRef.current > 0 && histories.length > prevLengthRef.current) {
      navigator.vibrate?.(100);
      playNotifySound();
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
        {info?.isLiar && <LiarNotice key={`${scene?.id}:${scene?.round}`} />}
      </Card>
      <ul
        ref={timelineRef}
        className={styles.timeline}
        tabIndex={0}
        aria-label="質問と解答の履歴"
        onScroll={(event) => {
          const timeline = event.currentTarget;
          followLatest.current = timeline.scrollHeight - timeline.scrollTop - timeline.clientHeight <= 4;
        }}
      >
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
      <div className={styles.inputArea}>
        <InputRow name="question" placeholder="質問（例: 生き物ですか？）" label="質問" remaining={(config?.questionLimit ?? 0) - myQuestions} submit={askQuestion} />
        <InputRow name="answer" placeholder="解答" label="解答" remaining={(config?.answerLimit ?? 0) - myAnswers} submit={submitAnswer} />
      </div>
    </div>
  );
}

export default QuestionAnswering;
