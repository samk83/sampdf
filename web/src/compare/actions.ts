import { closeFile, exportPdf, runCompare, saveBlob, uploadFile, type MarkEditPayload } from '../api';
import { useApp } from '../app/store';
import { translate, type MessageKey } from '../i18n';
import {
  allChanges, changesCsv, compareTab, EMPTY_SIDE, labelsFor, stepChange, useCompare, type CompareSettings, type SideKey,
} from './state';

/**
 * 비교 탭에서 하는 일 (파일 열기·비교·내려받기·고르기). 명령과 화면 부품이 함께 쓴다.
 * 서버 일은 끝나기 전에 탭이 닫힐 수 있어서, 끝난 뒤 탭이 아직 있는지 먼저 본다.
 */

const app = () => useApp.getState();
const store = () => useCompare.getState();
const tr = (key: MessageKey, params?: Record<string, string | number>) => translate(app().language, key, params);
const log = (text: string, kind: 'info' | 'cmd' | 'ok' | 'error' = 'info') => app().log({ text, kind });
const alive = (tabId: string) => app().tabs.some((t) => t.id === tabId);

/** 탭마다 진행 중인 비교 (취소용). */
const running = new Map<string, AbortController>();

export async function openFile(tabId: string, side: SideKey, file: File): Promise<void> {
  const before = compareTab(tabId)[side];
  store().patch(tabId, (t) => ({ [side]: { ...t[side], busy: true, error: null, failedName: null } }));
  try {
    const info = await uploadFile(file);
    if (!alive(tabId)) return closeFile(info.id); // 올리는 사이 탭을 닫음
    if (before.file) closeFile(before.file.id);
    store().patch(tabId, {
      [side]: { file: info, size: file.size, busy: false, error: null, failedName: null },
      result: null, compareError: null, pairIndex: 0, selectedId: null, warningsSeen: false,
      marks: { edits: {}, excluded: [], reviewed: [] }, undo: [], redo: [],
    });
    log(tr(side === 'left' ? 'compare.log.original' : 'compare.log.revised', { name: info.name, n: info.pages.length }));
    startCompare(tabId);
  } catch (e) {
    if (!alive(tabId)) return;
    const message = (e as Error).message;
    store().patch(tabId, (t) => ({ [side]: { ...t[side], busy: false, error: message, failedName: file.name } }));
    log(`COMPARE ${tr('compare.log.openFailed', { name: file.name, reason: message })}`, 'error');
  }
}

/** 두 파일이 다 있으면 비교 (이미 도는 비교는 취소). 설정의 "제목란 무시"를 바꿀 때·Try again도 여기로. */
export function startCompare(tabId: string): void {
  running.get(tabId)?.abort();
  const t = compareTab(tabId);
  if (!t.left.file || !t.right.file) return;
  const ctrl = new AbortController();
  running.set(tabId, ctrl);
  store().patch(tabId, { comparing: true, compareError: null, result: null, selectedId: null });
  log(`COMPARE ${t.left.file.name} ↔ ${t.right.file.name}`);
  runCompare(t.left.file.id, t.right.file.id, t.settings.ignoreTitle, ctrl.signal)
    .then((r) => {
      if (ctrl.signal.aborted || !alive(tabId)) return;
      const first = r.pages.findIndex((p) => p.changes.length > 0 || p.isAddedPage || p.isRemovedPage);
      store().patch(tabId, {
        result: r, comparing: false, pairIndex: first >= 0 ? first : 0, warningsSeen: false,
        marks: { edits: {}, excluded: [], reviewed: [] }, undo: [], redo: [], viewRequest: null,
      });
      const n = r.total.total;
      log(n ? tr('compare.log.done', { n, pages: r.pages.length }) : tr('compare.log.noChanges'), 'ok');
    })
    .catch((e: Error) => {
      if (ctrl.signal.aborted || !alive(tabId)) return;
      store().patch(tabId, { comparing: false, compareError: e.message });
      log(`COMPARE ${e.message}`, 'error');
    })
    .finally(() => {
      if (running.get(tabId) === ctrl) running.delete(tabId);
    });
}

/** 진행 창의 Cancel·Esc. 비교 결과 없이 두 파일은 열린 채로 남는다. */
export function cancelCompare(tabId: string): boolean {
  const ctrl = running.get(tabId);
  if (!ctrl) return false;
  ctrl.abort();
  running.delete(tabId);
  store().patch(tabId, { comparing: false });
  log(tr('compare.log.cancelled'));
  return true;
}

export function closeSide(tabId: string, side: SideKey): void {
  running.get(tabId)?.abort();
  const t = compareTab(tabId);
  if (t[side].file) closeFile(t[side].file!.id);
  store().patch(tabId, { [side]: EMPTY_SIDE, result: null, comparing: false, compareError: null, selectedId: null });
}

/** 탭을 닫을 때: 서버에 올린 파일을 닫고 상태를 지운다. */
export function disposeTab(tabId: string): void {
  running.get(tabId)?.abort();
  running.delete(tabId);
  const t = useCompare.getState().tabs[tabId];
  if (t?.left.file) closeFile(t.left.file.id);
  if (t?.right.file) closeFile(t.right.file.id);
  store().remove(tabId);
}

/** SWAP: 원본과 리비전 바꾸기 → 다시 비교. */
export function swapSides(tabId: string): boolean {
  const t = compareTab(tabId);
  if (!t.left.file && !t.right.file) return false;
  store().patch(tabId, { left: t.right, right: t.left, result: null, selectedId: null });
  startCompare(tabId);
  return true;
}

export function setSettings(tabId: string, patch: Partial<CompareSettings>): void {
  const before = compareTab(tabId).settings;
  store().patch(tabId, { settings: { ...before, ...patch } });
  if (patch.ignoreTitle !== undefined && patch.ignoreTitle !== before.ignoreTitle) startCompare(tabId);
}

/** 변경 하나 고르기 (화면을 그 자리로 옮김). 다른 페이지면 그 페이지로. */
export function selectChange(tabId: string, id: string | null, focus = true): void {
  const t = compareTab(tabId);
  if (!id) return store().patch(tabId, { selectedId: null });
  const pi = t.result?.pages.findIndex((p) => p.changes.some((c) => c.id === id)) ?? -1;
  store().patch(tabId, { selectedId: id, pairIndex: pi >= 0 ? pi : t.pairIndex, focusNo: focus ? t.focusNo + 1 : t.focusNo });
}

/** NEXT·PREV. 끝이면 false. */
export function stepTo(tabId: string, step: 1 | -1): boolean {
  const t = compareTab(tabId);
  const to = stepChange(t.result, t.pairIndex, t.selectedId, step);
  if (!to) return false;
  selectChange(tabId, to.id);
  return true;
}

const toggleIn = (list: string[], id: string, on?: boolean) => {
  const has = list.includes(id);
  const want = on ?? !has;
  return want === has ? list : want ? [...list, id] : list.filter((x) => x !== id);
};

/** 내보내기에 넣기·빼기 (표의 체크, EXCLUDE, Delete 키). */
export function setIncluded(tabId: string, id: string, included: boolean): void {
  store().setMarks(tabId, (m) => {
    const excluded = toggleIn(m.excluded, id, !included);
    return excluded === m.excluded ? m : { ...m, excluded };
  });
}

export function setReviewed(tabId: string, id: string, reviewed?: boolean): void {
  store().setMarks(tabId, (m) => {
    const list = toggleIn(m.reviewed, id, reviewed);
    return list === m.reviewed ? m : { ...m, reviewed: list };
  });
}

/** 표시가 들어간 리비전 PDF 내려받기 (EXPORT, 설계서 U8 전까지는 창 없이 바로). */
const exporting = new Set<string>();

export async function exportMarked(tabId: string): Promise<boolean> {
  const t = compareTab(tabId);
  if (!t.result || !t.right.file) return false;
  // 만드는 중에 다시 누르면 (Ctrl+E 두 번) 두 번 내려받지 않게
  if (exporting.has(tabId)) {
    log(tr('compare.log.exportBusy'));
    return false;
  }
  exporting.add(tabId);
  const excluded = new Set(t.marks.excluded);
  const labels = labelsFor(t.result, t.marks, t.settings);
  const payload: Record<string, MarkEditPayload> = {};
  for (const c of allChanges(t.result)) {
    if (excluded.has(c.id)) continue;
    const e = t.marks.edits[c.id];
    if (labels[c.id] || e) payload[c.id] = { label: labels[c.id], cloud: e?.cloud, delta: e?.delta, highlights: e?.highlights };
  }
  log(tr('compare.log.exporting'));
  try {
    await exportPdf(t.result.id, {
      revisionLabel: t.settings.startLabel.trim() || '1', excluded: [...excluded], cloudTextChanges: t.settings.cloudText, edits: payload,
    }, t.right.file.name.replace(/\.[^.]+$/, '') + ' (compared).pdf');
    log(tr('compare.log.exported'), 'ok');
    return true;
  } catch (e) {
    log(tr('compare.log.exportFailed', { reason: (e as Error).message }), 'error');
    return false;
  } finally {
    exporting.delete(tabId);
  }
}

export function exportCsv(tabId: string): boolean {
  const t = compareTab(tabId);
  if (!t.result || !t.right.file) return false;
  const csv = changesCsv(t.result, t.marks, labelsFor(t.result, t.marks, t.settings));
  saveBlob(new Blob([csv], { type: 'text/csv;charset=utf-8' }), t.right.file.name.replace(/\.[^.]+$/, '') + ' changes.csv');
  log(tr('compare.log.csv'), 'ok');
  return true;
}

/** 탭 제목: `Compare · a ↔ b` (파일이 바뀌면 같이). */
export function compareTitle(tabId: string): string | undefined {
  const t = compareTab(tabId);
  const base = (n?: string) => n?.replace(/\.[^.]+$/, '');
  if (!t.left.file && !t.right.file) return undefined;
  return `${tr('tabs.compare')} · ${base(t.left.file?.name) ?? '…'} ↔ ${base(t.right.file?.name) ?? '…'}`;
}
