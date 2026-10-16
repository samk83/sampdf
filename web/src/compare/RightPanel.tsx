import { useState, type ReactNode } from 'react';
import { ChevronLeft, ChevronRight, Move, RotateCcw, Triangle } from 'lucide-react';
import { runCommand } from '../commands/runner';
import { useT, type MessageKey } from '../i18n';
import { Button } from '../ui/Button';
import { Toggle } from '../ui/Toggle';
import { selectChange, setIncluded, setReviewed, setSettings, startCompare } from './actions';
import { allChanges, labelsFor, pageNumber, useCompare, useCompareTab, type CompareTab } from './state';

/**
 * 오른쪽 패널 (설계서 2-2): 비교 전 = Compare settings(02), 겹쳐 보기 = Overlay(05),
 * 변경을 고르면 = Change(04), 결과만 있으면 = 요약 + 설정.
 */
export function RightPanel({ tabId }: { tabId: string }) {
  const t = useT();
  const s = useCompareTab(tabId);
  const selected = s.selectedId ? allChanges(s.result).find((c) => c.id === s.selectedId) : undefined;

  let title: MessageKey = 'compare.right.settings';
  let body: ReactNode = <SettingsSection tabId={tabId} s={s} heading={s.result ? 'compare.right.markSettings' : 'compare.right.before'} />;
  let footer: ReactNode = <StartFooter tabId={tabId} s={s} />;
  if (s.result && s.viewMode === 'overlay') {
    title = 'compare.right.overlay';
    body = <OverlaySection tabId={tabId} s={s} />;
    footer = null;
  } else if (s.result && selected) {
    title = 'compare.right.change';
    body = <ChangeSection tabId={tabId} s={s} />;
    footer = <StepFooter />;
  } else if (s.result) {
    title = 'compare.right.result';
    body = (
      <>
        <Summary s={s} />
        <SettingsSection tabId={tabId} s={s} heading="compare.right.markSettings" />
      </>
    );
    footer = s.result.total.total > 0 ? <StepFooter /> : null;
  }

  return (
    <aside aria-label={t(title)} className="flex w-panel-right flex-none flex-col border-l border-default bg-surface">
      <div className="flex h-[34px] flex-none items-center border-b border-subtle px-3.5 text-sm font-semibold text-fg-secondary">{t(title)}</div>
      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto p-3.5">{body}</div>
      {footer && <div className="flex flex-none flex-col gap-1.5 p-3.5">{footer}</div>}
    </aside>
  );
}

function Heading({ children }: { children: ReactNode }) {
  return <h3 className="m-0 text-xs font-semibold uppercase tracking-[0.04em] text-fg-faint">{children}</h3>;
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex min-w-0 flex-1 flex-col gap-1">
      <span className="text-xs text-fg-faint">{label}</span>
      {children}
    </label>
  );
}

const INPUT = 'h-control w-full rounded-sm border border-strong bg-background px-2.5 text-sm text-fg outline-none focus:border-accent focus-visible:shadow-none';

function SettingsSection({ tabId, s, heading }: { tabId: string; s: CompareTab; heading: MessageKey }) {
  const t = useT();
  const set = (p: Parameters<typeof setSettings>[1]) => setSettings(tabId, p);
  return (
    <>
      <Heading>{t(heading)}</Heading>
      <div className="flex gap-2">
        <Field label={t('compare.set.start')}>
          <input className={INPUT} value={s.settings.startLabel} maxLength={4} onChange={(e) => set({ startLabel: e.target.value })} />
        </Field>
        <Field label={t('compare.set.numbering')}>
          <select className={INPUT} value={s.settings.numbering} onChange={(e) => set({ numbering: e.target.value as 'sequential' | 'same' })}>
            <option value="sequential">{t('compare.set.each')}</option>
            <option value="same">{t('compare.set.same')}</option>
          </select>
        </Field>
      </div>
      <Toggle label={t('compare.set.cloudText')} note={t('compare.set.cloudTextNote')} on={s.settings.cloudText} onChange={(v) => set({ cloudText: v })} />
      <Toggle label={t('compare.set.ignoreTitle')} note={t('compare.set.ignoreTitleNote')} on={s.settings.ignoreTitle} onChange={(v) => set({ ignoreTitle: v })} />
      <Toggle label={t('compare.set.showRemoved')} on={s.settings.showRemoved} onChange={(v) => set({ showRemoved: v })} />
    </>
  );
}

function StartFooter({ tabId, s }: { tabId: string; s: CompareTab }) {
  const t = useT();
  const ready = !!s.left.file && !!s.right.file;
  const hint = !s.left.file ? 'compare.right.needOriginal' : !s.right.file ? 'compare.right.needRevised' : s.comparing ? 'compare.right.running' : 'compare.right.again';
  return (
    <>
      <Button variant="primary" disabled={!ready || s.comparing} onClick={() => startCompare(tabId)}>{t('compare.right.compare')}</Button>
      <span className="text-center text-xs text-fg-faint">{t(hint)}</span>
    </>
  );
}

function StepFooter() {
  const t = useT();
  return (
    <div className="flex gap-2">
      <Button className="flex-1" icon={<ChevronLeft className="h-icon w-icon" aria-hidden />} onClick={() => void runCommand('PREV')}>{t('compare.right.previous')}</Button>
      <Button className="flex-1" variant="primary" icon={<ChevronRight className="h-icon w-icon" aria-hidden />} onClick={() => void runCommand('NEXT')}>{t('compare.right.next')}</Button>
    </div>
  );
}

function Summary({ s }: { s: CompareTab }) {
  const t = useT();
  const total = s.result!.total;
  const rows: [MessageKey, number][] = [['compare.kind.added', total.added], ['compare.kind.removed', total.removed], ['compare.kind.modified', total.modified], ['compare.kind.text', total.text]];
  return (
    <>
      <Heading>{t('compare.right.found', { n: total.total })}</Heading>
      {total.total === 0 ? (
        <p className="m-0 text-sm text-success">{t('compare.right.noDifferences')}</p>
      ) : (
        <dl className="m-0 grid grid-cols-2 gap-x-3 gap-y-1.5 text-sm">
          {rows.map(([k, n]) => (
            <div key={k} className="contents">
              <dt className="text-fg-muted">{t(k)}</dt>
              <dd className="m-0 text-right font-mono text-fg">{n}</dd>
            </div>
          ))}
        </dl>
      )}
      {total.total > 0 && <p className="m-0 text-xs text-fg-faint">{t('compare.right.howTo')}</p>}
    </>
  );
}

function ChangeSection({ tabId, s }: { tabId: string; s: CompareTab }) {
  const t = useT();
  const setMarks = useCompare((x) => x.setMarks);
  const c = allChanges(s.result).find((x) => x.id === s.selectedId)!;
  const labels = labelsFor(s.result, s.marks, s.settings);
  const pairAt = s.result?.pages.findIndex((p) => p.changes.includes(c)) ?? 0;
  const pageNo = pageNumber(s.result?.pages[pairAt], pairAt);
  const reviewed = s.marks.reviewed.includes(c.id);
  const included = !s.marks.excluded.includes(c.id);
  const edit = s.marks.edits[c.id];
  const kind = t(c.category === 'text' ? 'compare.kind.text' : (`compare.kind.${c.kind}` as MessageKey));

  // 비우면 자동 번호로 돌아간다. 다 고친 뒤(Enter·칸 떠남) 한 번만 기록 → 되돌리기 한 번
  const setLabel = (label: string) =>
    setMarks(tabId, (m) => {
      const { label: old, ...rest } = m.edits[c.id] ?? {};
      if ((old ?? '') === label) return m;
      const next = label ? { ...rest, label } : rest;
      const edits = { ...m.edits };
      if (Object.keys(next).length) edits[c.id] = next;
      else delete edits[c.id];
      return { ...m, edits };
    });
  const resetEdit = () =>
    setMarks(tabId, (m) => {
      const { [c.id]: _gone, ...rest } = m.edits;
      return { ...m, edits: rest };
    });

  return (
    <>
      <div className="flex gap-3">
        <span className="flex h-[30px] min-w-[30px] items-center justify-center gap-0.5 rounded-sm bg-pressed px-1 font-mono text-sm font-semibold text-danger">
          <Triangle className="h-3 w-3" aria-hidden />{labels[c.id] ?? ''}
        </span>
        <div className="flex min-w-0 flex-col">
          <span className="break-words text-md font-semibold text-fg">{c.description || `${kind} #${c.number}`}</span>
          <span className="text-xs text-fg-muted">
            {kind} · {t('compare.pages.page', { n: pageNo })} · {c.confidence === 'high' ? t('compare.match.high') : t('compare.match.check')}
          </span>
        </div>
      </div>
      <div className="flex gap-2" key={c.id}>
        <Field label={t('compare.table.status')}>
          <select className={INPUT} value={reviewed ? 'reviewed' : 'open'} onChange={(e) => setReviewed(tabId, c.id, e.target.value === 'reviewed')}>
            <option value="open">{t('compare.status.open')}</option>
            <option value="reviewed">{t('compare.status.reviewed')}</option>
          </select>
        </Field>
        <Field label={t('compare.change.label')}>
          <LabelInput value={labels[c.id] ?? ''} disabled={!included} onCommit={setLabel} />
        </Field>
      </div>
      <Toggle label={t('compare.change.include')} note={t('compare.change.includeNote')} on={included} onChange={(v) => setIncluded(tabId, c.id, v)} />
      {edit && (
        <Button variant="ghost" className="self-start px-0" icon={<RotateCcw className="h-3.5 w-3.5" aria-hidden />} onClick={resetEdit}>
          {t('compare.change.reset')}
        </Button>
      )}
      <p className="m-0 text-xs text-fg-faint">{t('compare.change.hint')}</p>
      <Button variant="ghost" className="self-start px-0" onClick={() => selectChange(tabId, null)}>{t('compare.change.deselect')}</Button>
    </>
  );
}

function OverlaySection({ tabId, s }: { tabId: string; s: CompareTab }) {
  const t = useT();
  const patch = useCompare((x) => x.patch);
  const set = (p: Partial<CompareTab['overlay']>) => patch(tabId, (x) => ({ overlay: { ...x.overlay, ...p } }));
  const pair = s.result?.pages[s.pairIndex];
  const o = s.overlay;
  return (
    <>
      <Heading>{t('compare.overlay.display')}</Heading>
      <Toggle label={t('compare.overlay.byColor')} note={t('compare.overlay.byColorNote')} on={o.byColor} onChange={(v) => set({ byColor: v, nudging: false })} />
      <label className={`flex flex-col gap-1.5 ${o.byColor ? 'opacity-50' : ''}`}>
        <span className="flex justify-between text-sm text-fg-secondary">{t('compare.overlay.opacity')}<span className="font-mono text-fg">{Math.round(o.opacity * 100)}%</span></span>
        <input type="range" min={0} max={100} value={Math.round(o.opacity * 100)} disabled={o.byColor} aria-label={t('compare.overlay.opacity')}
          onChange={(e) => set({ opacity: Number(e.target.value) / 100 })} className="accent-[var(--color-accent)]" />
      </label>
      <Toggle label={t('compare.overlay.marks')} note={t('compare.overlay.marksNote')} on={o.showMarks} onChange={(v) => set({ showMarks: v })} />
      <Heading>{t('compare.overlay.alignment')}</Heading>
      <p className="m-0 text-sm text-fg-muted">
        {pair?.alignment ?? t('compare.overlay.noAlignment')}
        {(o.offset.x !== 0 || o.offset.y !== 0) && ` · ${t('compare.overlay.nudged', { x: o.offset.x.toFixed(1), y: o.offset.y.toFixed(1) })}`}
      </p>
      <div className="flex gap-2">
        <Button className="flex-1" disabled={o.byColor} onClick={() => void runCommand('NUDGE')} aria-pressed={o.nudging}
          icon={<Move className="h-icon w-icon" aria-hidden />}>{t(o.nudging ? 'compare.overlay.nudgeDone' : 'compare.overlay.nudge')}</Button>
        <Button className="flex-1" disabled={o.offset.x === 0 && o.offset.y === 0} onClick={() => set({ offset: { x: 0, y: 0 } })}>{t('compare.overlay.reset')}</Button>
      </div>
      <p className="m-0 text-xs text-fg-faint">{t('compare.overlay.nudgeHint')}</p>
    </>
  );
}

/** △ 번호 칸: 치는 동안은 칸 안에만, Enter·칸을 떠날 때 반영 (빈 칸 = 자동 번호). Esc = 원래대로. */
function LabelInput({ value, disabled, onCommit }: { value: string; disabled: boolean; onCommit: (v: string) => void }) {
  const [draft, setDraft] = useState<string | null>(null);
  const commit = () => {
    if (draft !== null && draft.trim() !== value) onCommit(draft.trim());
    setDraft(null);
  };
  return (
    <input className={INPUT} maxLength={4} disabled={disabled} value={draft ?? value}
      onChange={(e) => setDraft(e.target.value)} onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === 'Enter') commit();
        if (e.key === 'Escape') { e.stopPropagation(); setDraft(null); }
      }} />
  );
}
