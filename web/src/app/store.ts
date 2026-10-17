import { create } from 'zustand';
import type { Ask } from '../commands/registry';

/**
 * 문서 탭 종류 (설계서 2-1). Drawing 탭은 U5·U6에서 열 수 있게 된다.
 * app = 아직 준비 중인 기능의 안내 탭 (설계서 11-3). 기능이 동작하면 그 기능의 탭 종류로 바뀐다.
 */
export type TabKind = 'home' | 'drawing' | 'compare' | 'png' | 'app';
export type RibbonTabId = 'home' | 'compare' | 'markup' | 'draw' | 'view' | 'tools';
export type StatusToggleId = 'grid' | 'snap' | 'ortho' | 'osnap' | 'darkView';

export type DocTab = {
  id: string;
  kind: TabKind;
  /** 탭에 보이는 이름. 비어 있으면 종류별 기본 이름(번역)을 쓴다. */
  title?: string;
  /** 저장 안 된 변경 (●). 닫을 때·브라우저 창을 닫을 때 묻는다. */
  dirty: boolean;
  ribbonTab: RibbonTabId;
  /** 리본을 접어 탭 줄만 보임. 옛 PNG 화면 탭은 접힌 채 시작 (U4까지 도면 칸을 넓게). */
  ribbonCollapsed: boolean;
  /** 시작 화면에서 끌어다 놓아 열 때 넘겨받은 파일 (탭이 처음 그려질 때 한 번 쓴다). */
  files?: File[];
  /** app 탭: 어떤 기능의 안내인지 (apps/registry.ts의 id). */
  appId?: string;
};

export const HOME_TAB_ID = 'home';

/** 탭 종류별로 처음 열리는 리본 탭 (설계서 2-1 표). */
/** 아직 옛 디자인 화면을 담은 탭 종류 (U3·U4에서 새 틀로 옮기면 비운다). */
export const LEGACY_KINDS: TabKind[] = ['png'];

export const FIRST_RIBBON: Record<TabKind, RibbonTabId> = { home: 'home', drawing: 'markup', compare: 'compare', png: 'tools', app: 'home' };

/**
 * 탭 종류마다 보이는 리본 탭 (설계서 11-3, 2026-10-17 결정: 그 탭에 맞는 리본 탭만).
 * 빈 목록 = 리본 없음 (Home 탭, 기능 안내 탭).
 */
export const RIBBON_TABS_FOR: Record<TabKind, RibbonTabId[]> = {
  home: [],
  compare: ['home', 'compare', 'markup', 'draw', 'view'],
  drawing: ['home', 'markup', 'draw', 'view', 'tools'],
  png: ['home', 'tools'],
  app: [],
};

/** 명령창 기록 한 줄. kind: info(흐림) · cmd(친 명령) · error(빨강) · ok(초록). */
export type HistoryLine = { text: string; kind: 'info' | 'cmd' | 'error' | 'ok' };

export type CmdState = {
  /** 입력칸 글자. */
  input: string;
  /** 실행 중인 명령 이름. */
  running: string | null;
  /** 지금 명령이 묻는 질문 (없으면 "Command:"). */
  ask: Ask | null;
  /** 빈 Enter로 다시 부를 직전 명령. */
  last: string | null;
  /** 이전 입력 (↑↓로 다시 부름, 최근 50개). */
  recall: string[];
  /** 오타 제안: Tab을 누르면 이 명령으로 바꾼다. */
  suggestion: string | null;
};

export type AppState = {
  tabs: DocTab[];
  history: HistoryLine[];
  activeId: string;
  language: 'en' | 'ko';
  toggles: Record<StatusToggleId, boolean>;
  /** 명령창 (설계서 6장). */
  cmd: CmdState;
  /** Ctrl+0 / PANELS: 좌우 패널 숨김 (모든 탭 공통). */
  panelsHidden: boolean;
  /** Ctrl+9 / CMDLINE: 명령창 숨김. 글자를 치면 다시 보인다. */
  cmdlineHidden: boolean;
  /** F2: 명령 기록 펼침. */
  historyExpanded: boolean;
  /** ? / F1: 명령 목록 창. */
  helpOpen: boolean;
  setCmd: (patch: Partial<CmdState>) => void;
  setUi: (patch: Partial<Pick<AppState, 'panelsHidden' | 'cmdlineHidden' | 'historyExpanded' | 'helpOpen'>>) => void;
  openTab: (kind: Exclude<TabKind, 'home'>, options?: { title?: string; files?: File[] }) => string;
  /** 기능 안내 탭을 연다. 같은 기능의 탭이 이미 있으면 그 탭으로 간다. */
  openApp: (appId: string) => string;
  closeTab: (id: string) => void;
  activate: (id: string) => void;
  /** 리본 탭을 누름: 다른 탭이면 그 탭을 펼쳐 보이고, 지금 탭을 다시 누르면 접고 펼친다. */
  setRibbonTab: (id: string, ribbonTab: RibbonTabId) => void;
  setDirty: (id: string, dirty: boolean) => void;
  /** 탭 이름 바꾸기 (비교 탭: 파일을 열면 "원본 ↔ 리비전"). */
  setTitle: (id: string, title: string) => void;
  toggle: (key: StatusToggleId) => void;
  /** 명령창 기록에 줄을 더한다 (최근 200줄만 남김). */
  log: (...lines: HistoryLine[]) => void;
};

const HISTORY_LIMIT = 200;

let nextId = 1;

type Data = 'tabs' | 'history' | 'activeId' | 'language' | 'toggles' | 'cmd' | 'panelsHidden' | 'cmdlineHidden' | 'historyExpanded' | 'helpOpen';

export const initialState = (): Pick<AppState, Data> => ({
  tabs: [{ id: HOME_TAB_ID, kind: 'home', dirty: false, ribbonTab: 'home', ribbonCollapsed: false }],
  history: [],
  activeId: HOME_TAB_ID,
  language: 'en',
  toggles: { grid: false, snap: false, ortho: false, osnap: true, darkView: false },
  cmd: { input: '', running: null, ask: null, last: null, recall: [], suggestion: null },
  panelsHidden: false,
  cmdlineHidden: false,
  historyExpanded: false,
  helpOpen: false,
});

/**
 * 앱 전체가 함께 쓰는 보관소 (설계서 5-1). 화면 부품은 읽기만 하고,
 * 값을 바꾸는 일은 명령(commands/)을 거친다. 탭 누르기처럼 명령이 없는 조작만 직접 바꾼다.
 */
export const useApp = create<AppState>()((set, get) => ({
  ...initialState(),

  setCmd: (patch) => set((s) => ({ cmd: { ...s.cmd, ...patch } })),
  setUi: (patch) => set(patch),

  openTab: (kind, options) => {
    const id = `${kind}-${nextId++}`;
    const tab: DocTab = {
      id, kind, title: options?.title, dirty: false, ribbonTab: FIRST_RIBBON[kind], ribbonCollapsed: LEGACY_KINDS.includes(kind), files: options?.files,
    };
    set((s) => ({ tabs: [...s.tabs, tab], activeId: id }));
    return id;
  },

  openApp: (appId) => {
    const open = get().tabs.find((t) => t.kind === 'app' && t.appId === appId);
    if (open) {
      set({ activeId: open.id });
      return open.id;
    }
    const id = `app-${nextId++}`;
    const tab: DocTab = { id, kind: 'app', appId, dirty: false, ribbonTab: FIRST_RIBBON.app, ribbonCollapsed: false };
    set((s) => ({ tabs: [...s.tabs, tab], activeId: id }));
    return id;
  },

  closeTab: (id) => {
    if (id === HOME_TAB_ID) return; // Home 탭은 닫을 수 없다
    const { tabs, activeId } = get();
    const index = tabs.findIndex((t) => t.id === id);
    if (index < 0) return;
    const rest = tabs.filter((t) => t.id !== id);
    // 지금 탭을 닫으면 오른쪽 탭, 없으면 왼쪽 탭으로 간다
    const nextActive = activeId === id ? (rest[index] ?? rest[index - 1]).id : activeId;
    set({ tabs: rest, activeId: nextActive });
  },

  activate: (id) => {
    if (get().tabs.some((t) => t.id === id)) set({ activeId: id });
  },

  setRibbonTab: (id, ribbonTab) =>
    set((s) => ({
      tabs: s.tabs.map((t) =>
        t.id !== id || !RIBBON_TABS_FOR[t.kind].includes(ribbonTab) ? t : { ...t, ribbonTab, ribbonCollapsed: t.ribbonTab === ribbonTab ? !t.ribbonCollapsed : false }),
    })),

  setDirty: (id, dirty) => set((s) => ({ tabs: s.tabs.map((t) => (t.id === id ? { ...t, dirty } : t)) })),

  setTitle: (id, title) => set((s) => ({ tabs: s.tabs.map((t) => (t.id === id && t.title !== title ? { ...t, title } : t)) })),

  toggle: (key) => set((s) => ({ toggles: { ...s.toggles, [key]: !s.toggles[key] } })),

  log: (...lines) => set((s) => ({ history: [...s.history, ...lines].slice(-HISTORY_LIMIT) })),
}));

export const activeTab = (s: AppState): DocTab => s.tabs.find((t) => t.id === s.activeId) ?? s.tabs[0];
