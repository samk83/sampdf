import type { ReactNode } from 'react';

type Props = { label: string; onClick: () => void; children: ReactNode; pressed?: boolean; disabled?: boolean };

/** 아이콘만 있는 작은 버튼 (설계서 4-2). 설명은 툴팁과 aria-label로. */
export function IconButton({ label, onClick, children, pressed, disabled }: Props) {
  return (
    <button type="button" aria-label={label} title={label} aria-pressed={pressed} disabled={disabled} onClick={onClick}
      className={`flex h-touch w-touch items-center justify-center rounded-sm transition-colors duration-fast ease-standard disabled:opacity-40 ${
        pressed ? 'bg-pressed text-accent' : 'text-fg-secondary hover:bg-pressed hover:text-fg'
      }`}>
      {children}
    </button>
  );
}
