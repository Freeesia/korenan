import type { ButtonHTMLAttributes } from "react";
import styles from "./Button.module.css";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  size?: "sm" | "md" | "lg";
  block?: boolean;
}

export function Button({ variant = "secondary", size = "md", block = false, className, type = "button", ...props }: ButtonProps) {
  const classes = [styles.button, styles[variant], styles[size], block ? styles.block : undefined, className].filter(Boolean).join(" ");
  return <button type={type} className={classes} {...props} />;
}
