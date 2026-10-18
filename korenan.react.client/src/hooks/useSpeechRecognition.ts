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
  const recognitionRef = useRef<SpeechRecognition>(undefined);
  const onResultRef = useRef(onResult);
  const Recognition = getSpeechRecognition();

  useEffect(() => {
    onResultRef.current = onResult;
  });

  useEffect(() => () => recognitionRef.current?.abort(), []);

  const start = () => {
    if (!Recognition || listening) {
      return;
    }
    const recognition = new Recognition();
    recognition.lang = "ja-JP";
    recognition.interimResults = true;
    recognition.continuous = false;
    recognition.onresult = (ev) => {
      const result = ev.results[0];
      onResultRef.current(result[0].transcript, result.isFinal);
      if (result.isFinal) {
        recognition.stop();
      }
    };
    recognition.onend = () => setListening(false);
    recognition.onerror = () => setListening(false);
    recognitionRef.current = recognition;
    setListening(true);
    recognition.start();
  };

  return { supported: Recognition !== undefined, listening, start };
}
