import { useState } from 'react';
import { Bookmark, Layers } from 'lucide-react';
import { pageImageUrl } from '../api';
import { useT, type MessageKey } from '../i18n';
import { Badge } from '../ui/Badge';
import { EmptyState } from '../ui/EmptyState';
import { pageBadge, useCompare, useCompareTab } from './state';

type PanelTab = 'pages' | 'bookmarks' | 'layers';
const TABS: PanelTab[] = ['pages', 'bookmarks', 'layers'];

/** 왼쪽 패널 (시안 02·04): Pages(페이지 그림 + 변경 수) · Bookmarks · Layers(U6·U7d). */
export function PagesPanel({ tabId }: { tabId: string }) {
  const t = useT();
  const s = useCompareTab(tabId);
  const patch = useCompare((x) => x.patch);
  const [panel, setPanel] = useState<PanelTab>('pages');

  const count = s.result?.pages.length ?? Math.max(s.left.file?.pages.length ?? 0, s.right.file?.pages.length ?? 0);
  // 페이지 그림: 리비전 쪽 페이지, 리비전에서 빠진 페이지면 원본 쪽
  const thumb = (i: number): string | null => {
    const pair = s.result?.pages[i];
    const newPage = pair ? pair.newPage : i < (s.right.file?.pages.length ?? 0) ? i : null;
    const oldPage = pair ? pair.oldPage : i < (s.left.file?.pages.length ?? 0) ? i : null;
    if (s.right.file && newPage != null) return pageImageUrl(s.right.file.id, newPage, 0.15);
    if (s.left.file && oldPage != null) return pageImageUrl(s.left.file.id, oldPage, 0.15);
    return null;
  };

  return (
    <aside aria-label={t('compare.panel.left')} className="flex w-panel-left flex-none flex-col border-r border-default bg-surface">
      <div role="tablist" className="flex h-[34px] flex-none border-b border-subtle">
        {TABS.map((id) => (
          <button key={id} type="button" role="tab" aria-selected={panel === id} onClick={() => setPanel(id)}
            className={`flex-1 text-sm ${panel === id ? 'font-semibold text-fg shadow-[inset_0_-2px_0_var(--color-accent)]' : 'text-fg-muted hover:text-fg'}`}>
            {t(`compare.panel.${id}` as MessageKey)}
          </button>
        ))}
      </div>
      {panel === 'pages' ? (
        <div className="flex min-h-0 flex-1 flex-col gap-2.5 overflow-auto p-3">
          {count === 0 && <p className="m-0 p-2 text-center text-sm text-fg-faint">{t('compare.pages.empty')}</p>}
          {Array.from({ length: count }, (_, i) => {
            const on = i === s.pairIndex;
            const badge = pageBadge(s.result, i);
            const src = thumb(i);
            return (
              <button key={i} type="button" aria-current={on ? 'page' : undefined} onClick={() => patch(tabId, { pairIndex: i, selectedId: null })}
                className={`flex flex-none flex-col gap-1.5 rounded-sm border p-2 text-left ${on ? 'border-accent bg-accent-subtle' : 'border-default hover:bg-pressed'}`}>
                <span className="flex h-[92px] items-center justify-center overflow-hidden rounded-sm bg-[var(--drawing-paper)]">
                  {src && <img src={src} alt="" loading="lazy" draggable={false} className="max-h-full max-w-full" />}
                </span>
                <span className="flex items-center justify-between gap-2 text-sm text-fg-secondary">
                  {t('compare.pages.page', { n: i + 1 })}
                  {badge?.kind === 'changes' && <Badge tone="danger">{t('compare.pages.changes', { n: badge.n })}</Badge>}
                  {badge?.kind === 'none' && <span className="text-xs text-fg-faint">{t('compare.pages.none')}</span>}
                  {badge?.kind === 'added' && <Badge tone="success">{t('compare.pages.added')}</Badge>}
                  {badge?.kind === 'removed' && <Badge tone="warning">{t('compare.pages.removed')}</Badge>}
                  {!badge && <span className="text-xs text-fg-faint">–</span>}
                </span>
              </button>
            );
          })}
        </div>
      ) : (
        <EmptyState icon={panel === 'bookmarks' ? Bookmark : Layers}>
          {t(panel === 'bookmarks' ? 'compare.panel.bookmarksLater' : 'compare.panel.layersLater')}
        </EmptyState>
      )}
    </aside>
  );
}
