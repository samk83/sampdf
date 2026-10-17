import type { ReactNode } from 'react';

type Tone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger' | 'outline';
const TONE: Record<Tone, string> = {
  neutral: 'bg-pressed text-fg-secondary',
  accent: 'bg-accent-subtle text-accent',
  success: 'bg-pressed text-success',
  warning: 'bg-pressed text-warning',
  danger: 'bg-pressed text-danger',
  /** 명령 이름 표시 (시작 화면 카드의 `CMP`). */
  outline: 'border border-strong font-mono text-fg-muted',
};

/** 작은 상태 표시 ("4 changes", "Reviewed", "New page"). 색은 상태색 토큰. */
export function Badge({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`inline-flex h-[18px] items-center whitespace-nowrap rounded-sm px-1.5 text-2xs font-medium ${TONE[tone]}`}>{children}</span>;
}
