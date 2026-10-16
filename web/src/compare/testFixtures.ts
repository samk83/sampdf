import type { Change, CompareResult, PagePair } from '../api';

/** 비교 테스트용 가짜 결과 (테스트에서만 씀). */
export const rect = { x0: 10, y0: 10, x1: 30, y1: 30 };
export const change = (id: string, more: Partial<Change> = {}): Change => ({
  id, number: Number(id.replace(/\D/g, '')) || 1, kind: 'modified', category: 'graphic', confidence: 'high',
  bounds: rect, boundsInOld: rect, highlights: [], description: `change ${id}`, ...more,
});
const summary = { total: 0, added: 0, removed: 0, modified: 0, text: 0, graphic: 0 };
export const pair = (index: number, changes: Change[], more: Partial<PagePair> = {}): PagePair => ({
  index, oldPage: index, newPage: index, isAddedPage: false, isRemovedPage: false, alignment: null, summary, changes, ...more,
});
export const result = (pages: PagePair[]): CompareResult => ({
  id: 'r1', originalId: 'a', revisionId: 'b', warnings: [], pages,
  total: { ...summary, total: pages.reduce((n, p) => n + p.changes.length, 0) },
});

