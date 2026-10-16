import { beforeEach, describe, expect, it } from 'vitest';
import { change, pair, rect, result } from './testFixtures';
import { changesCsv, DEFAULT_SETTINGS, hasWork, labelsFor, pageBadge, stepChange, useCompare, type Marks } from './state';

const noMarks: Marks = { edits: {}, excluded: [], reviewed: [] };

describe('△ 번호 (labelsFor)', () => {
  const r = result([pair(0, [change('c1'), change('c2', { category: 'text', highlights: [rect] })]), pair(1, [change('c3')])]);

  it('문서 전체에서 차례로, 시작 번호부터', () => {
    expect(labelsFor(r, noMarks, DEFAULT_SETTINGS)).toEqual({ c1: '1', c2: '2', c3: '3' });
    expect(labelsFor(r, noMarks, { ...DEFAULT_SETTINGS, startLabel: 'B' })).toEqual({ c1: 'B', c2: 'C', c3: 'D' });
  });

  it('뺀 변경과 구름 없는 글자 변경은 건너뛴다', () => {
    expect(labelsFor(r, { ...noMarks, excluded: ['c1'] }, DEFAULT_SETTINGS)).toEqual({ c2: '1', c3: '2' });
    expect(labelsFor(r, noMarks, { ...DEFAULT_SETTINGS, cloudText: false })).toEqual({ c1: '1', c3: '2' });
  });

  it('같은 번호 모드, 직접 고친 번호가 우선', () => {
    expect(labelsFor(r, noMarks, { ...DEFAULT_SETTINGS, numbering: 'same', startLabel: '4' })).toEqual({ c1: '4', c2: '4', c3: '4' });
    expect(labelsFor(r, { ...noMarks, edits: { c3: { label: 'X' } } }, DEFAULT_SETTINGS).c3).toBe('X');
  });
});

describe('다음·이전 변경 (stepChange)', () => {
  const r = result([pair(0, [change('c1')]), pair(1, []), pair(2, [change('c2'), change('c3')])]);

  it('고른 것이 없으면 지금 페이지부터, 페이지를 넘어간다', () => {
    expect(stepChange(r, 0, null, 1)).toEqual({ pairIndex: 0, id: 'c1' });
    expect(stepChange(r, 1, null, 1)).toEqual({ pairIndex: 2, id: 'c2' });
    expect(stepChange(r, 1, null, -1)).toEqual({ pairIndex: 0, id: 'c1' });
    expect(stepChange(r, 0, 'c1', 1)).toEqual({ pairIndex: 2, id: 'c2' });
    expect(stepChange(r, 2, 'c2', -1)).toEqual({ pairIndex: 0, id: 'c1' });
  });

  it('끝이면 null', () => {
    expect(stepChange(r, 2, 'c3', 1)).toBeNull();
    expect(stepChange(r, 0, 'c1', -1)).toBeNull();
    expect(stepChange(null, 0, null, 1)).toBeNull();
  });
});

describe('페이지 배지·CSV·저장 안 된 일', () => {
  it('페이지 배지', () => {
    const r = result([pair(0, [change('c1')]), pair(1, []), pair(2, [], { isAddedPage: true }), pair(3, [], { isRemovedPage: true })]);
    expect([0, 1, 2, 3, 4].map((i) => pageBadge(r, i)?.kind ?? null)).toEqual(['changes', 'none', 'added', 'removed', null]);
  });

  it('CSV: BOM, 따옴표, 검토·내보내기 열', () => {
    const r = result([pair(0, [change('c1', { description: 'Door "D1", moved' }), change('c2')])]);
    const csv = changesCsv(r, { ...noMarks, excluded: ['c2'], reviewed: ['c1'] }, { c1: '1' });
    expect(csv.charCodeAt(0)).toBe(0xfeff);
    const rows = csv.slice(1).trim().split('\r\n');
    expect(rows[0]).toBe('No,Page,Delta,Type,Kind,Description,Match,Status,In export');
    expect(rows[1]).toBe('1,1,1,graphic,modified,"Door ""D1"", moved",High,Reviewed,Yes');
    expect(rows[2]).toBe('2,1,,graphic,modified,change c2,High,Open,No');
  });

  it('CSV 페이지 = 리비전 페이지 번호 (원본에만 있는 페이지가 끼어 있어도)', () => {
    const r = result([pair(0, []), pair(1, [], { newPage: null, isRemovedPage: true }), pair(2, [change('c1')], { oldPage: 2, newPage: 1 })]);
    expect(changesCsv(r, noMarks, {}).split('\r\n')[1].split(',')[1]).toBe('2');
  });

  it('hasWork = 고친 표시·뺀 변경·검토됨이 있을 때', () => {
    expect(hasWork(noMarks)).toBe(false);
    expect(hasWork({ ...noMarks, reviewed: ['c1'] })).toBe(true);
    expect(hasWork({ ...noMarks, edits: { c1: { label: 'A' } } })).toBe(true);
  });
});

describe('되돌리기 (설계서 5-8, 탭마다)', () => {
  const st = () => useCompare.getState();
  beforeEach(() => useCompare.setState({ tabs: {} }));

  it('setMarks는 기록, 끌기 중간 값(record=false)은 기록 안 함, undo·redo', () => {
    st().setMarks('t1', (m) => ({ ...m, excluded: ['c1'] }));
    st().checkpoint('t1');
    st().setMarks('t1', (m) => ({ ...m, edits: { c1: { label: 'A' } } }), false);
    st().setMarks('t1', (m) => ({ ...m, edits: { c1: { label: 'AB' } } }), false);
    expect(st().tabs.t1.undo).toHaveLength(2);
    expect(st().undo('t1')).toBe(true);
    expect(st().tabs.t1.marks).toEqual({ edits: {}, excluded: ['c1'], reviewed: [] });
    expect(st().undo('t1')).toBe(true);
    expect(st().tabs.t1.marks.excluded).toEqual([]);
    expect(st().undo('t1')).toBe(false);
    expect(st().redo('t1')).toBe(true);
    expect(st().redo('t1')).toBe(true);
    expect(st().tabs.t1.marks.edits.c1.label).toBe('AB');
    expect(st().redo('t1')).toBe(false);
  });

  it('탭마다 따로, 새로 바꾸면 redo는 지워진다', () => {
    st().setMarks('t1', (m) => ({ ...m, reviewed: ['c1'] }));
    st().setMarks('t2', (m) => ({ ...m, reviewed: ['c9'] }));
    st().undo('t1');
    expect(st().tabs.t2.marks.reviewed).toEqual(['c9']);
    st().setMarks('t1', (m) => ({ ...m, excluded: ['c2'] }));
    expect(st().redo('t1')).toBe(false);
  });

  it('바뀐 것이 없으면 기록하지 않는다', () => {
    st().setMarks('t1', (m) => m);
    expect(st().tabs.t1).toBeUndefined();
  });
});
