import { beforeEach, describe, expect, it } from 'vitest';
import { activeTab, FIRST_RIBBON, HOME_TAB_ID, initialState, RIBBON_TABS_FOR, useApp } from './store';

const s = () => useApp.getState();

describe('문서 탭 (설계서 2-1)', () => {
  beforeEach(() => useApp.setState(initialState()));

  it('처음에는 Home 탭 하나', () => {
    expect(s().tabs.map((t) => t.kind)).toEqual(['home']);
    expect(activeTab(s()).id).toBe(HOME_TAB_ID);
  });

  it('탭을 열면 그 탭으로 가고, 종류별 첫 리본 탭이 정해진다', () => {
    const c = s().openTab('compare');
    expect(s().activeId).toBe(c);
    expect(activeTab(s()).ribbonTab).toBe('compare');
    const p = s().openTab('png');
    expect(activeTab(s()).ribbonTab).toBe('tools');
    expect(s().tabs.map((t) => t.id)).toEqual([HOME_TAB_ID, c, p]);
  });

  it('비교 탭은 리본이 펼쳐진 채 열린다 (U3부터 새 화면)', () => {
    const c = s().openTab('compare');
    expect(s().tabs.find((t) => t.id === c)).toMatchObject({ ribbonTab: 'compare', ribbonCollapsed: false });
  });

  it('옛 화면 탭(PNG)은 리본이 접힌 채 열리고, 리본 탭을 누르면 펼치고 같은 탭을 다시 누르면 접는다', () => {
    const c = s().openTab('png');
    const tab = () => s().tabs.find((t) => t.id === c)!;
    expect(tab().ribbonCollapsed).toBe(true);
    s().setRibbonTab(c, 'home');
    expect(tab()).toMatchObject({ ribbonTab: 'home', ribbonCollapsed: false });
    s().setRibbonTab(c, 'home');
    expect(tab().ribbonCollapsed).toBe(true);
    s().setRibbonTab(c, 'home');
    expect(tab().ribbonCollapsed).toBe(false);
  });

  it('Home 탭은 닫을 수 없다', () => {
    s().closeTab(HOME_TAB_ID);
    expect(s().tabs).toHaveLength(1);
  });

  it('지금 탭을 닫으면 오른쪽, 없으면 왼쪽 탭으로 간다', () => {
    const a = s().openTab('compare');
    const b = s().openTab('png');
    const c = s().openTab('compare');
    s().activate(b);
    s().closeTab(b);
    expect(s().activeId).toBe(c);
    s().closeTab(c);
    expect(s().activeId).toBe(a);
    s().closeTab(a);
    expect(s().activeId).toBe(HOME_TAB_ID);
  });

  it('다른 탭을 닫아도 지금 탭은 그대로', () => {
    const a = s().openTab('compare');
    const b = s().openTab('png');
    s().closeTab(a);
    expect(s().activeId).toBe(b);
  });

  it('없는 탭으로는 가지 않는다', () => {
    s().activate('nope');
    expect(s().activeId).toBe(HOME_TAB_ID);
  });

  it('탭마다 리본 탭·저장 안 됨 표시를 따로 기억한다', () => {
    const a = s().openTab('compare');
    const b = s().openTab('compare');
    s().setRibbonTab(a, 'view');
    s().setDirty(b, true);
    expect(s().tabs.find((t) => t.id === a)).toMatchObject({ ribbonTab: 'view', ribbonCollapsed: false, dirty: false });
    expect(s().tabs.find((t) => t.id === b)).toMatchObject({ ribbonTab: 'compare', ribbonCollapsed: false, dirty: true });
  });

  it('탭 이름을 바꾼다', () => {
    const a = s().openTab('compare');
    s().setTitle(a, 'A-101 ↔ A-101 rev B');
    expect(s().tabs.find((t) => t.id === a)?.title).toBe('A-101 ↔ A-101 rev B');
  });
});

describe('상태 토글과 명령창 기록', () => {
  beforeEach(() => useApp.setState(initialState()));

  it('토글은 켜고 끈다 (OSNAP은 처음부터 켜짐)', () => {
    expect(s().toggles.osnap).toBe(true);
    s().toggle('grid');
    expect(s().toggles.grid).toBe(true);
    s().toggle('grid');
    expect(s().toggles.grid).toBe(false);
  });

  it('기록은 최근 200줄만 남긴다', () => {
    for (let i = 0; i < 205; i++) s().log({ text: `line ${i}`, kind: 'info' });
    expect(s().history).toHaveLength(200);
    expect(s().history[0].text).toBe('line 5');
  });
});

describe('기능 안내 탭과 탭별 리본 (설계서 11-3)', () => {
  beforeEach(() => useApp.setState(initialState()));

  it('같은 기능의 안내 탭은 하나만 열린다', () => {
    const a = s().openApp('merge-pdf');
    s().activate(HOME_TAB_ID);
    const b = s().openApp('merge-pdf');
    expect(b).toBe(a);
    expect(s().activeId).toBe(a);
    expect(s().tabs.filter((t) => t.kind === 'app')).toHaveLength(1);
    s().openApp('ocr');
    expect(s().tabs.filter((t) => t.kind === 'app').map((t) => t.appId)).toEqual(['merge-pdf', 'ocr']);
  });

  it('리본이 있는 탭은 첫 리본 탭이 보이는 목록 안에 있고, Home·안내 탭은 리본이 없다', () => {
    for (const kind of Object.keys(RIBBON_TABS_FOR) as (keyof typeof RIBBON_TABS_FOR)[]) {
      const list = RIBBON_TABS_FOR[kind];
      if (list.length) expect(list, kind).toContain(FIRST_RIBBON[kind]);
    }
    expect(RIBBON_TABS_FOR.home).toEqual([]);
    expect(RIBBON_TABS_FOR.app).toEqual([]);
    expect(RIBBON_TABS_FOR.png).not.toContain('draw');
  });

  it('그 탭에 없는 리본 탭으로는 바뀌지 않는다', () => {
    const p = s().openTab('png');
    s().setRibbonTab(p, 'draw');
    expect(s().tabs.find((t) => t.id === p)?.ribbonTab).toBe('tools');
  });
});
