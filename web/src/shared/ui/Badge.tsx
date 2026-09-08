import type { HTMLAttributes, PropsWithChildren } from 'react';
import './Badge.css';

type BadgeTone = 'ok' | 'warn' | 'risk' | 'neutral' | 'brand';

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
}

export function Badge({ tone = 'neutral', className, children, ...props }: PropsWithChildren<BadgeProps>) {
  const classes = ['ui-badge', `ui-badge--${tone}`, className].filter(Boolean).join(' ');
  return (
    <span className={classes} {...props}>
      {children}
    </span>
  );
}
