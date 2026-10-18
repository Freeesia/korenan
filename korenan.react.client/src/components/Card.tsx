import type { HTMLAttributes, ReactNode } from "react";
import styles from "./Card.module.css";

interface CardProps extends Omit<HTMLAttributes<HTMLElement>, "title"> {
  title?: ReactNode;
  variant?: "solid" | "translucent";
}

export function Card({ title, variant = "solid", className, children, ...props }: CardProps) {
  return (
    <section className={[styles.card, styles[variant], className].filter(Boolean).join(" ")} {...props}>
      {title && <h2 className={styles.title}>{title}</h2>}
      {children}
    </section>
  );
}
