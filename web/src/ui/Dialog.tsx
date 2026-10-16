import { useEffect, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { X } from 'lucide-react';
import { IconButton } from './IconButton';

type Props = { title: string; closeLabel: string; onClose: () => void; children: ReactNode; footer?: ReactNode; width?: number };

/**
 * 대화상자 (설계서 4-2, 시안 06·10 모양): 제목 48px · 본문 · 아래 줄 60px.
 * 열리면 첫 입력칸(또는 첫 버튼)에 커서, Esc로 닫힘, Tab은 상자 안에서만 돈다, 닫으면 커서를 원래 자리로.
 */
export function Dialog({ title, closeLabel, onClose, children, footer, width = 560 }: Props) {
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    box.current?.querySelector<HTMLElement>('input, button:not([aria-label])')?.focus();
    return () => before?.focus?.();
  }, []);

  const onKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      e.stopPropagation();
      onClose();
    }
    if (e.key === 'Tab' && box.current) {
      const all = Array.from(box.current.querySelectorAll<HTMLElement>('input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'));
      if (!all.length) return;
      const first = all[0];
      const last = all[all.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    }
  };

  return (
    <div className="fixed inset-0 z-modal" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div ref={box} role="dialog" aria-modal="true" aria-label={title} onKeyDown={onKeyDown} style={{ width }}
        className="absolute left-1/2 top-[110px] flex max-h-[calc(100vh-140px)] max-w-[calc(100vw-32px)] -translate-x-1/2 flex-col overflow-hidden rounded-lg border border-strong bg-surface font-sans text-md text-fg shadow-lg">
        <div className="flex h-12 flex-none items-center border-b border-subtle pl-[18px] pr-2">
          <h2 className="m-0 text-lg font-bold">{title}</h2>
          <span className="ml-auto"><IconButton label={closeLabel} onClick={onClose}><X className="h-icon w-icon" aria-hidden /></IconButton></span>
        </div>
        <div className="min-h-0 flex-1 overflow-auto">{children}</div>
        {footer && <div className="flex h-[60px] flex-none items-center gap-2 border-t border-subtle bg-sunken px-[18px]">{footer}</div>}
      </div>
    </div>
  );
}
