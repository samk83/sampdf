import { create } from 'zustand';

/** 문서 탭 종류 (설계서 2-1). Drawing 탭은 U5·U6에서 열 수 있게 된다. */
export type TabKind = 'home' | 'drawing' | 'compare' | 'png';
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
  /** 시작 화면에서 끌어다 놓아 열 때 넘겨받은 파일 (탭이 처음 그려질 때 한 번 쓴다). */
  files?: File[];
};

export const HOME_TAB_ID = 'home';

/** 탭 종류별로 처음 열리는 리본 탭 (설계서 2-1 표). */
export const FIRST_RIBBON: Record<TabKind, RibbonTabId> = { home: 'home', drawing: 'markup', compare: 'compare', png: 'tools' };

/** 명령창 기록 한 줄. kind: info(흐림) · cmd(친 명령) · error(빨강) · ok(초록). */
export type HistoryLine = { text: string; kind: 'info' | 'cmd' | 'error' | 'ok' };

export type AppState = {
  tabs: DocTab[];
  history: HistoryLine[];
  activeId: string;
  language: 'en' | 'ko';
  toggles: Record<StatusToggleId, boolean>;
  openTab: (kind: Exclude<TabKind, 'home'>, options?: { title?: string; files?: File[] }) => string;
  closeTab: (id: string) => void;
  activate: (id: string) => void;
  setRibbonTab: (id: string, ribbonTab: RibbonTabId) => void;
  setDirty: (id: string, dirty: boolean) => void;
  toggle: (key: StatusToggleId) => void;
  /** 명령창 기록에 줄을 더한다 (최근 200줄만 남김). */
  log: (...lines: HistoryLine[]) => void;
};

const HISTORY_LIMIT = 200;

let nextId = 1;

export const initialState = (): Pick<AppState, 'tabs' | 'history' | 'activeId' | 'language' | 'toggles'> => ({
  tabs: [{ id: HOME_TAB_ID, kind: 'home', dirty: false, ribbonTab: 'home' }],
  history: [],
  activeId: HOME_TAB_ID,
  language: 'en',
  toggles: { grid: false, snap: false, ortho: false, osnap: true, darkView: false },
});

/**
 * 앱 전체가 함께 쓰는 보관소 (설계서 5-1). 화면 부품은 읽기만 하고,
 * 값을 바꾸는 일은 U2부터 명령을 거친다 (지금은 탭·토글 동작만 있음).
 */
export const useApp = create<AppState>()((set, get) => ({
  ...initialState(),

  openTab: (kind, options) => {
    const id = `${kind}-${nextId++}`;
    const tab: DocTab = { id, kind, title: options?.title, dirty: false, ribbonTab: FIRST_RIBBON[kind], files: options?.files };
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

  setRibbonTab: (id, ribbonTab) => set((s) => ({ tabs: s.tabs.map((t) => (t.id === id ? { ...t, ribbonTab } : t)) })),

  setDirty: (id, dirty) => set((s) => ({ tabs: s.tabs.map((t) => (t.id === id ? { ...t, dirty } : t)) })),

  toggle: (key) => set((s) => ({ toggles: { ...s.toggles, [key]: !s.toggles[key] } })),

  log: (...lines) => set((s) => ({ history: [...s.history, ...lines].slice(-HISTORY_LIMIT) })),
}));

export const activeTab = (s: AppState): DocTab => s.tabs.find((t) => t.id === s.activeId) ?? s.tabs[0];
