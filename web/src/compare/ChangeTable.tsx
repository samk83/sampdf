import { ChevronDown, ChevronUp, Cloud, Highlighter, Table2 } from 'lucide-react';
import type { Change } from '../api';
import { shortText } from '../doc/geometry';
import { useT, type MessageKey } from '../i18n';
import { Badge } from '../ui/Badge';
import { EmptyState } from '../ui/EmptyState';
import { Table, type Column } from '../ui/Table';
import { selectChange, setIncluded } from './actions';
import { labelsFor, useCompare, useCompareTab, type Filter } from './state';

const FILTERS: Filter[] = ['all', 'text', 'graphic'];

/**
 * 아래 칸 변경 표 (시안 04): 지금 페이지의 변경. 체크 = 내보내기에 넣기, 줄을 누르면 그 자리로.
 * 머리 줄을 눌러 접고 펼친다.
 */
export function ChangeTable({ tabId }: { tabId: string }) {
  const t = useT();
  const s = useCompareTab(tabId);
  const patch = useCompare((x) => x.patch);
  const pair = s.result?.pages[s.pairIndex];
  const excluded = new Set(s.marks.excluded);
  const reviewed = new Set(s.marks.reviewed);
  const labels = labelsFor(s.result, s.marks, s.settings);
  const changes = pair?.changes ?? [];
  const shown = changes.filter((c) => s.filter === 'all' || c.category === s.filter);
  const count = (f: Filter) => changes.filter((c) => f === 'all' || c.category === f).length;

  const columns: Column<Change>[] = [
    {
      key: 'in', header: <span className="sr-only">{t('compare.table.include')}</span>, width: '40px', align: 'center',
      render: (c) => (
        <input type="checkbox" checked={!excluded.has(c.id)} aria-label={t('compare.table.includeOne', { n: c.number })}
          onClick={(e) => e.stopPropagation()} onChange={(e) => setIncluded(tabId, c.id, e.target.checked)}
          className="h-3.5 w-3.5 accent-[var(--color-primary)]" />
      ),
    },
    { key: 'delta', header: '△', width: '44px', render: (c) => labels[c.id] ? <span className="font-mono font-semibold text-danger">{labels[c.id]}</span> : <span className="text-fg-faint">–</span> },
    {
      key: 'type', header: t('compare.table.type'), width: '120px',
      render: (c) => (
        <span className="flex items-center gap-1.5">
          {c.category === 'text' ? <Highlighter className="h-3.5 w-3.5 text-warning" aria-hidden /> : <Cloud className="h-3.5 w-3.5 text-danger" aria-hidden />}
          {t(c.category === 'text' ? 'compare.kind.text' : (`compare.kind.${c.kind}` as MessageKey))}
        </span>
      ),
    },
    { key: 'desc', header: t('compare.table.description'), render: (c) => <span title={c.description}>{shortText(c.description) || c.description || `#${c.number}`}</span> },
    { key: 'match', header: t('compare.table.match'), width: '84px', render: (c) => c.confidence === 'high' ? t('compare.match.high') : <span className="text-warning">{t('compare.match.check')}</span> },
    {
      key: 'status', header: t('compare.table.status'), width: '96px',
      render: (c) => reviewed.has(c.id) ? <Badge tone="success">{t('compare.status.reviewed')}</Badge> : <Badge>{t('compare.status.open')}</Badge>,
    },
  ];

  return (
    <section aria-label={t('compare.table.label')} className={`flex flex-none flex-col border-t border-default bg-surface ${s.tableOpen && s.result ? 'h-[150px]' : ''}`}>
      <div className="flex h-[34px] flex-none items-center gap-2.5 pl-3 pr-2 text-sm">
        <span className="font-semibold text-fg">{t('compare.table.title')}</span>
        {s.result && (
          <>
            <span className="text-accent">{changes.length}</span>
            <span className="text-fg-muted">· {t('compare.table.reviewedN', { n: changes.filter((c) => reviewed.has(c.id)).length })} · {t('compare.table.excludedN', { n: changes.filter((c) => excluded.has(c.id)).length })}</span>
            <span role="group" aria-label={t('compare.table.filter')} className="ml-3 flex gap-1">
              {FILTERS.map((f) => (
                <button key={f} type="button" aria-pressed={s.filter === f} onClick={() => patch(tabId, { filter: f })}
                  className={`h-[22px] rounded-sm border px-2 text-xs ${s.filter === f ? 'border-strong bg-pressed text-fg' : 'border-dashed border-strong text-fg-muted hover:text-fg'}`}>
                  {t(`compare.filter.${f}` as MessageKey)}{f !== 'all' && ` ${count(f)}`}
                </button>
              ))}
            </span>
            <button type="button" aria-label={t(s.tableOpen ? 'compare.table.collapse' : 'compare.table.expand')} title={t(s.tableOpen ? 'compare.table.collapse' : 'compare.table.expand')}
              aria-expanded={s.tableOpen} onClick={() => patch(tabId, { tableOpen: !s.tableOpen })}
              className="ml-auto flex h-6 w-6 items-center justify-center rounded-sm text-fg-muted hover:bg-pressed hover:text-fg">
              {s.tableOpen ? <ChevronDown className="h-icon w-icon" aria-hidden /> : <ChevronUp className="h-icon w-icon" aria-hidden />}
            </button>
          </>
        )}
      </div>
      {!s.result ? (
        <div className="h-[86px]"><EmptyState icon={Table2}>{t('compare.table.waiting')}</EmptyState></div>
      ) : s.tableOpen && (
        <Table label={t('compare.table.label')} columns={columns} rows={shown} rowKey={(c) => c.id} selectedKey={s.selectedId}
          onRowClick={(c) => selectChange(tabId, c.id)} dimmed={(c) => excluded.has(c.id)}
          empty={<EmptyState icon={Table2}>{t(changes.length ? 'compare.table.noneFiltered' : pair?.isAddedPage ? 'compare.table.addedPage' : 'compare.table.none')}</EmptyState>} />
      )}
    </section>
  );
}
