import { Info, X } from 'lucide-react';
import type { ReactNode } from 'react';

type Props = { title: string; children?: ReactNode; closeLabel: string; onClose: () => void; actions?: ReactNode };

/** 오른쪽 위 알림 (시안 11 "Page 2 looks scanned"). 왼쪽 노란 줄 = 주의. */
export function Toast({ title, children, closeLabel, onClose, actions }: Props) {
  return (
    <div role="status" className="absolute right-4 top-4 z-floating flex w-[360px] max-w-[calc(100%-32px)] gap-3 rounded-md border border-strong border-l-[3px] border-l-warning bg-surface p-3.5 shadow-md">
      <Info className="mt-0.5 h-icon w-icon flex-none text-warning" aria-hidden />
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="text-md font-semibold text-fg">{title}</span>
        {children && <div className="text-sm text-fg-muted">{children}</div>}
        {actions && <div className="flex gap-3 pt-1 text-sm">{actions}</div>}
      </div>
      <button type="button" aria-label={closeLabel} title={closeLabel} onClick={onClose} className="flex h-6 w-6 flex-none items-center justify-center rounded-sm text-fg-faint hover:bg-pressed hover:text-fg">
        <X className="h-3.5 w-3.5" aria-hidden />
      </button>
    </div>
  );
}
