import { activeTab, useApp, type DocTab } from '../app/store';
import { appById } from './registry';
import { openAppById } from './openApp';

/**
 * 기능별 주소 (설계서 11-5): `/tools/<기능 id>`로 들어오면 그 기능을 바로 열고,
 * 탭을 옮기면 주소창도 그 기능 주소로 바꾼다 (Home = `/`). 기록을 쌓지 않고 바꾸기만 한다(뒤로 가기로 탭이 바뀌지 않게).
 */
const PREFIX = '/tools/';

/** 탭 종류 → 기능 id. 비교 탭·투명 PNG 탭은 그 기능 카드와 같은 주소. */
export function appIdOfTab(tab: DocTab | undefined): string | null {
  if (!tab) return null;
  if (tab.kind === 'app') return tab.appId ?? null;
  if (tab.kind === 'compare') return 'drawing-compare';
  if (tab.kind === 'png') return 'remove-background';
  return null;
}

/** 주소 → 기능 id (등록표에 없는 기능이면 null). */
export function appIdFromPath(pathname: string): string | null {
  const m = /^\/tools\/([a-z0-9-]+)\/?$/.exec(pathname);
  return m && appById(m[1]) ? m[1] : null;
}

/** 처음 주소로 기능을 연 적이 있는가 (React StrictMode가 효과를 두 번 불러도 탭을 두 번 열지 않게). */
let opened = false;

export const pathOfApp = (id: string | null) => (id ? PREFIX + id : '/');

/** 앱을 켤 때 한 번 부른다. 미리보기(claude.ai 링크)에서는 주소를 건드리지 않는다. */
export function startToolUrls(): () => void {
  if (import.meta.env.VITE_PREVIEW === '1') return () => undefined;
  const { pathname, search, hash } = window.location;
  const id = appIdFromPath(pathname);
  if (id && !opened) { opened = true; openAppById(id); }
  else if (pathname.startsWith(PREFIX)) window.history.replaceState(null, '', '/' + search + hash);

  const sync = () => {
    const want = pathOfApp(appIdOfTab(activeTab(useApp.getState())));
    if (window.location.pathname !== want) window.history.replaceState(null, '', want + window.location.search + window.location.hash);
  };
  sync();
  return useApp.subscribe((s, prev) => { if (s.activeId !== prev.activeId || s.tabs !== prev.tabs) sync(); });
}
