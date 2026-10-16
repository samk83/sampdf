import type { ReactNode } from 'react';

type Props = { left?: ReactNode; right?: ReactNode };

/**
 * 옛 비교·PNG 화면의 위쪽 도구 막대 (앱 이름·기능 탭은 새 제목줄로 옮김).
 * U3·U4에서 리본·패널로 옮기면 지운다.
 */
export function AppHeader({ left, right }: Props) {
  return (
    <header className="flex min-h-12 flex-wrap items-center justify-between gap-2.5 border-b border-legacy-line bg-white px-4 py-1.5">
      <div className="flex flex-wrap items-center gap-3.5">{left}</div>
      <div className="flex flex-wrap items-center gap-2">{right}</div>
    </header>
  );
}

/** 아이콘만 있는 도구 버튼 (설명은 aria-label과 마우스를 올렸을 때 나오는 title). */
export function IconButton({ label, onClick, children, pressed, disabled }: {
  label: string; onClick: () => void; children: ReactNode; pressed?: boolean; disabled?: boolean;
}) {
  return (
    <button type="button" aria-label={label} title={label} aria-pressed={pressed} disabled={disabled} onClick={onClick}
      className={`flex h-9 w-9 items-center justify-center rounded-[8px] disabled:opacity-40 ${
        pressed ? 'bg-legacy-accent-soft text-legacy-accent' : 'text-[#3f4552] hover:bg-[#eef0f3]'
      }`}>
      {children}
    </button>
  );
}
