import { useEffect, useRef, useState } from "react";

type SpeechRecognitionConstructor = new () => SpeechRecognition;

function getSpeechRecognition(): SpeechRecognitionConstructor | undefined {
  const w = window as Window & { SpeechRecognition?: SpeechRecognitionConstructor; webkitSpeechRecognition?: SpeechRecognitionConstructor };
  return w.SpeechRecognition ?? w.webkitSpeechRecognition;
}

/**
 * 音声入力を扱う。非対応ブラウザではsupportedがfalseになる。
 */
export function useSpeechRecognition(onResult: (text: string, isFinal: boolean) => void) {
  const [listening, setListening] = useState(false);
  const [error, setError] = useState<string>();
  const recognitionRef = useRef<SpeechRecognition>(undefined);
  const onResultRef = useRef(onResult);
  const Recognition = getSpeechRecognition();

  useEffect(() => {
    onResultRef.current = onResult;
  });

  useEffect(() => () => {
    const recognition = recognitionRef.current;
    if (!recognition) return;
    recognition.onresult = null;
    recognition.onend = null;
    recognition.onerror = null;
    recognition.abort();
  }, []);

  const start = () => {
    if (!Recognition || recognitionRef.current) {
      return;
    }
    const recognition = new Recognition();
    recognition.lang = "ja-JP";
    recognition.interimResults = true;
    recognition.continuous = false;
    recognition.onresult = (ev) => {
      if (recognitionRef.current !== recognition) return;
      const result = ev.results[0];
      onResultRef.current(result[0].transcript, result.isFinal);
      if (result.isFinal) {
        recognition.stop();
      }
    };
    recognition.onend = () => {
      if (recognitionRef.current !== recognition) return;
      recognitionRef.current = undefined;
      setListening(false);
    };
    recognition.onerror = (event) => {
      if (recognitionRef.current !== recognition) return;
      recognitionRef.current = undefined;
      setListening(false);
      if (event.error === "aborted") return;
      const messages: Partial<Record<SpeechRecognitionErrorCode, string>> = {
        "not-allowed": "音声入力が許可されていません。マイク・音声認識の設定を確認してください。iPhoneではSafariかキーボードの音声入力もお試しください。",
        "service-not-allowed": "このブラウザーでは音声認識サービスを利用できません。iPhoneではSafariかキーボードの音声入力をお試しください。",
        "audio-capture": "マイクを利用できません。端末のマイク設定や、他のアプリが使用中でないか確認してください。",
        "network": "音声認識サービスに接続できません。通信状態を確認してください。",
        "no-speech": "声を認識できませんでした。もう一度お試しください。",
      };
      setError(messages[event.error] ?? "音声入力を開始できませんでした。キーボードの音声入力も利用できます。");
    };
    recognitionRef.current = recognition;
    setError(undefined);
    setListening(true);
    try {
      recognition.start();
    } catch {
      recognitionRef.current = undefined;
      setListening(false);
      setError("音声入力を開始できませんでした。iPhoneではSafariかキーボードの音声入力をお試しください。");
    }
  };

  const stop = () => recognitionRef.current?.stop();
  const cancel = () => {
    const recognition = recognitionRef.current;
    recognitionRef.current = undefined;
    recognition?.abort();
    setListening(false);
  };

  return { supported: Recognition !== undefined, listening, error, start, stop, cancel };
}
