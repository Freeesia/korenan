import { flushSync } from "react-dom";
import type { NavigateFunction } from "react-router";

export function transitionTo(navigate: NavigateFunction, path: string) {
  if (!document.startViewTransition) {
    void navigate(path);
    return;
  }
  const transition = document.startViewTransition(() => flushSync(() => void navigate(path)));
  // 次のシーンでアニメーションが中断されても、画面更新は完了する。
  // readyの拒否を未処理にせず、更新処理の例外はupdateCallbackDone・finishedに残す。
  void transition.ready.catch(() => {});
}
