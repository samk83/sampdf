import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, ChevronLeft, ChevronRight, Columns2, Download, Layers, Link2, Link2Off, Loader2, Maximize, PanelRightOpen, X, ZoomIn, ZoomOut } from 'lucide-react';
import { closeFile, exportPdf, overlayUrl, pageImageUrl, removedLayerUrl, runCompare, uploadFile, type Change, type CompareResult, type FileInfo, type MarkEditPayload, type PageSize } from '../api';
import { AppHeader, IconButton, type Tab } from '../ui/AppHeader';
import { Segmented } from '../ui/Segmented';
import { ChangeList, type Filter } from './ChangeList';
import { DropZone } from './DropZone';
import { hasCloud, RevisionMarks, Selection, type MarkEdit } from './Marks';
import { assignLabels } from './numbering';
import { PageViewer } from './PageViewer';
import { SettingsMenu, type MarkSettings } from './SettingsMenu';
import { SplitPanes } from './SplitPanes';
import { fitView, focusRect, zoomAt, type View } from './view';

type Side = { file: FileInfo | null; busy: boolean; error: string | null };
const emptySide: Side = { file: null, busy: false, error: null };
type PaneSize = { width: number; height: number };

/**
 * 도면 비교 화면: 왼쪽 원본, 오른쪽 리비전. 두 파일이 모두 열리면 자동으로 비교하고
 * 리비전 칸에 하이라이트·구름·△를 보여 준다. 내려받기는 표시를 넣은 리비전 PDF.
 */
export function ComparePage({ tab, onTab }: { tab: Tab; onTab: (t: Tab) => void }) {
  const [left, setLeft] = useState<Side>(emptySide);
  const [right, setRight] = useState<Side>(emptySide);
  const [result, setResult] = useState<CompareResult | null>(null);
  const [comparing, setComparing] = useState(false);
  const [compareError, setCompareError] = useState<string | null>(null);
  const [warningsOpen, setWarningsOpen] = useState(true);
  const [pairIndex, setPairIndex] = useState(0);
  const [settings, setSettings] = useState<MarkSettings>({ numbering: 'sequential', startLabel: '1', cloudText: true, ignoreTitle: false });
  const ignoreTitle = settings.ignoreTitle;
  const [viewMode, setViewMode] = useState<'side' | 'overlay'>('side');
  const [overlayOpacity, setOverlayOpacity] = useState(0.5);
  const [overlayColor, setOverlayColor] = useState(false);
  const [edits, setEdits] = useState<Record<string, MarkEdit>>({});
  const [excluded, setExcluded] = useState<Set<string>>(new Set());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>('all');
  const [listOpen, setListOpen] = useState(true);
  const [sync, setSync] = useState(true);
  const [showRemoved, setShowRemoved] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [leftView, setLeftView] = useState<View | null>(null);
  const [rightView, setRightView] = useState<View | null>(null);
  const [leftPane, setLeftPane] = useState<PaneSize | null>(null);
  const [rightPane, setRightPane] = useState<PaneSize | null>(null);
  // 사용자가 직접 확대·이동하기 전에는 칸 크기가 바뀔 때마다(목록 열기·창 크기) 페이지 전체가 보이게 다시 맞춘다
  const [autoFit, setAutoFit] = useState(true);

  // ---- 파일 열기 ----
  const open = async (side: 'left' | 'right', file: File) => {
    const set = side === 'left' ? setLeft : setRight;
    const current = side === 'left' ? left : right;
    set({ file: current.file, busy: true, error: null });
    try {
      const info = await uploadFile(file);
      if (current.file) closeFile(current.file.id);
      set({ file: info, busy: false, error: null });
      setResult(null);
      setPairIndex(0);
      setExcluded(new Set());
      setEdits({});
      setSelectedId(null);
      (side === 'left' ? setLeftView : setRightView)(null);
    } catch (e) {
      set({ file: current.file, busy: false, error: (e as Error).message });
    }
  };

  const close = (side: 'left' | 'right') => {
    const s = side === 'left' ? left : right;
    if (s.file) closeFile(s.file.id);
    (side === 'left' ? setLeft : setRight)(emptySide);
    (side === 'left' ? setLeftView : setRightView)(null);
    setResult(null);
    setCompareError(null);
    setSelectedId(null);
  };

  // ---- 두 파일이 모두 있으면 자동 비교 (제목란 무시를 바꾸면 다시) ----
  useEffect(() => {
    if (!left.file || !right.file) return;
    let alive = true;
    setComparing(true);
    setCompareError(null);
    runCompare(left.file.id, right.file.id, ignoreTitle)
      .then((r) => {
        if (!alive) return;
        setResult(r);
        setWarningsOpen(true);
        setExcluded(new Set());
        setEdits({});
        setSelectedId(null);
        // 처음 보여 줄 페이지: 변경이 있는 첫 페이지
        const first = r.pages.findIndex((p) => p.changes.length > 0 || p.isAddedPage || p.isRemovedPage);
        setPairIndex(first >= 0 ? first : 0);
      })
      .catch((e) => alive && setCompareError((e as Error).message))
      .finally(() => alive && setComparing(false));
    return () => {
      alive = false;
    };
  }, [left.file, right.file, ignoreTitle]);

  // ---- 현재 페이지 짝 ----
  const pageCount = result ? result.pages.length : Math.max(left.file?.pages.length ?? 0, right.file?.pages.length ?? 0);
  const pair = result?.pages[pairIndex] ?? null;
  const oldPage = result ? pair?.oldPage ?? null : left.file && pairIndex < left.file.pages.length ? pairIndex : null;
  const newPage = result ? pair?.newPage ?? null : right.file && pairIndex < right.file.pages.length ? pairIndex : null;
  const leftSize: PageSize | null = left.file && oldPage != null ? left.file.pages[oldPage] : null;
  const rightSize: PageSize | null = right.file && newPage != null ? right.file.pages[newPage] : null;

  const visible = useMemo(() => (pair ? pair.changes.filter((c) => !excluded.has(c.id)) : []), [pair, excluded]);
  // △ 번호: 문서 전체에서 구름이 붙는 변경을 페이지·번호 순으로 세어 붙인다 (지운 변경은 건너뜀)
  const labels = useMemo(() => {
    const ids = result ? result.pages.flatMap((p) => p.changes).filter((c) => !excluded.has(c.id) && hasCloud(c, settings.cloudText)).map((c) => c.id) : [];
    return assignLabels(ids, settings.startLabel, settings.numbering);
  }, [result, excluded, settings.cloudText, settings.startLabel, settings.numbering]);
  const totals = useMemo(() => {
    const all = result ? result.pages.flatMap((p) => p.changes).filter((c) => !excluded.has(c.id)) : [];
    return {
      added: all.filter((c) => c.category === 'graphic' && c.kind === 'added').length,
      removed: all.filter((c) => c.category === 'graphic' && c.kind === 'removed').length,
      modified: all.filter((c) => c.category === 'graphic' && c.kind === 'modified').length,
      text: all.filter((c) => c.category === 'text').length,
    };
  }, [result, excluded]);

  // 페이지가 바뀌거나 칸 크기를 처음 알면 페이지 전체가 보이게 맞춘다
  useEffect(() => {
    setLeftView(null);
    setAutoFit(true);
  }, [pairIndex, oldPage]);
  useEffect(() => {
    setRightView(null);
    setAutoFit(true);
  }, [pairIndex, newPage]);
  useEffect(() => {
    if (!autoFit) return;
    setLeftView(null);
    setRightView(null);
  }, [autoFit, leftPane?.width, leftPane?.height, rightPane?.width, rightPane?.height]);
  useEffect(() => {
    if (!rightView && rightSize && rightPane) setRightView(fitView(rightSize, rightPane));
  }, [rightView, rightSize, rightPane]);
  useEffect(() => {
    if (!leftView && leftSize && leftPane) setLeftView(fitView(leftSize, leftPane));
  }, [leftView, leftSize, leftPane]);

  // ---- 함께 움직이기 ----
  const changeLeft = useCallback((v: View) => {
    setAutoFit(false);
    setLeftView(v);
    if (sync) setRightView(v);
  }, [sync]);
  const changeRight = useCallback((v: View) => {
    setAutoFit(false);
    setRightView(v);
    if (sync) setLeftView(v);
  }, [sync]);

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

  const select = (c: Change) => {
    setSelectedId(c.id);
    setAutoFit(false);
    if (rightPane && rightView) {
      const v = focusRect(c.bounds, rightPane, rightView);
      setRightView(v);
      if (sync) setLeftView(v);
      else if (leftPane && leftView) setLeftView(focusRect(c.boundsInOld, leftPane, leftView));
    }
  };

  const remove = (c: Change) => {
    setExcluded((s) => new Set(s).add(c.id));
    if (selectedId === c.id) setSelectedId(null);
  };

  const editMark = useCallback((id: string, e: MarkEdit) => setEdits((all) => ({ ...all, [id]: e })), []);

  // Delete 키: 선택한 변경 지우기, Esc: 선택 해제 (글자 입력 중에는 무시)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const typing = (e.target as HTMLElement)?.closest('input, textarea');
      if (typing) return;
      if (e.key === 'Escape') setSelectedId(null);
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedId) {
        setExcluded((s) => new Set(s).add(selectedId));
        setSelectedId(null);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [selectedId]);

  const download = async () => {
    if (!result || !right.file) return;
    setExporting(true);
    try {
      const payload: Record<string, MarkEditPayload> = {};
      for (const p of result.pages)
        for (const c of p.changes) {
          if (excluded.has(c.id)) continue;
          const e = edits[c.id];
          if (labels[c.id] || e) payload[c.id] = { label: labels[c.id], cloud: e?.cloud, delta: e?.delta, highlights: e?.highlights };
        }
      await exportPdf(result.id, {
        revisionLabel: settings.startLabel.trim() || '1',
        excluded: [...excluded],
        cloudTextChanges: settings.cloudText,
        edits: payload,
      }, right.file.name.replace(/\.[^.]+$/, '') + ' (compared).pdf');
    } catch (e) {
      setCompareError(`내려받지 못했어요: ${(e as Error).message}`);
    } finally {
      setExporting(false);
    }
  };

  const zoomPercent = Math.round(((rightView ?? leftView)?.zoom ?? 0) * 100);

  const marks = result && rightSize ? (
    <RevisionMarks changes={visible} page={rightSize} labels={labels} cloudText={settings.cloudText} edits={edits}
      selectedId={selectedId} zoom={rightView?.zoom ?? 1} onSelect={setSelectedId} onEdit={editMark} />
  ) : null;

  return (
    <div className="flex h-screen flex-col bg-[#f4f5f7] text-ink">
      <AppHeader
        tab={tab}
        onTab={onTab}
        left={result && (
          <div aria-label="변경 요약" className="flex flex-wrap gap-1.5">
            {totals.added > 0 && <Chip className="bg-[#e0ecff] text-[#1e40af]">추가 {totals.added}</Chip>}
            {totals.removed > 0 && <Chip className="bg-[#fee2e2] text-[#991b1b]">삭제 {totals.removed}</Chip>}
            {totals.modified > 0 && <Chip className="bg-[#ede9fe] text-[#5b21b6]">변경 {totals.modified}</Chip>}
            {totals.text > 0 && <Chip className="bg-[#fef3c7] text-[#854d0e]">글자 {totals.text}</Chip>}
            {totals.added + totals.removed + totals.modified + totals.text === 0 && <Chip className="bg-[#dcfce7] text-[#166534]">차이 없음</Chip>}
          </div>
        )}
        right={
          <>
            <Segmented label="보기 방식" value={viewMode} onChange={(v) => { setViewMode(v); setAutoFit(true); }}
              options={[
                { value: 'side', label: '나란히', icon: <Columns2 className="h-[15px] w-[15px]" aria-hidden /> },
                { value: 'overlay', label: '겹쳐 보기', icon: <Layers className="h-[15px] w-[15px]" aria-hidden /> },
              ]} />
            {viewMode === 'side' && (
              <Segmented label="두 도면 이동" value={sync ? 'together' : 'separate'} onChange={(v) => setSync(v === 'together')}
                options={[
                  { value: 'together', label: '같이 이동', icon: <Link2 className="h-[15px] w-[15px]" aria-hidden /> },
                  { value: 'separate', label: '따로 이동', icon: <Link2Off className="h-[15px] w-[15px]" aria-hidden /> },
                ]} />
            )}
            {pageCount > 1 && (
              <div className="flex items-center gap-0.5 border-l border-line pl-2">
                <IconButton label="이전 페이지" disabled={pairIndex === 0} onClick={() => setPairIndex((i) => Math.max(0, i - 1))}>
                  <ChevronLeft className="h-[18px] w-[18px]" aria-hidden />
                </IconButton>
                <span className="font-mono text-[13px] text-[#3f4552]">{pairIndex + 1} / {pageCount}</span>
                <IconButton label="다음 페이지" disabled={pairIndex >= pageCount - 1} onClick={() => setPairIndex((i) => Math.min(pageCount - 1, i + 1))}>
                  <ChevronRight className="h-[18px] w-[18px]" aria-hidden />
                </IconButton>
              </div>
            )}
            <div className="flex items-center gap-0.5 border-l border-line pl-2">
              <IconButton label="축소" onClick={() => zoomBy(1 / 1.25)} disabled={!rightView && !leftView}><ZoomOut className="h-[18px] w-[18px]" aria-hidden /></IconButton>
              <span className="w-11 text-center font-mono text-[13px] text-[#3f4552]">{zoomPercent ? `${zoomPercent}%` : '–'}</span>
              <IconButton label="확대" onClick={() => zoomBy(1.25)} disabled={!rightView && !leftView}><ZoomIn className="h-[18px] w-[18px]" aria-hidden /></IconButton>
              <IconButton label="화면에 맞추기" onClick={fit} disabled={!rightView && !leftView}><Maximize className="h-[18px] w-[18px]" aria-hidden /></IconButton>
            </div>
            {viewMode === 'side' ? (
              <label className="flex cursor-pointer items-center gap-1.5 border-l border-line pl-2.5 text-[13px] text-[#3f4552]">
                <input type="checkbox" checked={showRemoved} onChange={(e) => setShowRemoved(e.target.checked)} className="h-4 w-4 accent-[#dc2626]" />
                지워진 선 보기
              </label>
            ) : (
              <div className="flex items-center gap-2.5 border-l border-line pl-2.5 text-[13px] text-[#3f4552]">
                <label className="flex items-center gap-2">
                  원본 투명도
                  <input type="range" min={0} max={100} value={Math.round(overlayOpacity * 100)} disabled={overlayColor} aria-label="원본 투명도"
                    onChange={(e) => setOverlayOpacity(Number(e.target.value) / 100)} className="w-24 accent-[#1d4ed8] disabled:opacity-40" />
                  <span className="w-9 font-mono text-xs">{Math.round(overlayOpacity * 100)}%</span>
                </label>
                <label className="flex cursor-pointer items-center gap-1.5">
                  <input type="checkbox" checked={overlayColor} onChange={(e) => setOverlayColor(e.target.checked)} className="h-4 w-4 accent-[#1d4ed8]" />
                  색으로 구분
                </label>
              </div>
            )}
            <SettingsMenu value={settings} onChange={setSettings} />
            <button type="button" onClick={download} disabled={!result || exporting}
              className="ml-1 inline-flex h-[38px] items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-[#1e40af] disabled:bg-[#c7cdd8]">
              {exporting ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <Download className="h-4 w-4" aria-hidden />}
              PDF 내려받기
            </button>
          </>
        }
      />

      {(compareError || (result && result.warnings.length > 0 && warningsOpen)) && (
        <div role="status" className={`flex items-start gap-2 border-b px-4 py-2 text-[13px] ${compareError ? 'border-red-200 bg-red-50 text-red-800' : 'border-amber-200 bg-amber-50 text-amber-900'}`}>
          <AlertTriangle className="mt-0.5 h-4 w-4 flex-none" aria-hidden />
          <div className="flex-1">{compareError ?? result!.warnings.map((w) => <div key={w}>{w}</div>)}</div>
          <button type="button" aria-label="알림 닫기" onClick={() => (compareError ? setCompareError(null) : setWarningsOpen(false))} className="rounded p-0.5 hover:bg-black/5">
            <X className="h-4 w-4" aria-hidden />
          </button>
        </div>
      )}

      <div className="flex min-h-0 flex-1">
        <div className="relative flex min-w-0 flex-1">
          {viewMode === 'overlay' && result && rightSize && newPage != null ? (
            <Pane title="겹쳐 보기" side={right} pageNote={overlayColor ? '원본만 빨강 · 리비전만 파랑' : `원본을 ${Math.round(overlayOpacity * 100)}%로 겹침`} onClose={() => setViewMode('side')} closeLabel="겹쳐 보기 닫기">
              <PageViewer label="원본과 리비전을 겹친 도면" page={rightSize} view={rightView} onViewChange={changeRight} onResize={setRightPane}
                imageUrl={(sc) => overlayColor && pair?.oldPage != null ? overlayUrl(result.id, pairIndex, sc) : pageImageUrl(right.file!.id, newPage, sc)}
                layers={!overlayColor && left.file && oldPage != null ? [{ key: 'original', url: (sc) => pageImageUrl(left.file!.id, oldPage, sc), opacity: overlayOpacity, blend: 'multiply' }] : []}
                onBackgroundPointerDown={() => setSelectedId(null)}
                overlay={marks} />
            </Pane>
          ) : (
            <SplitPanes
              left={
                <Pane title="원본" side={left} pageNote={pageNote(left.file, oldPage)} onClose={() => close('left')}>
                  {left.file && leftSize && oldPage != null ? (
                    <PageViewer label="원본 도면" page={leftSize} view={leftView} onViewChange={changeLeft} onResize={setLeftPane}
                      imageUrl={(sc) => pageImageUrl(left.file!.id, oldPage, sc)}
                      overlay={<Selection changes={visible} selectedId={selectedId} zoom={leftView?.zoom ?? 1} useOld />} />
                  ) : left.file ? (
                    <MissingPage text="이 페이지는 원본에 없어요 (리비전에서 새로 생긴 페이지)" />
                  ) : (
                    <DropZone title="원본 파일 열기" busy={left.busy} error={left.error} onFile={(f) => open('left', f)} />
                  )}
                </Pane>
              }
              right={
                <Pane title="리비전" side={right} pageNote={pageNote(right.file, newPage)} onClose={() => close('right')}>
                  {right.file && rightSize && newPage != null ? (
                    <PageViewer label="리비전 도면과 변경 표시" page={rightSize} view={rightView} onViewChange={changeRight} onResize={setRightPane}
                      imageUrl={(sc) => pageImageUrl(right.file!.id, newPage, sc)}
                      layers={result && showRemoved && pair?.oldPage != null && pair.changes.some((c) => c.kind !== 'added')
                        ? [{ key: 'removed', url: (sc) => removedLayerUrl(result.id, pairIndex, sc), opacity: 0.6, blend: 'multiply' }] : []}
                      onBackgroundPointerDown={() => setSelectedId(null)}
                      overlay={marks} />
                  ) : right.file ? (
                    <MissingPage text="이 페이지는 리비전에서 빠졌어요 (원본에만 있는 페이지)" />
                  ) : (
                    <DropZone title="리비전 파일 열기" busy={right.busy} error={right.error} onFile={(f) => open('right', f)} />
                  )}
                </Pane>
              }
            />
          )}
          {comparing && (
            <div className="absolute inset-0 flex items-center justify-center bg-white/50 backdrop-blur-[1px]">
              <div className="flex items-center gap-3 rounded-xl bg-white px-5 py-4 text-sm font-semibold shadow-lg">
                <Loader2 className="h-5 w-5 animate-spin text-accent" aria-hidden /> 비교하는 중…
              </div>
            </div>
          )}
        </div>

        {result && listOpen && pair && (
          <ChangeList changes={visible} removedCount={excluded.size} filter={filter} onFilter={setFilter} selectedId={selectedId}
            labels={labels} editedIds={Object.keys(edits)} onResetEdits={() => setEdits({})}
            onSelect={select} onRemove={remove} onRestoreAll={() => setExcluded(new Set())} onCollapse={() => setListOpen(false)} />
        )}
        {result && !listOpen && (
          <div className="flex flex-none flex-col items-center border-l border-line bg-white p-1.5">
            <IconButton label="변경 목록 펼치기" onClick={() => setListOpen(true)}><PanelRightOpen className="h-[18px] w-[18px]" aria-hidden /></IconButton>
            <span className="mt-2 rounded-full bg-ink px-1.5 py-0.5 text-[11px] font-bold text-white">{visible.length}</span>
          </div>
        )}
      </div>

      <footer className="flex h-[34px] items-center gap-2 border-t border-line bg-white px-4 text-xs text-muted">
        내 PC에서만 처리 · 두 파일이 모두 열리면 자동으로 비교합니다 · 올린 원본 파일은 바뀌지 않아요
        {pair?.alignment && <span className="ml-auto font-mono">정렬: {pair.alignment}</span>}
      </footer>
    </div>
  );
}

function pageNote(file: FileInfo | null, page: number | null) {
  if (!file) return null;
  return `${file.pages.length}페이지${page != null && file.pages.length > 1 ? ` 중 ${page + 1}` : ''}${file.isImage ? ' · 그림' : ''}`;
}

function Chip({ className, children }: { className: string; children: React.ReactNode }) {
  return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${className}`}>{children}</span>;
}

function Pane({ title, side, pageNote, onClose, closeLabel, children }: {
  title: string; side: Side; pageNote: string | null; onClose: () => void; closeLabel?: string; children: React.ReactNode;
}) {
  return (
    <section aria-label={title} className="flex min-h-0 min-w-0 flex-1 flex-col bg-canvas">
      <div className="flex h-10 flex-none items-center justify-between border-b border-line bg-white pl-3.5 pr-2 text-[13px]">
        <div className="flex min-w-0 items-center gap-2">
          <span className="font-bold text-[#4a505c]">{title}</span>
          {side.file && <span className="truncate">{side.file.name}</span>}
          {pageNote && <span className="whitespace-nowrap text-[#6b7280]">· {pageNote}</span>}
        </div>
        {side.file && (
          <button type="button" aria-label={closeLabel ?? `${title} 닫기`} onClick={onClose} className="flex h-8 w-8 items-center justify-center rounded-md text-muted hover:bg-[#eef0f3]">
            <X className="h-4 w-4" aria-hidden />
          </button>
        )}
      </div>
      <div className="relative min-h-0 flex-1">{children}</div>
    </section>
  );
}

function MissingPage({ text }: { text: string }) {
  return <div className="flex h-full items-center justify-center p-8 text-center text-sm text-muted">{text}</div>;
}
