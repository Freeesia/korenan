import { useEffect, useRef, type ReactNode } from "react";
import styles from "./Dialog.module.css";

interface DialogProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}

export function Dialog({ open, title, onClose, children }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) {
      return;
    }
    if (open && !dialog.open) {
      dialog.showModal();
    }
    if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog ref={ref} className={styles.dialog} onClose={onClose} onCancel={onClose}>
      <header className={styles.header}>
        <h2 className={styles.title}>{title}</h2>
        <button type="button" className={styles.close} onClick={onClose} aria-label="閉じる">
          ✕
        </button>
      </header>
      <div className={styles.body}>{open && children}</div>
    </dialog>
  );
}
