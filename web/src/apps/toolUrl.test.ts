import { describe, expect, it } from 'vitest';
import { appIdFromPath, appIdOfTab, pathOfApp } from './toolUrl';

describe('기능별 주소 (설계서 11-5)', () => {
  it('/tools/<id>는 등록표에 있는 기능만 읽는다', () => {
    expect(appIdFromPath('/tools/merge-pdf')).toBe('merge-pdf');
    expect(appIdFromPath('/tools/drawing-compare/')).toBe('drawing-compare');
    expect(appIdFromPath('/tools/no-such-tool')).toBeNull();
    expect(appIdFromPath('/tools/')).toBeNull();
    expect(appIdFromPath('/')).toBeNull();
    expect(appIdFromPath('/tools/Merge-PDF')).toBeNull();
  });

  it('탭 → 주소: 비교·투명 PNG 탭은 그 기능 카드와 같은 주소, Home은 /', () => {
    const base = { dirty: false, ribbonTab: 'home' as const, ribbonCollapsed: false };
    expect(pathOfApp(appIdOfTab({ id: 'home', kind: 'home', ...base }))).toBe('/');
    expect(pathOfApp(appIdOfTab({ id: 'c', kind: 'compare', ...base }))).toBe('/tools/drawing-compare');
    expect(pathOfApp(appIdOfTab({ id: 'p', kind: 'png', ...base }))).toBe('/tools/remove-background');
    expect(pathOfApp(appIdOfTab({ id: 'a', kind: 'app', appId: 'split-pdf', ...base }))).toBe('/tools/split-pdf');
    expect(pathOfApp(appIdOfTab(undefined))).toBe('/');
  });
});
