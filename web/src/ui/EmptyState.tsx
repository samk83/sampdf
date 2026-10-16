import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';

type Props = { icon: LucideIcon; title?: string; children?: ReactNode; actions?: ReactNode; tone?: 'neutral' | 'danger' | 'success' };

/**
 * 빈 상태·오류 상태 (설계서 4-2, 시안 11): 원인 + 해결 버튼 (design.md 5장 9번).
 * title이 없으면 한 줄 안내 (변경 표의 "No changes yet").
 */
export function EmptyState({ icon: Icon, title, children, actions, tone = 'neutral' }: Props) {
  if (!title) {
    return (
      <div className="flex h-full items-center justify-center gap-2.5 p-4 text-sm text-fg-muted">
        <Icon className="h-icon w-icon flex-none" aria-hidden />
        <span>{children}</span>
      </div>
    );
  }
  const ring = { neutral: 'bg-pressed text-fg-secondary', danger: 'bg-pressed text-danger', success: 'bg-pressed text-success' }[tone];
  return (
    <div role={tone === 'danger' ? 'alert' : 'status'}
      className={`flex w-full max-w-[426px] flex-col gap-3 rounded-lg border bg-surface p-6 shadow-md ${tone === 'danger' ? 'border-danger' : 'border-strong'}`}>
      <span className={`flex h-11 w-11 items-center justify-center rounded-full ${ring}`}><Icon className="h-[22px] w-[22px]" aria-hidden /></span>
      <h2 className="m-0 text-lg font-bold text-fg">{title}</h2>
      {children && <div className="text-md leading-normal text-fg-secondary">{children}</div>}
      {actions && <div className="flex flex-wrap gap-2 pt-1">{actions}</div>}
    </div>
  );
}
