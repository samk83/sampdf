import { create } from 'zustand';
import type { Change, CompareResult, FileInfo, PagePair, PageSize, Rect } from '../api';
import { assignLabels, type NumberingMode } from '../doc/numbering';
import { clampRect, inflate } from '../doc/geometry';

/**
 * 비교 탭의 상태 (설계서 5-1, U3). 문서 탭 하나마다 한 묶음 (`tabs[탭 id]`).
 * 화면 부품은 읽기만 하고, 바꾸는 일은 compare/actions.ts와 비교 명령(compare/commands.ts)이 한다.
 * 확대·이동처럼 자주 바뀌는 보기 상태는 화면 부품 안에 둔다 (명령은 viewRequest로 부탁).
 */

export type SideKey = 'left' | 'right';
export type Side = { file: FileInfo | null; size: number | null; busy: boolean; error: string | null; failedName: string | null };
export const EMPTY_SIDE: Side = { file: null, size: null, busy: false, error: null, failedName: null };

/** 사용자가 고친 표시 하나 (없는 값은 자동). 내려받을 때 그대로 서버로 보낸다. */
export type MarkEdit = { cloud?: Rect; delta?: { x: number; y: number }; highlights?: Rect[]; label?: string };

/** 되돌리기 대상: 표시 수정·내보내기에서 빼기·검토됨 (설계서 5-8, 탭마다). */
export type Marks = { edits: Record<string, MarkEdit>; excluded: string[]; reviewed: string[] };
const EMPTY_MARKS: Marks = { edits: {}, excluded: [], reviewed: [] };

export type CompareSettings = {
  numbering: NumberingMode;
  startLabel: string;
  /** 글자 변경도 구름 (꺼지면 노란 하이라이트만). */
  cloudText: boolean;
  /** 오른쪽 아래 제목란 무시 (바꾸면 다시 비교). */
  ignoreTitle: boolean;
  /** 리비전 칸에 지워진 선을 빨갛게. */
  showRemoved: boolean;
};

export type Filter = 'all' | 'text' | 'graphic';
export type ViewRequest = { kind: 'fit' | 'in' | 'out'; n: number };

export type CompareTab = {
  left: Side;
  right: Side;
  result: CompareResult | null;
  comparing: boolean;
  compareError: string | null;
  /** 다시 비교 요청 번호 (Try again). */
  runNo: number;
  pairIndex: number;
  settings: CompareSettings;
  viewMode: 'side' | 'overlay';
  sync: boolean;
  overlay: { opacity: number; byColor: boolean; showMarks: boolean; nudging: boolean; offset: { x: number; y: number } };
  marks: Marks;
  undo: Marks[];
  redo: Marks[];
  selectedId: string | null;
  /** 고른 변경으로 화면을 옮겨 달라는 요청 번호. */
  focusNo: number;
  filter: Filter;
  tableOpen: boolean;
  warningsSeen: boolean;
  viewRequest: ViewRequest | null;
  /** 보기 정보 (상태 표시줄용, 화면 부품이 알려 줌). */
  zoom: number | null;
};

export const DEFAULT_SETTINGS: CompareSettings = { numbering: 'sequential', startLabel: '1', cloudText: true, ignoreTitle: false, showRemoved: true };

export const newCompareTab = (): CompareTab => ({
  left: EMPTY_SIDE, right: EMPTY_SIDE, result: null, comparing: false, compareError: null, runNo: 0, pairIndex: 0,
  settings: DEFAULT_SETTINGS, viewMode: 'side', sync: true,
  overlay: { opacity: 0.55, byColor: false, showMarks: false, nudging: false, offset: { x: 0, y: 0 } },
  marks: EMPTY_MARKS, undo: [], redo: [], selectedId: null, focusNo: 0, filter: 'all', tableOpen: true, warningsSeen: false,
  viewRequest: null, zoom: null,
});

const UNDO_LIMIT = 200; // 설계서 5-8

type CompareStore = {
  tabs: Record<string, CompareTab>;
  patch: (id: string, patch: Partial<CompareTab> | ((t: CompareTab) => Partial<CompareTab>)) => void;
  /** 표시를 바꾼다. record면 바꾸기 전 상태를 되돌리기 기록에 넣는다 (끌기 중간 값은 record=false). */
  setMarks: (id: string, fn: (m: Marks) => Marks, record?: boolean) => void;
  /** 지금 표시를 되돌리기 기록에 넣기만 (끌기를 시작할 때). */
  checkpoint: (id: string) => void;
  undo: (id: string) => boolean;
  redo: (id: string) => boolean;
  remove: (id: string) => void;
};

export const useCompare = create<CompareStore>()((set, get) => {
  const tab = (id: string) => get().tabs[id] ?? newCompareTab();
  const put = (id: string, t: CompareTab) => set((s) => ({ tabs: { ...s.tabs, [id]: t } }));
  return {
    tabs: {},
    patch: (id, patch) => {
      const t = tab(id);
      put(id, { ...t, ...(typeof patch === 'function' ? patch(t) : patch) });
    },
    setMarks: (id, fn, record = true) => {
      const t = tab(id);
      const next = fn(t.marks);
      if (next === t.marks) return;
      put(id, record ? { ...t, marks: next, undo: [...t.undo, t.marks].slice(-UNDO_LIMIT), redo: [] } : { ...t, marks: next });
    },
    checkpoint: (id) => {
      const t = tab(id);
      put(id, { ...t, undo: [...t.undo, t.marks].slice(-UNDO_LIMIT), redo: [] });
    },
    undo: (id) => {
      const t = tab(id);
      const prev = t.undo[t.undo.length - 1];
      if (!prev) return false;
      put(id, { ...t, marks: prev, undo: t.undo.slice(0, -1), redo: [...t.redo, t.marks] });
      return true;
    },
    redo: (id) => {
      const t = tab(id);
      const next = t.redo[t.redo.length - 1];
      if (!next) return false;
      put(id, { ...t, marks: next, redo: t.redo.slice(0, -1), undo: [...t.undo, t.marks] });
      return true;
    },
    remove: (id) => set((s) => {
      const { [id]: _gone, ...rest } = s.tabs;
      return { tabs: rest };
    }),
  };
});

/** 화면 부품에서 탭 상태 읽기 (없으면 새 탭 기본값). */
const FALLBACK = newCompareTab();
export const useCompareTab = (id: string) => useCompare((s) => s.tabs[id] ?? FALLBACK);
export const compareTab = (id: string) => useCompare.getState().tabs[id] ?? FALLBACK;

// ---- 계산 (순수 함수, 테스트) ----

/** 이 변경에 구름·△이 붙는가: 그림 변경, 하이라이트가 없는 글자 변경, 또는 "글자 변경도 구름" 설정. */
export const hasCloud = (c: Change, cloudText: boolean) => c.category === 'graphic' || c.highlights.length === 0 || cloudText;
export const autoCloud = (c: Change, page: PageSize) => clampRect(inflate(c.bounds, 6), page.width, page.height);
export const autoHighlights = (c: Change) => c.highlights.map((h) => inflate(h, 1));

/** 사람에게 보이는 페이지 번호: 리비전 쪽 페이지(빠진 페이지면 원본 쪽), 1부터. 짝 순서(pairIndex)와 다를 수 있다. */
export const pageNumber = (p: PagePair | undefined, pairIndex: number) => (p?.newPage ?? p?.oldPage ?? pairIndex) + 1;

export const allChanges = (r: CompareResult | null) => (r ? r.pages.flatMap((p) => p.changes) : []);

/** △ 번호: 문서 전체에서 구름이 붙는 변경을 페이지·번호 순으로 (뺀 변경은 건너뜀). 직접 고친 번호가 우선. */
export function labelsFor(r: CompareResult | null, marks: Marks, s: CompareSettings): Record<string, string> {
  const excluded = new Set(marks.excluded);
  const ids = allChanges(r).filter((c) => !excluded.has(c.id) && hasCloud(c, s.cloudText)).map((c) => c.id);
  const labels = assignLabels(ids, s.startLabel, s.numbering);
  for (const id of ids) {
    const own = marks.edits[id]?.label?.trim();
    if (own) labels[id] = own;
  }
  return labels;
}

/** 다음·이전 변경 (페이지를 넘어감). 고른 것이 없으면 지금 페이지의 첫 변경부터. */
export function stepChange(r: CompareResult | null, pairIndex: number, selectedId: string | null, step: 1 | -1): { pairIndex: number; id: string } | null {
  if (!r) return null;
  const flat = r.pages.flatMap((p, pi) => p.changes.map((c) => ({ pi, id: c.id })));
  if (!flat.length) return null;
  const at = flat.findIndex((x) => x.id === selectedId);
  let next: number;
  if (at >= 0) next = at + step;
  else {
    // 고른 것이 없으면: 다음 = 지금 페이지 이후 첫 변경, 이전 = 지금 페이지 이전 마지막 변경
    next = step === 1 ? flat.findIndex((x) => x.pi >= pairIndex) : flat.map((x) => x.pi <= pairIndex).lastIndexOf(true);
  }
  if (next < 0 || next >= flat.length) return null;
  return { pairIndex: flat[next].pi, id: flat[next].id };
}

/** 페이지 패널 배지: 변경 수 / 변경 없음 / 새 페이지 / 빠진 페이지. */
export function pageBadge(r: CompareResult | null, index: number): { kind: 'changes' | 'none' | 'added' | 'removed'; n: number } | null {
  const p = r?.pages[index];
  if (!p) return null;
  if (p.isAddedPage) return { kind: 'added', n: 0 };
  if (p.isRemovedPage) return { kind: 'removed', n: 0 };
  return p.changes.length ? { kind: 'changes', n: p.changes.length } : { kind: 'none', n: 0 };
}

/** 변경 목록 CSV (EXPORTCSV). 엑셀이 한글을 깨뜨리지 않게 BOM을 붙인다. */
export function changesCsv(r: CompareResult, marks: Marks, labels: Record<string, string>): string {
  const excluded = new Set(marks.excluded);
  const reviewed = new Set(marks.reviewed);
  const q = (v: string | number) => {
    const s = String(v);
    return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const rows = [['No', 'Page', 'Delta', 'Type', 'Kind', 'Description', 'Match', 'Status', 'In export']];
  r.pages.forEach((p, pi) =>
    p.changes.forEach((c) =>
      rows.push([
        String(c.number), String(pageNumber(p, pi)), labels[c.id] ?? '', c.category, c.kind, c.description,
        c.confidence === 'high' ? 'High' : 'Check', reviewed.has(c.id) ? 'Reviewed' : 'Open', excluded.has(c.id) ? 'No' : 'Yes',
      ])));
  return '﻿' + rows.map((row) => row.map(q).join(',')).join('\r\n') + '\r\n';
}

/** 저장 안 된 일 (탭 ●): 표시 수정·뺀 변경·검토됨 표시. */
export const hasWork = (m: Marks) => m.excluded.length > 0 || m.reviewed.length > 0 || Object.keys(m.edits).length > 0;
