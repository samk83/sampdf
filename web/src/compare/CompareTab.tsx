import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, ChevronLeft, ChevronRight, FolderOpen, Minus, Plus, RefreshCw, X } from 'lucide-react';
import { overlayUrl, pageImageUrl, removedLayerUrl, type FileInfo, type PageSize } from '../api';
import { useApp } from '../app/store';
import { classifyTarget } from '../app/keyboard';
import { useT } from '../i18n';
import { Button } from '../ui/Button';
import { EmptyState } from '../ui/EmptyState';
import { Toast } from '../ui/Toast';
import { PageViewer } from '../viewer/PageViewer';
import { fitView, focusRect, zoomAt, type View } from '../viewer/view';
import { cancelCompare, closeSide, compareTitle, openFile, setIncluded, startCompare } from './actions';
import { ChangeTable } from './ChangeTable';
import { FileDrop } from './FileDrop';
import { pickFileFor } from './filePicker';
import { PagesPanel } from './PagesPanel';
import { ProgressDialog } from './ProgressDialog';
import { RevisionMarks, Selection } from './RevisionMarks';
import { RightPanel } from './RightPanel';
import { SplitPanes } from './SplitPanes';
import { allChanges, hasWork, labelsFor, useCompare, useCompareTab, type Side, type SideKey } from './state';

type PaneSize = { width: number; height: number };

/** 겹쳐 보기 미세 조정 한 번에 미는 양 (pt). Shift = 10배. */
const NUDGE_STEP = 1;

/**
 * 비교 탭 (설계서 2-2·2-3, 시안 02~05·11): 왼쪽 Pages · 원본/리비전 칸(나란히 또는 겹쳐 보기) · 변경 표 · 오른쪽 패널.
 * 비교 상태는 compare/state.ts 보관소에 있고, 이 부품은 확대·이동(보기)만 직접 갖는다.
 */
export function CompareTab({ tabId, active, initialFiles }: { tabId: string; active: boolean; initialFiles?: File[] }) {
  const t = useT();
  const s = useCompareTab(tabId);
  const patch = useCompare((x) => x.patch);
  const setMarks = useCompare((x) => x.setMarks);
  const checkpoint = useCompare((x) => x.checkpoint);
  const panelsHidden = useApp((x) => x.panelsHidden);
  const setDirty = useApp((x) => x.setDirty);
  const setTitle = useApp((x) => x.setTitle);

  const [leftView, setLeftView] = useState<View | null>(null);
  const [rightView, setRightView] = useState<View | null>(null);
  const [leftPane, setLeftPane] = useState<PaneSize | null>(null);
  const [rightPane, setRightPane] = useState<PaneSize | null>(null);
  // 직접 확대·이동하기 전에는 칸 크기가 바뀔 때마다(표 접기·창 크기) 페이지 전체가 보이게 다시 맞춘다
  const [autoFit, setAutoFit] = useState(true);

  // ---- 탭이 열릴 때·닫힐 때 ----
  const opened = useRef(false);
  useEffect(() => {
    if (opened.current || !initialFiles?.length) return;
    opened.current = true;
    if (initialFiles[0]) void openFile(tabId, 'left', initialFiles[0]);
    if (initialFiles[1]) void openFile(tabId, 'right', initialFiles[1]);
  }, [tabId, initialFiles]);

  const dirty = hasWork(s.marks);
  useEffect(() => setDirty(tabId, dirty), [dirty, tabId, setDirty]);
  const title = compareTitle(tabId);
  useEffect(() => { if (title) setTitle(tabId, title); }, [title, tabId, setTitle]);

  // ---- 지금 페이지 짝 ----
  const { result, pairIndex } = s;
  const pair = result?.pages[pairIndex] ?? null;
  const oldPage = result ? pair?.oldPage ?? null : s.left.file && pairIndex < s.left.file.pages.length ? pairIndex : null;
  const newPage = result ? pair?.newPage ?? null : s.right.file && pairIndex < s.right.file.pages.length ? pairIndex : null;
  const leftSize: PageSize | null = s.left.file && oldPage != null ? s.left.file.pages[oldPage] : null;
  const rightSize: PageSize | null = s.right.file && newPage != null ? s.right.file.pages[newPage] : null;
  const pageCount = result ? result.pages.length : Math.max(s.left.file?.pages.length ?? 0, s.right.file?.pages.length ?? 0);

  const excluded = useMemo(() => new Set(s.marks.excluded), [s.marks.excluded]);
  const visible = useMemo(() => (pair ? pair.changes.filter((c) => !excluded.has(c.id)) : []), [pair, excluded]);
  const labels = useMemo(() => labelsFor(result, s.marks, s.settings), [result, s.marks, s.settings]);

  // ---- 보기 맞추기 ----
  useEffect(() => { setLeftView(null); setAutoFit(true); }, [pairIndex, oldPage, s.viewMode]);
  useEffect(() => { setRightView(null); setAutoFit(true); }, [pairIndex, newPage, s.viewMode]);
  useEffect(() => {
    if (!autoFit) return;
    setLeftView(null);
    setRightView(null);
  }, [autoFit, leftPane?.width, leftPane?.height, rightPane?.width, rightPane?.height]);
  useEffect(() => { if (!rightView && rightSize && rightPane) setRightView(fitView(rightSize, rightPane)); }, [rightView, rightSize, rightPane]);
  useEffect(() => { if (!leftView && leftSize && leftPane) setLeftView(fitView(leftSize, leftPane)); }, [leftView, leftSize, leftPane]);

  const changeLeft = useCallback((v: View) => {
    setAutoFit(false);
    setLeftView(v);
    if (s.sync) setRightView(v);
  }, [s.sync]);
  const changeRight = useCallback((v: View) => {
    setAutoFit(false);
    setRightView(v);
    if (s.sync && s.viewMode === 'side') setLeftView(v);
  }, [s.sync, s.viewMode]);

  const zoomBy = (factor: number) => {
    const pane = rightPane ?? leftPane;
    const base = rightView ?? leftView;
    if (!pane || !base) return;
    const v = zoomAt(base, factor, pane.width / 2, pane.height / 2);
    if (rightView || !leftView) changeRight(v); else changeLeft(v);
  };
  const fit = () => {
    setAutoFit(true);
    setLeftView(null);
    setRightView(null);
  };

  // ZOOM 명령 (보관소의 viewRequest로 부탁이 온다)
  const lastRequest = useRef(s.viewRequest);
  useEffect(() => {
    const r = s.viewRequest;
    if (!r || r === lastRequest.current) return;
    lastRequest.current = r;
    if (r.kind === 'fit') fit();
    else zoomBy(r.kind === 'in' ? 1.25 : 1 / 1.25);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [s.viewRequest]);

  // 변경을 고르면 그 자리로 (표·NEXT·PREV)
  const lastFocus = useRef(0);
  useEffect(() => {
    if (s.focusNo === lastFocus.current) return;
    const c = allChanges(result).find((x) => x.id === s.selectedId);
    if (!c || !rightPane || !rightView) return; // 페이지가 바뀌어 보기가 아직 없으면 다음 그리기 때
    lastFocus.current = s.focusNo;
    setAutoFit(false);
    const v = focusRect(c.bounds, rightPane, rightView);
    setRightView(v);
    if (s.sync) setLeftView(v);
    else if (leftPane && leftView) setLeftView(focusRect(c.boundsInOld, leftPane, leftView));
  }, [s.focusNo, s.selectedId, result, rightPane, rightView, leftPane, leftView, s.sync]);

  // 상태 표시줄의 확대 비율
  const zoom = (rightView ?? leftView)?.zoom ?? null;
  useEffect(() => patch(tabId, { zoom }), [zoom, tabId, patch]);

  // ---- 키: Delete = 고른 변경 빼기, Esc = 고르기 해제, 화살표 = 겹쳐 보기 미세 조정 ----
  useEffect(() => {
    if (!active) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.defaultPrevented || e.ctrlKey || e.altKey || e.metaKey) return;
      const target = classifyTarget(document.activeElement);
      if (target === 'field' || target === 'cmdline' || useApp.getState().helpOpen) return;
      const cur = useCompare.getState().tabs[tabId];
      if (!cur) return;
      if (e.key === 'Escape') {
        // 설계서 2-5: 먼저 도는 명령 취소(명령창 몫) → 비교 취소 → 미세 조정 끝 → 고르기 해제. 한 번에 하나씩
        if (useApp.getState().cmd.running) return;
        if (cancelCompare(tabId)) return e.preventDefault();
        if (cur.overlay.nudging) {
          e.preventDefault();
          return patch(tabId, { overlay: { ...cur.overlay, nudging: false } });
        }
        if (cur.selectedId) {
          e.preventDefault();
          return patch(tabId, { selectedId: null });
        }
        return;
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && cur.selectedId) {
        e.preventDefault();
        setIncluded(tabId, cur.selectedId, false);
      }
      const arrows: Record<string, [number, number]> = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
      if (cur.overlay.nudging && arrows[e.key]) {
        e.preventDefault();
        const k = (e.shiftKey ? 10 : 1) * NUDGE_STEP;
        const [dx, dy] = arrows[e.key];
        patch(tabId, { overlay: { ...cur.overlay, offset: { x: cur.overlay.offset.x + dx * k, y: cur.overlay.offset.y + dy * k } } });
      }
    };
    // 잡는 단계(capture)에서 받아 명령창 키 처리(useGlobalKeys)보다 먼저 본다
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [active, tabId, patch]);

  const select = (id: string) => patch(tabId, { selectedId: id });
  const marks = result && rightSize ? (
    <RevisionMarks changes={visible} page={rightSize} labels={labels} cloudText={s.settings.cloudText} edits={s.marks.edits}
      selectedId={s.selectedId} zoom={rightView?.zoom ?? 1} onSelect={select} onEditStart={() => checkpoint(tabId)}
      onEdit={(id, edit) => setMarks(tabId, (m) => ({ ...m, edits: { ...m.edits, [id]: edit } }), false)} />
  ) : null;

  const warnings = result?.warnings ?? [];
  // 차이 없음 화면 (시안 11): 결과마다 한 번, OK로 닫음
  const [noDiffSeen, setNoDiffSeen] = useState<string | null>(null);
  const noDiff = !!result && result.total.total === 0 && noDiffSeen !== result.id && !s.comparing;
  const overlayOn = s.viewMode === 'overlay' && !!result && !!rightSize && newPage != null;

  const pane = (side: SideKey) => {
    const info = s[side];
    const size = side === 'left' ? leftSize : rightSize;
    const page = side === 'left' ? oldPage : newPage;
    let body: ReactNode;
    if (info.file && size && page != null) {
      body = side === 'left' ? (
        <PageViewer label={t('compare.pane.originalView')} page={size} view={leftView} onViewChange={changeLeft} onResize={setLeftPane}
          imageUrl={(sc) => pageImageUrl(info.file!.id, page, sc)}
          overlay={<Selection changes={visible} selectedId={s.selectedId} zoom={leftView?.zoom ?? 1} useOld />} />
      ) : (
        <PageViewer label={t('compare.pane.revisedView')} page={size} view={rightView} onViewChange={changeRight} onResize={setRightPane}
          imageUrl={(sc) => pageImageUrl(info.file!.id, page, sc)}
          layers={result && s.settings.showRemoved && pair?.oldPage != null && pair.changes.some((c) => c.kind !== 'added')
            ? [{ key: 'removed', url: (sc) => removedLayerUrl(result.id, pairIndex, sc), opacity: 0.6, blend: 'multiply' }] : []}
          onBackgroundPointerDown={() => patch(tabId, { selectedId: null })} overlay={marks} />
      );
    } else if (info.file) {
      body = <Missing text={t(side === 'left' ? 'compare.pane.notInOriginal' : 'compare.pane.notInRevised')} />;
    } else if (info.error) {
      body = (
        <div className="flex h-full items-center justify-center bg-canvas p-6">
          <EmptyState icon={AlertTriangle} tone="danger" title={t('compare.error.cantOpen', { name: info.failedName ?? '' })}
            actions={<Button variant="primary" icon={<FolderOpen className="h-icon w-icon" aria-hidden />} onClick={() => pickFileFor(tabId, side)}>{t('compare.error.another')}</Button>}>
            <p className="m-0">{info.error}</p>
            <p className="m-0 mt-2 text-xs text-fg-faint">{t('compare.error.local')}</p>
          </EmptyState>
        </div>
      );
    } else {
      body = <FileDrop title={t(side === 'left' ? 'compare.drop.original' : 'compare.drop.revised')} busy={info.busy}
        onFiles={(fs) => {
          // 두 파일을 한 번에 놓으면 첫 파일 = 원본, 둘째 = 리비전
          if (fs.length > 1) { void openFile(tabId, 'left', fs[0]); void openFile(tabId, 'right', fs[1]); }
          else void openFile(tabId, side, fs[0]);
        }} onBrowse={() => pickFileFor(tabId, side)} />;
    }
    return (
      <section aria-label={t(side === 'left' ? 'compare.pane.original' : 'compare.pane.revised')} className="flex min-h-0 min-w-0 flex-1 flex-col">
        <PaneHeader side={side} info={info} pageCount={pageCount} page={page} onClose={() => closeSide(tabId, side)} />
        <div className="relative min-h-0 flex-1">{body}</div>
      </section>
    );
  };

  return (
    <div className="flex h-full min-h-0">
      {!panelsHidden && <PagesPanel tabId={tabId} />}
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="relative flex min-h-0 flex-1">
          {overlayOn ? (
            <section aria-label={t('compare.pane.overlay')} className="flex min-h-0 min-w-0 flex-1 flex-col">
              <OverlayLegend byColor={s.overlay.byColor} nudging={s.overlay.nudging} />
              <div className="relative min-h-0 flex-1">
                <PageViewer label={t('compare.pane.overlayView')} page={rightSize!} view={rightView} onViewChange={changeRight} onResize={setRightPane}
                  imageUrl={(sc) => s.overlay.byColor && pair?.oldPage != null ? overlayUrl(result!.id, pairIndex, sc) : pageImageUrl(s.right.file!.id, newPage!, sc)}
                  layers={!s.overlay.byColor && s.left.file && oldPage != null
                    ? [{ key: 'original', url: (sc) => pageImageUrl(s.left.file!.id, oldPage, sc), opacity: s.overlay.opacity, blend: 'multiply', offset: s.overlay.offset }] : []}
                  onBackgroundPointerDown={() => patch(tabId, { selectedId: null })}
                  overlay={s.overlay.showMarks ? marks : null} />
              </div>
            </section>
          ) : (
            <SplitPanes left={pane('left')} right={pane('right')} />
          )}
          {(leftView || rightView) && pageCount > 0 && (
            <NavPill page={pairIndex} count={pageCount} zoom={zoom} onPage={(i) => patch(tabId, { pairIndex: i, selectedId: null })}
              onZoom={zoomBy} onFit={fit} />
          )}
          {s.comparing && <ProgressDialog onCancel={() => cancelCompare(tabId)} />}
          {s.compareError && !s.comparing && (
            <div className="absolute inset-0 z-floating flex items-center justify-center p-6">
              <EmptyState icon={AlertTriangle} tone="danger" title={t('compare.error.failed')}
                actions={
                  <>
                    <Button variant="primary" icon={<RefreshCw className="h-icon w-icon" aria-hidden />} onClick={() => startCompare(tabId)}>{t('compare.error.retry')}</Button>
                    <Button onClick={() => patch(tabId, { compareError: null })}>{t('compare.error.dismiss')}</Button>
                  </>
                }>
                <p className="m-0">{s.compareError}</p>
              </EmptyState>
            </div>
          )}
          {noDiff && (
            <div className="absolute inset-0 z-floating flex items-center justify-center p-6">
              <EmptyState icon={CheckCircle2} tone="success" title={t('compare.nodiff.title')}
                actions={<Button variant="primary" onClick={() => setNoDiffSeen(result!.id)}>{t('compare.nodiff.ok')}</Button>}>
                <p className="m-0">{t('compare.nodiff.body', { pages: result!.pages.length })}</p>
              </EmptyState>
            </div>
          )}
          {warnings.length > 0 && !s.warningsSeen && !s.comparing && (
            <Toast title={t('compare.warning.title', { n: warnings.length })} closeLabel={t('compare.warning.close')}
              onClose={() => patch(tabId, { warningsSeen: true })}
              actions={<button type="button" className="text-accent hover:underline" onClick={() => patch(tabId, { warningsSeen: true })}>{t('compare.warning.ok')}</button>}>
              <ul className="m-0 flex list-none flex-col gap-1 p-0">{warnings.slice(0, 4).map((w) => <li key={w}>{w}</li>)}</ul>
            </Toast>
          )}
        </div>
        <ChangeTable tabId={tabId} />
      </div>
      {!panelsHidden && <RightPanel tabId={tabId} />}
    </div>
  );
}

function PaneHeader({ side, info, pageCount, page, onClose }: { side: SideKey; info: Side; pageCount: number; page: number | null; onClose: () => void }) {
  const t = useT();
  const original = side === 'left';
  return (
    <div className="flex h-[34px] flex-none items-center gap-2.5 border-b border-subtle bg-sunken pl-3 pr-1.5 text-sm">
      <span className={`rounded-sm px-2 py-0.5 text-xs font-semibold ${original ? 'bg-pressed text-fg-secondary' : 'bg-accent-subtle text-accent'}`}>
        {t(original ? 'compare.pane.original' : 'compare.pane.revised')}
      </span>
      <span className={`min-w-0 truncate ${info.file ? 'text-fg' : 'text-fg-secondary'}`}>{info.file?.name ?? (info.busy ? t('compare.drop.opening') : t('compare.pane.notSelected'))}</span>
      <span className="ml-auto whitespace-nowrap text-fg-muted">{info.file ? fileNote(info.file, info.size, page, pageCount, t) : info.error ? t('compare.pane.failed') : ''}</span>
      {info.file && (
        <button type="button" aria-label={t('compare.pane.close', { name: info.file.name })} title={t('compare.pane.close', { name: info.file.name })} onClick={onClose}
          className="flex h-6 w-6 items-center justify-center rounded-sm text-fg-faint hover:bg-pressed hover:text-fg">
          <X className="h-3.5 w-3.5" aria-hidden />
        </button>
      )}
    </div>
  );
}

function fileNote(file: FileInfo, size: number | null, page: number | null, _count: number, t: ReturnType<typeof useT>) {
  const parts = [t('compare.pane.pages', { n: file.pages.length })];
  if (page != null && file.pages.length > 1) parts[0] = t('compare.pane.pageOf', { n: page + 1, total: file.pages.length });
  if (size != null) parts.push(size >= 1048576 ? t('compare.pane.mb', { n: (size / 1048576).toFixed(1) }) : t('compare.pane.kb', { n: Math.max(1, Math.round(size / 1024)) }));
  if (file.isImage) parts.push(t('compare.pane.image'));
  return parts.join(' · ');
}

function Missing({ text }: { text: string }) {
  return <div className="flex h-full items-center justify-center bg-canvas p-8 text-center text-md text-fg-muted">{text}</div>;
}

function OverlayLegend({ byColor, nudging }: { byColor: boolean; nudging: boolean }) {
  const t = useT();
  return (
    <div className="flex h-[34px] flex-none items-center gap-4 border-b border-subtle bg-sunken px-3 text-sm text-fg-secondary">
      {byColor ? (
        <>
          <span className="flex items-center gap-1.5"><span className="h-[3px] w-4 rounded-full bg-[var(--overlay-original)]" />{t('compare.overlay.removed')}</span>
          <span className="flex items-center gap-1.5"><span className="h-[3px] w-4 rounded-full bg-[var(--overlay-revised)]" />{t('compare.overlay.added')}</span>
        </>
      ) : (
        <span>{t('compare.overlay.opacityLegend')}</span>
      )}
      {nudging && <span className="ml-auto font-semibold text-warning">{t('compare.overlay.nudging')}</span>}
    </div>
  );
}

function NavPill({ page, count, zoom, onPage, onZoom, onFit }: {
  page: number; count: number; zoom: number | null; onPage: (i: number) => void; onZoom: (f: number) => void; onFit: () => void;
}) {
  const t = useT();
  const btn = 'flex h-7 w-7 items-center justify-center rounded-sm text-fg-secondary hover:bg-pressed hover:text-fg disabled:opacity-40';
  return (
    <div role="toolbar" aria-label={t('compare.nav.label')}
      className="absolute bottom-3 left-1/2 z-floating flex -translate-x-1/2 items-center gap-0.5 rounded-md border border-default bg-surface px-1 py-0.5 text-sm shadow-md">
      <button type="button" className={btn} aria-label={t('compare.nav.prevPage')} title={t('compare.nav.prevPage')} disabled={page <= 0} onClick={() => onPage(page - 1)}>
        <ChevronLeft className="h-icon w-icon" aria-hidden />
      </button>
      <span className="min-w-[44px] text-center font-mono text-fg">{page + 1} / {count}</span>
      <button type="button" className={btn} aria-label={t('compare.nav.nextPage')} title={t('compare.nav.nextPage')} disabled={page >= count - 1} onClick={() => onPage(page + 1)}>
        <ChevronRight className="h-icon w-icon" aria-hidden />
      </button>
      <span className="mx-1 h-4 w-px bg-[var(--color-border)]" />
      <button type="button" className={btn} aria-label={t('compare.nav.zoomOut')} title={t('compare.nav.zoomOut')} onClick={() => onZoom(1 / 1.25)}><Minus className="h-icon w-icon" aria-hidden /></button>
      <span className="min-w-[48px] text-center font-mono text-fg">{zoom ? `${Math.round(zoom * 100)}%` : '–'}</span>
      <button type="button" className={btn} aria-label={t('compare.nav.zoomIn')} title={t('compare.nav.zoomIn')} onClick={() => onZoom(1.25)}><Plus className="h-icon w-icon" aria-hidden /></button>
      <button type="button" className="h-7 rounded-sm px-2 text-fg-muted hover:bg-pressed hover:text-fg" onClick={onFit}>{t('compare.nav.fit')}</button>
    </div>
  );
}
