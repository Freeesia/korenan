import { flushSync } from "react-dom";
import type { NavigateFunction } from "react-router";

export function transitionTo(navigate: NavigateFunction, path: string) {
  if (!document.startViewTransition) {
    void navigate(path);
    return;
  }
  document.startViewTransition(() => flushSync(() => void navigate(path)));
}
